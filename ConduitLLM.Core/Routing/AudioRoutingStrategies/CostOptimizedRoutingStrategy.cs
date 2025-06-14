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
    /// Routes audio requests to minimize cost while maintaining quality thresholds.
    /// </summary>
    public class CostOptimizedRoutingStrategy : IAudioRoutingStrategy
    {
        private readonly ILogger<CostOptimizedRoutingStrategy> _logger;
        private readonly double _defaultQualityThreshold;

        /// <inheritdoc />
        public string Name => "CostOptimized";

        /// <summary>
        /// Initializes a new instance of the <see cref="CostOptimizedRoutingStrategy"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="defaultQualityThreshold">Default minimum quality score (0-100).</param>
        public CostOptimizedRoutingStrategy(
            ILogger<CostOptimizedRoutingStrategy> logger,
            double defaultQualityThreshold = 70)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultQualityThreshold = defaultQualityThreshold;
        }

        /// <inheritdoc />
        public Task<string?> SelectTranscriptionProviderAsync(
            AudioTranscriptionRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default)
        {
            var qualityThreshold = request.RequiredQuality ?? _defaultQualityThreshold;
            var audioFormat = request.AudioFormat ?? AudioFormat.Mp3;
            var estimatedDuration = EstimateAudioDuration(request.AudioData?.Length ?? 0, audioFormat);

            return SelectProviderByCostAsync(
                availableProviders,
                p => CalculateTranscriptionCost(p, estimatedDuration),
                qualityThreshold,
                p => p.Capabilities.SupportsStreaming || !request.EnableStreaming,
                p => SupportsLanguage(p, request.Language),
                p => SupportsFormat(p, request.AudioFormat?.ToString()));
        }

        /// <inheritdoc />
        public Task<string?> SelectTextToSpeechProviderAsync(
            TextToSpeechRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default)
        {
            var qualityThreshold = _defaultQualityThreshold;
            var characterCount = request.Input.Length;

            return SelectProviderByCostAsync(
                availableProviders,
                p => CalculateTTSCost(p, characterCount),
                qualityThreshold,
                p => p.Capabilities.SupportedVoices.Contains(request.Voice) ||
                     p.Capabilities.SupportedVoices.Count == 0,
                p => SupportsLanguage(p, request.Language),
                p => SupportsFormat(p, request.ResponseFormat?.ToString()));
        }

        /// <inheritdoc />
        public Task UpdateMetricsAsync(
            string provider,
            AudioRequestMetrics metrics,
            CancellationToken cancellationToken = default)
        {
            // Log cost efficiency
            if (metrics.Success)
            {
                var costEfficiency = CalculateCostEfficiency(provider, metrics);
                _logger.LogDebug(
                    "Provider {Provider} cost efficiency: ${Cost:F4} per unit",
                    provider,
                    costEfficiency);
            }

            return Task.CompletedTask;
        }

        private Task<string?> SelectProviderByCostAsync(
            IReadOnlyList<AudioProviderInfo> availableProviders,
            Func<AudioProviderInfo, decimal> costCalculator,
            double qualityThreshold,
            params Func<AudioProviderInfo, bool>[] filters)
        {
            // Filter by availability, quality threshold, and other criteria
            var eligibleProviders = availableProviders
                .Where(p => p.IsAvailable &&
                           p.Capabilities.QualityScore >= qualityThreshold &&
                           filters.All(f => f(p)))
                .ToList();

            if (!eligibleProviders.Any())
            {
                _logger.LogWarning(
                    "No eligible providers found with quality >= {Quality}",
                    qualityThreshold);
                return Task.FromResult<string?>(null);
            }

            // Calculate effective cost (considering success rate and potential retries)
            var costedProviders = eligibleProviders
                .Select(p => new
                {
                    Provider = p,
                    BaseCost = costCalculator(p),
                    EffectiveCost = CalculateEffectiveCost(costCalculator(p), p.Metrics.SuccessRate),
                    QualityAdjustedCost = CalculateQualityAdjustedCost(
                        costCalculator(p),
                        p.Metrics.SuccessRate,
                        p.Capabilities.QualityScore)
                })
                .OrderBy(x => x.QualityAdjustedCost)
                .ToList();

            var selected = costedProviders.First();

            _logger.LogInformation(
                "Selected {Provider} with cost ${Cost:F4} (effective: ${Effective:F4}, quality-adjusted: ${QualityAdjusted:F4})",
                selected.Provider.Name,
                selected.BaseCost,
                selected.EffectiveCost,
                selected.QualityAdjustedCost);

            return Task.FromResult<string?>(selected.Provider.Name);
        }

        private decimal CalculateTranscriptionCost(AudioProviderInfo provider, double durationMinutes)
        {
            return provider.Costs.TranscriptionPerMinute * (decimal)durationMinutes;
        }

        private decimal CalculateTTSCost(AudioProviderInfo provider, int characterCount)
        {
            return provider.Costs.TextToSpeechPer1kChars * (characterCount / 1000m);
        }

        private decimal CalculateEffectiveCost(decimal baseCost, double successRate)
        {
            // Account for retries due to failures
            if (successRate <= 0) return baseCost * 10; // Penalize heavily

            var expectedAttempts = 1.0 / successRate;
            return baseCost * (decimal)expectedAttempts;
        }

        private decimal CalculateQualityAdjustedCost(decimal baseCost, double successRate, double qualityScore)
        {
            // Lower quality should be reflected as higher "true" cost
            var qualityMultiplier = 2.0 - (qualityScore / 100.0); // 1.0 to 2.0
            var effectiveCost = CalculateEffectiveCost(baseCost, successRate);

            return effectiveCost * (decimal)qualityMultiplier;
        }

        private double EstimateAudioDuration(int audioDataLength, AudioFormat format)
        {
            // Rough estimates based on typical bitrates
            var bytesPerSecond = format switch
            {
                AudioFormat.Mp3 => 16000,     // 128 kbps
                AudioFormat.Wav => 176400,    // 1411 kbps (CD quality)
                AudioFormat.Flac => 88200,    // ~700 kbps
                AudioFormat.Ogg => 12000,     // 96 kbps
                AudioFormat.Opus => 6000,     // 48 kbps
                _ => 16000 // Default to MP3 estimate
            };

            var durationSeconds = audioDataLength / (double)bytesPerSecond;
            return durationSeconds / 60.0; // Convert to minutes
        }

        private double CalculateCostEfficiency(string provider, AudioRequestMetrics metrics)
        {
            // This would calculate actual cost based on the request
            // For now, return a placeholder
            return 0.01;
        }

        private bool SupportsLanguage(AudioProviderInfo provider, string? language)
        {
            if (string.IsNullOrEmpty(language))
                return true;

            return provider.Capabilities.SupportedLanguages.Count == 0 ||
                   provider.Capabilities.SupportedLanguages.Contains(language);
        }

        private bool SupportsFormat(AudioProviderInfo provider, string? format)
        {
            if (string.IsNullOrEmpty(format))
                return true;

            return provider.Capabilities.SupportedFormats.Count == 0 ||
                   provider.Capabilities.SupportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
        }
    }
}
