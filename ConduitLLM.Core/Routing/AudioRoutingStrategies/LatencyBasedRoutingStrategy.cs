using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing.AudioRoutingStrategies
{
    /// <summary>
    /// Routes audio requests based on latency, selecting the fastest available provider.
    /// </summary>
    public class LatencyBasedRoutingStrategy : IAudioRoutingStrategy
    {
        private readonly ILogger<LatencyBasedRoutingStrategy> _logger;
        private readonly Dictionary<string, Queue<double>> _latencyHistory = new();
        private readonly int _maxHistorySize;

        /// <inheritdoc />
        public string Name => "LatencyBased";

        /// <summary>
        /// Initializes a new instance of the <see cref="LatencyBasedRoutingStrategy"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="maxHistorySize">Maximum number of latency samples to keep per provider.</param>
        public LatencyBasedRoutingStrategy(
            ILogger<LatencyBasedRoutingStrategy> logger,
            int maxHistorySize = 100)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxHistorySize = maxHistorySize;
        }

        /// <inheritdoc />
        public Task<string?> SelectTranscriptionProviderAsync(
            AudioTranscriptionRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default)
        {
            return SelectProviderByLatencyAsync(
                availableProviders,
                p => p.Capabilities.SupportsStreaming || !request.EnableStreaming,
                p => SupportsLanguage(p, request.Language),
                p => SupportsFormat(p, request.AudioFormat.ToString()));
        }

        /// <inheritdoc />
        public Task<string?> SelectTextToSpeechProviderAsync(
            TextToSpeechRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default)
        {
            return SelectProviderByLatencyAsync(
                availableProviders,
                p => p.Capabilities.SupportedVoices.Contains(request.Voice) ||
                     p.Capabilities.SupportedVoices.Count() == 0, // Empty means all voices supported
                p => SupportsLanguage(p, request.Language),
                p => SupportsFormat(p, request.ResponseFormat?.ToString()));
        }

        /// <inheritdoc />
        public Task UpdateMetricsAsync(
            string provider,
            AudioRequestMetrics metrics,
            CancellationToken cancellationToken = default)
        {
            if (!_latencyHistory.ContainsKey(provider))
            {
                _latencyHistory[provider] = new Queue<double>();
            }

            var history = _latencyHistory[provider];
            history.Enqueue(metrics.LatencyMs);

            // Keep only recent samples
            while (history.Count() > _maxHistorySize)
            {
                history.Dequeue();
            }

            _logger.LogDebug(
                "Updated latency metrics for {Provider}: {Latency}ms (avg: {Average}ms)",
                provider,
                metrics.LatencyMs,
                history.Average());

            return Task.CompletedTask;
        }

        private Task<string?> SelectProviderByLatencyAsync(
            IReadOnlyList<AudioProviderInfo> availableProviders,
            params Func<AudioProviderInfo, bool>[] filters)
        {
            // Filter providers
            var eligibleProviders = availableProviders
                .Where(p => p.IsAvailable && filters.All(f => f(p)))
                .ToList();

            if (eligibleProviders.Count() == 0)
            {
                _logger.LogWarning("No eligible providers found for latency-based routing");
                return Task.FromResult<string?>(null);
            }

            // Sort by latency (using both current metrics and historical data)
            var sortedProviders = eligibleProviders
                .Select(p => new
                {
                    Provider = p,
                    EffectiveLatency = CalculateEffectiveLatency(p)
                })
                .OrderBy(x => x.EffectiveLatency)
                .ToList();

            var selected = sortedProviders.First();

            _logger.LogInformation(
                "Selected {Provider} with effective latency {Latency}ms",
                selected.Provider.Name,
                selected.EffectiveLatency);

            return Task.FromResult<string?>(selected.Provider.Name);
        }

        private double CalculateEffectiveLatency(AudioProviderInfo provider)
        {
            // Use current metrics as base
            var baseLatency = provider.Metrics.AverageLatencyMs;

            // Adjust based on historical data if available
            if (_latencyHistory.TryGetValue(provider.Name, out var history) && history.Count() > 0)
            {
                // Weight recent history more heavily
                var historicalAvg = history.Average();
                baseLatency = (baseLatency * 0.3) + (historicalAvg * 0.7);
            }

            // Penalize based on load and success rate
            var loadPenalty = provider.Metrics.CurrentLoad * 100; // Up to 100ms penalty
            var successPenalty = (1 - provider.Metrics.SuccessRate) * 200; // Up to 200ms penalty

            return baseLatency + loadPenalty + successPenalty;
        }

        private bool SupportsLanguage(AudioProviderInfo provider, string? language)
        {
            if (string.IsNullOrEmpty(language))
                return true;

            return provider.Capabilities.SupportedLanguages.Count() == 0 || // Empty means all languages
                   provider.Capabilities.SupportedLanguages.Contains(language);
        }

        private bool SupportsFormat(AudioProviderInfo provider, string? format)
        {
            if (string.IsNullOrEmpty(format))
                return true;

            return provider.Capabilities.SupportedFormats.Count() == 0 || // Empty means all formats
                   provider.Capabilities.SupportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
        }
    }
}
