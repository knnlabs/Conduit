using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service that proxies WebSocket connections - Proxy operations
    /// </summary>
    public partial class RealtimeProxyService
    {
        private async Task ProxyClientToProviderAsync(
            WebSocket clientWs,
            IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> providerStream,
            string connectionId,
            string virtualKey,
            CancellationToken cancellationToken)
        {
            var bufferArray = new byte[4096];
            var buffer = new ArraySegment<byte>(bufferArray);

            while (!cancellationToken.IsCancellationRequested &&
                   clientWs.State == WebSocketState.Open &&
                   providerStream.IsConnected)
            {
                try
                {
                    var result = await clientWs.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Client closed connection {ConnectionId}",
                        connectionId);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // Assume binary data is audio
                        var audioFrame = new RealtimeAudioFrame
                        {
                            AudioData = bufferArray.Take(result.Count).ToArray(),
                            IsOutput = false,
                            SampleRate = 24000, // Default, should be configurable
                            Channels = 1
                        };

                        await providerStream.SendAsync(audioFrame, cancellationToken);
                        
                        // Track input audio usage
                        var audioDuration = audioFrame.AudioData.Length / (double)(audioFrame.SampleRate * audioFrame.Channels * 2); // 16-bit audio
                        await _usageTracker.RecordAudioUsageAsync(connectionId, audioDuration, isInput: true);
                        
                        // Track bytes sent to provider
                        UpdateConnectionMetrics(connectionId, bytesSent: audioFrame.AudioData.Length);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(bufferArray, 0, result.Count);
                        
                        // Track bytes sent to provider
                        UpdateConnectionMetrics(connectionId, bytesSent: result.Count);

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
                        
                        // Track input audio usage
                        var audioDuration = audioFrame.AudioData.Length / (double)(audioFrame.SampleRate * audioFrame.Channels * 2); // 16-bit audio
                        await _usageTracker.RecordAudioUsageAsync(connectionId, audioDuration, isInput: true);
                                }
                                // Handle other control messages as needed
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                "Error parsing client message");
                        }
                    }
                }
                catch (WebSocketException ex)
                {
                    _logger.LogError(ex,
                "WebSocket error in client->provider proxy for {ConnectionId}",
                connectionId);
                    
                    // Track error
                    UpdateConnectionMetrics(connectionId, lastError: ex.Message);
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
                        _logger.LogInformation("Client WebSocket closed for {ConnectionId}",
                connectionId);
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

                        // Track bytes received from provider
                        UpdateConnectionMetrics(connectionId, bytesReceived: messageBytes.Length);

                        // Track usage based on response type
                        await TrackResponseUsageAsync(connectionId, response, virtualKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                "Error processing provider response for {ConnectionId}",
                connectionId);
                        
                        // Track error
                        UpdateConnectionMetrics(connectionId, lastError: ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Provider stream cancelled for {ConnectionId}",
                connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error in provider->client proxy for {ConnectionId}",
                connectionId);
                
                // Track error
                UpdateConnectionMetrics(connectionId, lastError: ex.Message);
            }
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
                    _logger.LogWarning(ex,
                "Error closing WebSocket");
                }
            }
        }
    }
}