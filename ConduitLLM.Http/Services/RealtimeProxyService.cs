using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Realtime;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service that proxies WebSocket connections between clients and real-time audio providers.
    /// </summary>
    public class RealtimeProxyService : IRealtimeProxyService
    {
        private readonly IRealtimeMessageTranslator _translator;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IRealtimeConnectionManager _connectionManager;
        private readonly ILogger<RealtimeProxyService> _logger;

        public RealtimeProxyService(
            IRealtimeMessageTranslator translator,
            IVirtualKeyService virtualKeyService,
            IRealtimeConnectionManager connectionManager,
            ILogger<RealtimeProxyService> logger)
        {
            _translator = translator ?? throw new ArgumentNullException(nameof(translator));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task HandleConnectionAsync(
            string connectionId,
            WebSocket clientWebSocket,
            VirtualKey virtualKey,
            string model,
            string? provider,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Starting proxy for connection {ConnectionId}, model {Model}, provider {Provider}",
                connectionId, model, provider);

            // Validate virtual key has permissions
            if (!virtualKey.IsEnabled || (virtualKey.MaxBudget.HasValue && virtualKey.CurrentSpend >= virtualKey.MaxBudget.Value))
            {
                throw new UnauthorizedAccessException("Virtual key is not active or has exceeded budget");
            }

            // Get the provider client and establish connection
            var audioRouter = _connectionManager as IAudioRouter ?? 
                throw new InvalidOperationException("Connection manager must implement IAudioRouter");
                
            // Create session configuration
            var sessionConfig = new RealtimeSessionConfig
            {
                Model = model,
                Voice = "alloy", // Default voice, could be made configurable
                SystemPrompt = "You are a helpful assistant."
            };
            
            var realtimeClient = await audioRouter.GetRealtimeClientAsync(sessionConfig, virtualKey.KeyHash);
            if (realtimeClient == null)
            {
                throw new InvalidOperationException($"No real-time audio provider available for model {model}");
            }

            // Connect to provider
            RealtimeSession? providerSession = null;
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            try
            {
                // Create the session
                providerSession = await realtimeClient.CreateSessionAsync(sessionConfig, virtualKey.KeyHash, cancellationToken);
                
                // Get the duplex stream from the provider
                var providerStream = realtimeClient.StreamAudioAsync(providerSession, cts.Token);
                
                // Start proxying in both directions
                var clientToProvider = ProxyClientToProviderAsync(
                    clientWebSocket, providerStream, connectionId, virtualKey.KeyHash, cts.Token);
                var providerToClient = ProxyProviderToClientAsync(
                    providerStream, clientWebSocket, connectionId, virtualKey.KeyHash, cts.Token);

                await Task.WhenAny(clientToProvider, providerToClient);
                
                // If one direction fails, cancel the other
                cts.Cancel();
                
                await Task.WhenAll(clientToProvider, providerToClient);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Proxy connection {ConnectionId} cancelled", connectionId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in proxy for connection {ConnectionId}", connectionId);
                throw;
            }
            finally
            {
                // Ensure client WebSocket is closed
                await CloseWebSocketAsync(clientWebSocket, "Proxy connection ended");
                
                // Close provider session
                if (providerSession != null)
                {
                    await realtimeClient.CloseSessionAsync(providerSession, cancellationToken);
                }
            }
        }

        private async Task ProxyClientToProviderAsync(
            WebSocket clientWs,
            IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> providerStream,
            string connectionId,
            string virtualKey,
            CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            
            while (!cancellationToken.IsCancellationRequested &&
                   clientWs.State == WebSocketState.Open &&
                   providerStream.IsConnected)
            {
                try
                {
                    var result = await clientWs.ReceiveAsync(buffer, cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Client closed connection {ConnectionId}", connectionId);
                        break;
                    }
                    
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // Assume binary data is audio
                        var audioFrame = new RealtimeAudioFrame
                        {
                            AudioData = buffer.Array!.Take(result.Count).ToArray(),
                            IsOutput = false,
                            SampleRate = 24000, // Default, should be configurable
                            Channels = 1
                        };
                        
                        await providerStream.SendAsync(audioFrame, cancellationToken);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                        
                        // Try to parse as JSON control message
                        try
                        {
                            using var doc = JsonDocument.Parse(message);
                            var root = doc.RootElement;
                            
                            if (root.TryGetProperty("type", out var typeElement))
                            {
                                var type = typeElement.GetString();
                                
                                if (type == "audio" && root.TryGetProperty("data", out var dataElement))
                                {
                                    // Audio data sent as base64 in JSON
                                    var audioData = Convert.FromBase64String(dataElement.GetString() ?? "");
                                    var audioFrame = new RealtimeAudioFrame
                                    {
                                        AudioData = audioData,
                                        IsOutput = false,
                                        SampleRate = 24000,
                                        Channels = 1
                                    };
                                    
                                    await providerStream.SendAsync(audioFrame, cancellationToken);
                                }
                                // Handle other control messages as needed
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing client message");
                        }
                    }
                }
                catch (WebSocketException ex)
                {
                    _logger.LogError(ex, "WebSocket error in client->provider proxy for {ConnectionId}", connectionId);
                    break;
                }
            }
        }

        private async Task ProxyProviderToClientAsync(
            IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> providerStream,
            WebSocket clientWs,
            string connectionId,
            string virtualKey,
            CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var response in providerStream.ReceiveAsync(cancellationToken))
                {
                    if (!clientWs.State.Equals(WebSocketState.Open))
                    {
                        _logger.LogInformation("Client WebSocket closed for {ConnectionId}", connectionId);
                        break;
                    }

                    try
                    {
                        // Convert response to client format
                        var clientMessage = ConvertResponseToClientMessage(response);
                        var messageBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(clientMessage));
                        
                        await clientWs.SendAsync(
                            new ArraySegment<byte>(messageBytes),
                            WebSocketMessageType.Text,
                            true,
                            cancellationToken);
                        
                        // Track usage if applicable
                        // Note: Usage tracking would need to be implemented based on provider-specific events
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing provider response for {ConnectionId}", connectionId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Provider stream cancelled for {ConnectionId}", connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in provider->client proxy for {ConnectionId}", connectionId);
            }
        }

        private RealtimeMessage? ParseClientMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("type", out var typeElement))
                {
                    var type = typeElement.GetString();
                    
                    // Simple parsing based on type
                    return type switch
                    {
                        "audio_frame" => new RealtimeAudioFrame
                        {
                            AudioData = Convert.FromBase64String(
                                root.GetProperty("audio").GetString() ?? ""),
                            IsOutput = false
                        },
                        _ => null
                    };
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            
            return null;
        }

        private RealtimeUsageUpdate? ParseUsageFromProviderMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("type", out var typeElement) && 
                    typeElement.GetString() == "response.done" &&
                    root.TryGetProperty("response", out var response) &&
                    response.TryGetProperty("usage", out var usage))
                {
                    var update = new RealtimeUsageUpdate();
                    
                    if (usage.TryGetProperty("total_tokens", out var totalTokens))
                        update.TotalTokens = totalTokens.GetInt64();
                        
                    if (usage.TryGetProperty("input_tokens", out var inputTokens))
                        update.InputTokens = inputTokens.GetInt64();
                        
                    if (usage.TryGetProperty("output_tokens", out var outputTokens))
                        update.OutputTokens = outputTokens.GetInt64();
                        
                    if (usage.TryGetProperty("input_token_details", out var inputDetails))
                    {
                        update.InputTokenDetails = new Dictionary<string, object>();
                        foreach (var prop in inputDetails.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Number)
                                update.InputTokenDetails[prop.Name] = prop.Value.GetInt64();
                        }
                    }
                    
                    if (usage.TryGetProperty("output_token_details", out var outputDetails))
                    {
                        update.OutputTokenDetails = new Dictionary<string, object>();
                        foreach (var prop in outputDetails.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Number)
                                update.OutputTokenDetails[prop.Name] = prop.Value.GetInt64();
                        }
                    }
                    
                    return update;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing usage from provider message");
            }
            
            return null;
        }

        private async Task HandleUsageUpdate(string connectionId, string virtualKey, RealtimeUsageUpdate usage)
        {
            try
            {
                // Update usage statistics
                var stats = new ConnectionUsageStats
                {
                    AudioDurationSeconds = 0, // Will be tracked separately
                    MessagesSent = 0,
                    MessagesReceived = 0,
                    EstimatedCost = 0.01m * usage.TotalTokens // Simple cost calculation
                };
                await _connectionManager.UpdateUsageStatsAsync(connectionId, stats);
                
                // Update spend for virtual key tracking
                var estimatedCost = stats.EstimatedCost;
                if (estimatedCost > 0)
                {
                    // TODO: Need to look up virtual key entity to update spend
                    // var virtualKeyEntity = await _virtualKeyService.GetVirtualKeyByKeyValueAsync(virtualKey);
                    // if (virtualKeyEntity != null)
                    // {
                    //     await _virtualKeyService.UpdateSpendAsync(virtualKeyEntity.Id, estimatedCost);
                    // }
                }
                    
                _logger.LogDebug(
                    "Updated usage for connection {ConnectionId}: {TotalTokens} tokens",
                    connectionId, usage.TotalTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling usage update for connection {ConnectionId}", connectionId);
            }
        }

        private object ConvertResponseToClientMessage(RealtimeResponse response)
        {
            // Convert to a simple client-friendly format
            return response.EventType switch
            {
                RealtimeEventType.AudioDelta => new
                {
                    type = "audio",
                    data = response.Audio != null ? Convert.ToBase64String(response.Audio.Data) : null,
                    isComplete = response.Audio?.IsComplete ?? false
                },
                RealtimeEventType.TranscriptionDelta => new
                {
                    type = "transcription",
                    text = response.Transcription?.Text,
                    role = response.Transcription?.Role,
                    isFinal = response.Transcription?.IsFinal ?? false
                },
                RealtimeEventType.TextResponse => new
                {
                    type = "text",
                    text = response.TextResponse
                },
                RealtimeEventType.ToolCallRequest => new
                {
                    type = "function_call",
                    callId = response.ToolCall?.CallId,
                    name = response.ToolCall?.FunctionName,
                    arguments = response.ToolCall?.Arguments
                },
                RealtimeEventType.Error => new
                {
                    type = "error",
                    message = response.Error?.Message,
                    code = response.Error?.Code
                },
                _ => new
                {
                    type = response.EventType.ToString().ToLowerInvariant(),
                    data = response
                }
            };
        }

        private async Task CloseWebSocketAsync(WebSocket webSocket, string reason)
        {
            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        reason,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing WebSocket");
                }
            }
        }
        public async Task<ProxyConnectionStatus?> GetConnectionStatusAsync(string connectionId)
        {
            var info = await _connectionManager.GetConnectionAsync(connectionId);
            if (info == null)
                return null;

            return new ProxyConnectionStatus
            {
                ConnectionId = connectionId,
                State = info.State switch
                {
                    "active" => ProxyConnectionState.Active,
                    "closed" => ProxyConnectionState.Closed,
                    "error" => ProxyConnectionState.Failed,
                    _ => ProxyConnectionState.Connecting
                },
                Provider = info.Provider,
                Model = info.Model,
                ConnectedAt = info.ConnectedAt,
                LastActivityAt = info.LastActivity,
                MessagesToProvider = info.Usage?.MessagesSent ?? 0,
                MessagesFromProvider = info.Usage?.MessagesReceived ?? 0,
                BytesSent = 0, // Not tracked in current implementation
                BytesReceived = 0, // Not tracked in current implementation
                EstimatedCost = info.EstimatedCost,
                LastError = null // Not tracked in current implementation
            };
        }

        public async Task<bool> CloseConnectionAsync(string connectionId, string? reason = null)
        {
            var info = await _connectionManager.GetConnectionAsync(connectionId);
            if (info == null)
                return false;

            await _connectionManager.UnregisterConnectionAsync(connectionId);
            return await Task.FromResult(true);
        }
    }

    /// <summary>
    /// Usage update from real-time providers.
    /// </summary>
    public class RealtimeUsageUpdate
    {
        public long TotalTokens { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public Dictionary<string, object>? InputTokenDetails { get; set; }
        public Dictionary<string, object>? OutputTokenDetails { get; set; }
    }
}