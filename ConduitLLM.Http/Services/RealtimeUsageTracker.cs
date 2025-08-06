using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Realtime;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Tracks usage and costs for real-time audio sessions.
    /// </summary>
    public class RealtimeUsageTracker : IRealtimeUsageTracker
    {
        private readonly ILogger<RealtimeUsageTracker> _logger;
        private readonly ConduitLLM.Configuration.Services.IModelCostService _costService;
        private readonly ConduitLLM.Configuration.Services.IVirtualKeyService _virtualKeyService;
        private readonly ConcurrentDictionary<string, SessionUsage> _sessions = new();

        public RealtimeUsageTracker(
            ILogger<RealtimeUsageTracker> logger,
            ConduitLLM.Configuration.Services.IModelCostService costService,
            ConduitLLM.Configuration.Services.IVirtualKeyService virtualKeyService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _costService = costService ?? throw new ArgumentNullException(nameof(costService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
        }

        public async Task StartTrackingAsync(string connectionId, int virtualKeyId, string model, string provider)
        {
            // Get virtual key details
            var virtualKey = await _virtualKeyService.GetVirtualKeyByIdAsync(virtualKeyId);
            if (virtualKey == null)
            {
                throw new ArgumentException($"Virtual key with ID {virtualKeyId} not found");
            }

            var session = new SessionUsage
            {
                ConnectionId = connectionId,
                VirtualKeyId = virtualKeyId,
                VirtualKey = virtualKey.KeyHash,
                Model = model,
                Provider = provider,
                StartTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                InputAudioSeconds = 0,
                OutputAudioSeconds = 0,
                InputTokens = 0,
                OutputTokens = 0,
                FunctionCalls = 0,
                EstimatedCost = 0
            };

            _sessions[connectionId] = session;

            _logger.LogInformation("Started usage tracking for connection {ConnectionId}, model {Model}, virtualKeyId {VirtualKeyId}",
                connectionId,
                model.Replace(Environment.NewLine, ""),
                virtualKeyId);

            await Task.CompletedTask;
        }

        public async Task UpdateUsageAsync(string connectionId, ConnectionUsageStats stats)
        {
            if (!_sessions.TryGetValue(connectionId, out var session))
            {
                _logger.LogWarning("Attempted to update usage for unknown connection {ConnectionId}",
                connectionId);
                return;
            }

            // Update from stats
            session.LastActivity = DateTime.UtcNow;
            session.EstimatedCost = stats.EstimatedCost;

            _logger.LogDebug("Updated usage stats for connection {ConnectionId}",
                connectionId);

            await Task.CompletedTask;
        }

        public async Task RecordAudioUsageAsync(string connectionId, double audioSeconds, bool isInput)
        {
            if (!_sessions.TryGetValue(connectionId, out var session))
            {
                _logger.LogWarning("Attempted to track audio for unknown connection {ConnectionId}",
                connectionId);
                return;
            }

            if (isInput)
            {
                session.InputAudioSeconds += audioSeconds;
            }
            else
            {
                session.OutputAudioSeconds += audioSeconds;
            }

            session.LastActivity = DateTime.UtcNow;

            _logger.LogDebug("Tracked {Seconds}s of {Type} audio for connection {ConnectionId}",
                audioSeconds,
                isInput ? "input" : "output".Replace(Environment.NewLine, ""),
                connectionId);

            await Task.CompletedTask;
        }

        public async Task RecordTokenUsageAsync(string connectionId, Usage usage)
        {
            if (!_sessions.TryGetValue(connectionId, out var session))
            {
                _logger.LogWarning("Attempted to track tokens for unknown connection {ConnectionId}",
                connectionId);
                return;
            }

            session.InputTokens += usage.PromptTokens.GetValueOrDefault();
            session.OutputTokens += usage.CompletionTokens.GetValueOrDefault();
            session.LastActivity = DateTime.UtcNow;

            _logger.LogDebug("Tracked {InputTokens} input tokens and {OutputTokens} output tokens for connection {ConnectionId}",
                usage.PromptTokens,
                usage.CompletionTokens,
                connectionId);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Records a function call for billing purposes.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="functionName">Optional function name for logging.</param>
        /// <returns>A task that completes when the function call is recorded.</returns>
        public async Task RecordFunctionCallAsync(string connectionId, string? functionName = null)
        {
            if (!_sessions.TryGetValue(connectionId, out var session))
            {
                _logger.LogWarning("Attempted to track function call for unknown connection {ConnectionId}",
                connectionId);
                return;
            }

            session.FunctionCalls++;
            session.LastActivity = DateTime.UtcNow;

            _logger.LogDebugSecure(
                "Tracked function call {FunctionName} for connection {ConnectionId}. Total calls: {TotalCalls}",
                functionName ?? "(unnamed)", connectionId, session.FunctionCalls);

            await Task.CompletedTask;
        }

        public async Task<decimal> GetEstimatedCostAsync(string connectionId)
        {
            if (!_sessions.TryGetValue(connectionId, out var session))
            {
                return 0;
            }

            // Get cost configuration for the model
            var modelCost = await _costService.GetCostForModelAsync(session.Model);
            if (modelCost == null)
            {
                _logger.LogWarning("No cost configuration found for model {Model}",
                session.Model.Replace(Environment.NewLine, ""));
                return 0;
            }

            decimal totalCost = 0;

            // Calculate token costs (costs are now per million tokens)
            if (session.InputTokens > 0)
            {
                totalCost += (session.InputTokens * modelCost.InputCostPerMillionTokens) / 1_000_000m;
            }

            if (session.OutputTokens > 0)
            {
                totalCost += (session.OutputTokens * modelCost.OutputCostPerMillionTokens) / 1_000_000m;
            }

            // For audio, we'll use a simple approximation
            // Assuming 1 minute of audio ≈ 1000 tokens for cost estimation
            if (session.InputAudioSeconds > 0)
            {
                var inputMinutes = (decimal)(session.InputAudioSeconds / 60.0);
                var estimatedTokens = inputMinutes * 1000; // 1 minute ≈ 1K tokens
                totalCost += (estimatedTokens * modelCost.InputCostPerMillionTokens) / 1_000_000m;
            }

            if (session.OutputAudioSeconds > 0)
            {
                var outputMinutes = (decimal)(session.OutputAudioSeconds / 60.0);
                var estimatedTokens = outputMinutes * 1000; // 1 minute ≈ 1K tokens
                totalCost += (estimatedTokens * modelCost.OutputCostPerMillionTokens) / 1_000_000m;
            }

            // Add function call costs
            // Assuming each function call costs approximately 100 tokens worth
            if (session.FunctionCalls > 0)
            {
                var estimatedTokens = session.FunctionCalls * 100m; // Each call ≈ 100 tokens
                totalCost += (estimatedTokens * modelCost.OutputCostPerMillionTokens) / 1_000_000m;
            }

            return totalCost;
        }

        public async Task<decimal> FinalizeUsageAsync(string connectionId, ConnectionUsageStats finalStats)
        {
            if (!_sessions.TryRemove(connectionId, out var session))
            {
                throw new InvalidOperationException($"Connection {connectionId} not found in usage tracking");
            }

            // Update with final stats if provided
            if (finalStats != null)
            {
                session.EstimatedCost = finalStats.EstimatedCost;
                session.LastActivity = DateTime.UtcNow;
            }

            var totalCost = await GetEstimatedCostAsync(connectionId);

            // Record usage with virtual key service by updating spend
            if (totalCost > 0)
            {
                await _virtualKeyService.UpdateSpendAsync(session.VirtualKeyId, totalCost);
            }

            var duration = DateTime.UtcNow - session.StartTime;
            _logger.LogInformation("Finalized session {ConnectionId}: Duration={Duration}s, Cost=${Cost:F4}",
                connectionId,
                duration.TotalSeconds,
                totalCost);

            return totalCost;
        }

        public async Task<RealtimeUsageDetails?> GetUsageDetailsAsync(string connectionId)
        {
            if (!_sessions.TryGetValue(connectionId, out var session))
            {
                return null;
            }

            var duration = DateTime.UtcNow - session.StartTime;
            var totalCost = await GetEstimatedCostAsync(connectionId);

            // Get cost configuration for breakdown
            var modelCost = await _costService.GetCostForModelAsync(session.Model);

            var details = new RealtimeUsageDetails
            {
                ConnectionId = connectionId,
                InputAudioSeconds = session.InputAudioSeconds,
                OutputAudioSeconds = session.OutputAudioSeconds,
                InputTokens = (int)session.InputTokens,
                OutputTokens = (int)session.OutputTokens,
                FunctionCalls = session.FunctionCalls,
                SessionDurationSeconds = duration.TotalSeconds,
                StartedAt = session.StartTime,
                EndedAt = null // Still active
            };

            if (modelCost != null)
            {
                // Calculate cost breakdown
                details.Costs = new CostBreakdown
                {
                    InputAudioCost = (decimal)(session.InputAudioSeconds / 60.0 * 1000) * modelCost.InputCostPerMillionTokens / 1_000_000m,
                    OutputAudioCost = (decimal)(session.OutputAudioSeconds / 60.0 * 1000) * modelCost.OutputCostPerMillionTokens / 1_000_000m,
                    TokenCost = (session.InputTokens * modelCost.InputCostPerMillionTokens / 1_000_000m) +
                                (session.OutputTokens * modelCost.OutputCostPerMillionTokens / 1_000_000m),
                    FunctionCallCost = session.FunctionCalls > 0 
                        ? (session.FunctionCalls * 100m * modelCost.OutputCostPerMillionTokens) / 1_000_000m
                        : 0,
                    AdditionalFees = 0
                };
            }

            return details;
        }

        private class SessionUsage
        {
            public string ConnectionId { get; set; } = string.Empty;
            public int VirtualKeyId { get; set; }
            public string VirtualKey { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public DateTime LastActivity { get; set; }
            public double InputAudioSeconds { get; set; }
            public double OutputAudioSeconds { get; set; }
            public long InputTokens { get; set; }
            public long OutputTokens { get; set; }
            public int FunctionCalls { get; set; }
            public decimal EstimatedCost { get; set; }
        }
    }
}
