using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Realtime;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service that proxies WebSocket connections - Tracking and helper methods
    /// </summary>
    public partial class RealtimeProxyService
    {
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
                _logger.LogWarning(ex,
                "Error parsing usage from provider message");
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
                    try
                    {
                        var virtualKeyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
                        if (virtualKeyEntity != null)
                        {
                            await _virtualKeyService.UpdateSpendAsync(virtualKeyEntity.Id, estimatedCost);
                            _logger.LogDebug("Updated virtual key {VirtualKeyId} spend by ${Cost:F4} for connection {ConnectionId}",
                                virtualKeyEntity.Id, estimatedCost, connectionId);
                        }
                        else
                        {
                            _logger.LogWarning("Virtual key not found for key value during spend update for connection {ConnectionId}",
                                connectionId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update virtual key spend for connection {ConnectionId}, cost: ${Cost:F4}",
                            connectionId, estimatedCost);
                        // Don't throw - continue processing even if spend tracking fails
                    }
                }

                _logger.LogDebug("Updated usage for connection {ConnectionId}: {TotalTokens} tokens",
                connectionId,
                usage.TotalTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error handling usage update for connection {ConnectionId}",
                connectionId);
            }
        }

        private async Task TrackResponseUsageAsync(string connectionId, RealtimeResponse response, string virtualKey)
        {
            try
            {
                switch (response.EventType)
                {
                    case RealtimeEventType.AudioDelta:
                        if (response.Audio != null && response.Audio.Data.Length > 0)
                        {
                            // Assume 24kHz, 1 channel, 16-bit audio for output
                            var audioDuration = response.Audio.Data.Length / (24000.0 * 1 * 2);
                            await _usageTracker.RecordAudioUsageAsync(connectionId, audioDuration, isInput: false);
                        }
                        break;

                    case RealtimeEventType.ResponseComplete:
                        // Check if response contains usage information
                        if (response.Usage != null)
                        {
                            var usage = new Usage
                            {
                                PromptTokens = (int)(response.Usage.InputTokens ?? 0),
                                CompletionTokens = (int)(response.Usage.OutputTokens ?? 0),
                                TotalTokens = (int)(response.Usage.TotalTokens ?? 0)
                            };
                            await _usageTracker.RecordTokenUsageAsync(connectionId, usage);
                            
                            // Track function calls if any
                            if (response.Usage.FunctionCalls > 0)
                            {
                                // Record each function call
                                for (int i = 0; i < response.Usage.FunctionCalls; i++)
                                {
                                    await _usageTracker.RecordFunctionCallAsync(connectionId);
                                }
                            }
                        }
                        break;
                        
                    case RealtimeEventType.ToolCallRequest:
                        // Track function call when it's requested
                        if (response.ToolCall != null)
                        {
                            await _usageTracker.RecordFunctionCallAsync(connectionId, response.ToolCall.FunctionName);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error tracking response usage for connection {ConnectionId}",
                connectionId);
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

        /// <summary>
        /// Updates connection metrics for enhanced tracking.
        /// </summary>
        private void UpdateConnectionMetrics(string connectionId, long bytesSent = 0, long bytesReceived = 0, string? lastError = null)
        {
            lock (_metricsLock)
            {
                if (!_connectionMetrics.ContainsKey(connectionId))
                {
                    _connectionMetrics[connectionId] = new ConnectionMetrics();
                }

                var metrics = _connectionMetrics[connectionId];
                metrics.BytesSent += bytesSent;
                metrics.BytesReceived += bytesReceived;
                metrics.LastActivityAt = DateTime.UtcNow;
                
                if (!string.IsNullOrEmpty(lastError))
                {
                    metrics.LastError = lastError;
                    metrics.ErrorCount++;
                }
            }
        }

        public async Task<ProxyConnectionStatus?> GetConnectionStatusAsync(string connectionId)
        {
            var info = await _connectionManager.GetConnectionAsync(connectionId);
            if (info == null)
                return null;

            // Get enhanced metrics
            ConnectionMetrics? metrics = null;
            lock (_metricsLock)
            {
                _connectionMetrics.TryGetValue(connectionId, out metrics);
            }

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
                Provider = info.Provider ?? string.Empty,
                Model = info.Model,
                ConnectedAt = info.ConnectedAt,
                LastActivityAt = info.LastActivity,
                MessagesToProvider = info.Usage?.MessagesSent ?? 0,
                MessagesFromProvider = info.Usage?.MessagesReceived ?? 0,
                BytesSent = metrics?.BytesSent ?? 0,
                BytesReceived = metrics?.BytesReceived ?? 0,
                EstimatedCost = info.EstimatedCost,
                LastError = metrics?.LastError
            };
        }

        public async Task<bool> CloseConnectionAsync(string connectionId, string? reason = null)
        {
            var info = await _connectionManager.GetConnectionAsync(connectionId);
            if (info == null)
                return false;

            await _connectionManager.UnregisterConnectionAsync(connectionId);
            
            // Clean up metrics
            lock (_metricsLock)
            {
                _connectionMetrics.Remove(connectionId);
            }
            
            return await Task.FromResult(true);
        }
    }

    /// <summary>
    /// Enhanced metrics for realtime connections.
    /// </summary>
    internal class ConnectionMetrics
    {
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public string? LastError { get; set; }
        public int ErrorCount { get; set; }
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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