using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing.AudioRoutingStrategies
{
    /// <summary>
    /// Routes audio requests to maximize quality, regardless of cost or latency.
    /// </summary>
    public class QualityBasedRoutingStrategy : IAudioRoutingStrategy
    {
        private readonly ILogger<QualityBasedRoutingStrategy> _logger;
        private readonly Dictionary<string, QualityMetrics> _providerQualityMetrics = new();

        /// <inheritdoc />
        public string Name => "QualityBased";

        /// <summary>
        /// Initializes a new instance of the <see cref="QualityBasedRoutingStrategy"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public QualityBasedRoutingStrategy(ILogger<QualityBasedRoutingStrategy> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<string?> SelectTranscriptionProviderAsync(
            AudioTranscriptionRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default)
        {
            return SelectProviderByQualityAsync(
                availableProviders,
                AudioRequestType.Transcription,
                p => p.Capabilities.SupportsStreaming || !request.EnableStreaming,
                p => SupportsLanguage(p, request.Language),
                p => SupportsFormat(p, request.AudioFormat?.ToString()),
                p => HasAdequateDuration(p, request.AudioData?.Length ?? 0, request.AudioFormat ?? AudioFormat.Mp3));
        }

        /// <inheritdoc />
        public Task<string?> SelectTextToSpeechProviderAsync(
            TextToSpeechRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default)
        {
            // For TTS, voice quality is paramount
            return SelectProviderByQualityAsync(
                availableProviders,
                AudioRequestType.TextToSpeech,
                p => p.Capabilities.SupportedVoices.Contains(request.Voice) ||
                     p.Capabilities.SupportedVoices.Count() == 0,
                p => SupportsLanguage(p, request.Language),
                p => SupportsFormat(p, request.ResponseFormat?.ToString()),
                p => SupportsAdvancedFeatures(p, request));
        }

        /// <inheritdoc />
        public Task UpdateMetricsAsync(
            string provider,
            AudioRequestMetrics metrics,
            CancellationToken cancellationToken = default)
        {
            if (!_providerQualityMetrics.ContainsKey(provider))
            {
                _providerQualityMetrics[provider] = new QualityMetrics();
            }

            var qualityMetrics = _providerQualityMetrics[provider];

            // Update quality metrics based on success and user feedback
            if (metrics.Success)
            {
                qualityMetrics.SuccessfulRequests++;
                qualityMetrics.UpdateAverageConfidence(0.85); // Default confidence
            }
            else
            {
                qualityMetrics.FailedRequests++;
            }

            qualityMetrics.LastUpdated = DateTime.UtcNow;

            return Task.CompletedTask;
        }

        private Task<string?> SelectProviderByQualityAsync(
            IReadOnlyList<AudioProviderInfo> availableProviders,
            AudioRequestType requestType,
            params Func<AudioProviderInfo, bool>[] filters)
        {
            // Filter available providers
            var eligibleProviders = availableProviders
                .Where(p => p.IsAvailable && filters.All(f => f(p)))
                .ToList();

            if (eligibleProviders.Count() == 0)
            {
                _logger.LogWarning("No eligible providers found for quality-based routing");
                return Task.FromResult<string?>(null);
            }

            // Calculate comprehensive quality score
            var scoredProviders = eligibleProviders
                .Select(p => new
                {
                    Provider = p,
                    QualityScore = CalculateComprehensiveQualityScore(p, requestType)
                })
                .OrderByDescending(x => x.QualityScore)
                .ToList();

            var selected = scoredProviders.First();

            _logger.LogInformation(
                "Selected {Provider} with quality score {Score:F2} for {RequestType}",
                selected.Provider.Name,
                selected.QualityScore,
                requestType);

            // Log why this provider was chosen
            LogQualityFactors(selected.Provider, requestType);

            return Task.FromResult<string?>(selected.Provider.Name);
        }

        private double CalculateComprehensiveQualityScore(AudioProviderInfo provider, AudioRequestType requestType)
        {
            var baseQuality = provider.Capabilities.QualityScore / 100.0;
            var successRate = provider.Metrics.SuccessRate;

            // Get historical quality metrics
            var historicalQuality = 0.8; // Default
            if (_providerQualityMetrics.TryGetValue(provider.Name, out var metrics))
            {
                historicalQuality = metrics.GetQualityScore();
            }

            // Request-type specific adjustments
            var typeMultiplier = requestType switch
            {
                AudioRequestType.Transcription => CalculateTranscriptionQualityMultiplier(provider),
                AudioRequestType.TextToSpeech => CalculateTTSQualityMultiplier(provider),
                AudioRequestType.Realtime => CalculateRealtimeQualityMultiplier(provider),
                _ => 1.0
            };

            // Feature richness bonus
            var featureBonus = CalculateFeatureBonus(provider, requestType);

            // Combine all factors
            var finalScore = (baseQuality * 0.3) +
                           (successRate * 0.2) +
                           (historicalQuality * 0.2) +
                           (typeMultiplier * 0.2) +
                           (featureBonus * 0.1);

            return Math.Min(1.0, finalScore);
        }

        private double CalculateTranscriptionQualityMultiplier(AudioProviderInfo provider)
        {
            var multiplier = 1.0;

            // Bonus for supporting custom vocabulary
            if (provider.Capabilities.SupportsCustomVocabulary)
                multiplier += 0.1;

            // Bonus for real-time capability
            if (provider.Capabilities.SupportsRealtime)
                multiplier += 0.1;

            // Bonus for many supported languages
            if (provider.Capabilities.SupportedLanguages.Count() > 50)
                multiplier += 0.1;

            return Math.Min(1.3, multiplier);
        }

        private double CalculateTTSQualityMultiplier(AudioProviderInfo provider)
        {
            var multiplier = 1.0;

            // Bonus for many voice options
            var voiceCount = provider.Capabilities.SupportedVoices.Count();
            if (voiceCount > 100)
                multiplier += 0.2;
            else if (voiceCount > 50)
                multiplier += 0.1;

            // No arbitrary provider bonuses - quality should be based on actual metrics

            return Math.Min(1.4, multiplier);
        }

        private double CalculateRealtimeQualityMultiplier(AudioProviderInfo provider)
        {
            if (!provider.Capabilities.SupportsRealtime)
                return 0.5; // Heavy penalty

            var multiplier = 1.0;

            // Bonus for low latency
            if (provider.Metrics.AverageLatencyMs < 100)
                multiplier += 0.2;
            else if (provider.Metrics.AverageLatencyMs < 200)
                multiplier += 0.1;

            return multiplier;
        }

        private double CalculateFeatureBonus(AudioProviderInfo provider, AudioRequestType requestType)
        {
            var bonus = 0.0;

            // Format support
            if (provider.Capabilities.SupportedFormats.Count() > 5)
                bonus += 0.1;

            // Streaming support
            if (provider.Capabilities.SupportsStreaming)
                bonus += 0.1;

            // Low error rate in recent history
            if (_providerQualityMetrics.TryGetValue(provider.Name, out var metrics))
            {
                var errorRate = metrics.GetErrorRate();
                if (errorRate < 0.01) // Less than 1% errors
                    bonus += 0.2;
                else if (errorRate < 0.05) // Less than 5% errors
                    bonus += 0.1;
            }

            return bonus;
        }

        private void LogQualityFactors(AudioProviderInfo provider, AudioRequestType requestType)
        {
            _logger.LogDebug(
                "Quality factors for {Provider}: Base={Base:F2}, Success={Success:F2}, Features={Features}",
                provider.Name,
                provider.Capabilities.QualityScore,
                provider.Metrics.SuccessRate,
                string.Join(", ", GetProviderFeatures(provider)));
        }

        private List<string> GetProviderFeatures(AudioProviderInfo provider)
        {
            var features = new List<string>();

            if (provider.Capabilities.SupportsStreaming)
                features.Add("Streaming");
            if (provider.Capabilities.SupportsRealtime)
                features.Add("Realtime");
            if (provider.Capabilities.SupportsCustomVocabulary)
                features.Add("CustomVocab");
            if (provider.Capabilities.SupportedLanguages.Count() > 30)
                features.Add($"{provider.Capabilities.SupportedLanguages.Count()} Languages");
            if (provider.Capabilities.SupportedVoices.Count() > 20)
                features.Add($"{provider.Capabilities.SupportedVoices.Count()} Voices");

            return features;
        }

        private bool SupportsLanguage(AudioProviderInfo provider, string? language)
        {
            if (string.IsNullOrEmpty(language))
                return true;

            return provider.Capabilities.SupportedLanguages.Count() == 0 ||
                   provider.Capabilities.SupportedLanguages.Contains(language);
        }

        private bool SupportsFormat(AudioProviderInfo provider, string? format)
        {
            if (string.IsNullOrEmpty(format))
                return true;

            return provider.Capabilities.SupportedFormats.Count() == 0 ||
                   provider.Capabilities.SupportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
        }

        private bool HasAdequateDuration(AudioProviderInfo provider, int audioDataLength, AudioFormat format)
        {
            // Estimate duration and check against provider limits
            var estimatedSeconds = EstimateAudioDuration(audioDataLength, format) * 60;
            return estimatedSeconds <= provider.Capabilities.MaxAudioDurationSeconds;
        }

        private bool SupportsAdvancedFeatures(AudioProviderInfo provider, TextToSpeechRequest request)
        {
            // Check if provider supports requested advanced features
            // TODO: These capabilities should come from the database ModelCapabilities
            // For now, assume all providers can handle the request unless proven otherwise
            // through actual capability checks from the database

            return true;
        }

        private double EstimateAudioDuration(int audioDataLength, AudioFormat format)
        {
            var bytesPerSecond = format switch
            {
                AudioFormat.Mp3 => 16000,
                AudioFormat.Wav => 176400,
                AudioFormat.Flac => 88200,
                AudioFormat.Ogg => 12000,
                AudioFormat.Opus => 6000,
                _ => 16000
            };

            return audioDataLength / (double)bytesPerSecond / 60.0; // Minutes
        }

        private class QualityMetrics
        {
            public int SuccessfulRequests { get; set; }
            public int FailedRequests { get; set; }
            public double AverageConfidence { get; private set; } = 0.8;
            public DateTime LastUpdated { get; set; }

            public void UpdateAverageConfidence(double newConfidence)
            {
                // Exponential moving average
                AverageConfidence = (AverageConfidence * 0.9) + (newConfidence * 0.1);
            }

            public double GetQualityScore()
            {
                var total = SuccessfulRequests + FailedRequests;
                if (total == 0) return 0.8; // Default

                var successRate = SuccessfulRequests / (double)total;
                return (successRate * 0.7) + (AverageConfidence * 0.3);
            }

            public double GetErrorRate()
            {
                var total = SuccessfulRequests + FailedRequests;
                if (total == 0) return 0;
                return FailedRequests / (double)total;
            }
        }
    }
}
