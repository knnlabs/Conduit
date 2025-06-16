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
    /// Routes audio requests based on language expertise and quality scores.
    /// </summary>
    public class LanguageOptimizedRoutingStrategy : IAudioRoutingStrategy
    {
        private readonly ILogger<LanguageOptimizedRoutingStrategy> _logger;
        private readonly Dictionary<string, Dictionary<string, double>> _languageQualityScores;

        /// <inheritdoc />
        public string Name => "LanguageOptimized";

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageOptimizedRoutingStrategy"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public LanguageOptimizedRoutingStrategy(ILogger<LanguageOptimizedRoutingStrategy> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _languageQualityScores = InitializeLanguageScores();
        }

        /// <inheritdoc />
        public Task<string?> SelectTranscriptionProviderAsync(
            AudioTranscriptionRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default)
        {
            var language = request.Language ?? DetectLanguageFromRequest(request);

            return SelectProviderByLanguageAsync(
                language,
                availableProviders,
                p => p.Capabilities.SupportsStreaming || !request.EnableStreaming,
                p => SupportsFormat(p, request.AudioFormat.ToString()));
        }

        /// <inheritdoc />
        public Task<string?> SelectTextToSpeechProviderAsync(
            TextToSpeechRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default)
        {
            var language = request.Language ?? "en";

            return SelectProviderByLanguageAsync(
                language,
                availableProviders,
                p => p.Capabilities.SupportedVoices.Contains(request.Voice) ||
                     p.Capabilities.SupportedVoices.Count == 0,
                p => SupportsFormat(p, request.ResponseFormat?.ToString()));
        }

        /// <inheritdoc />
        public Task UpdateMetricsAsync(
            string provider,
            AudioRequestMetrics metrics,
            CancellationToken cancellationToken = default)
        {
            // Update language-specific quality scores based on success rates
            if (!string.IsNullOrEmpty(metrics.Language) && metrics.Success)
            {
                if (!_languageQualityScores.ContainsKey(provider))
                {
                    _languageQualityScores[provider] = new Dictionary<string, double>();
                }

                var currentScore = _languageQualityScores[provider].GetValueOrDefault(metrics.Language, 0.7);
                // Exponential moving average
                _languageQualityScores[provider][metrics.Language] = (currentScore * 0.9) + (metrics.Success ? 0.1 : 0);
            }

            return Task.CompletedTask;
        }

        private Task<string?> SelectProviderByLanguageAsync(
            string language,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            params Func<AudioProviderInfo, bool>[] filters)
        {
            // Filter available providers
            var eligibleProviders = availableProviders
                .Where(p => p.IsAvailable && filters.All(f => f(p)))
                .Where(p => SupportsLanguage(p, language))
                .ToList();

            if (!eligibleProviders.Any())
            {
_logger.LogWarning("No eligible providers found for language {Language}", language.Replace(Environment.NewLine, ""));
                return Task.FromResult<string?>(null);
            }

            // Score providers based on language expertise
            var scoredProviders = eligibleProviders
                .Select(p => new
                {
                    Provider = p,
                    Score = CalculateLanguageScore(p, language)
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            var selected = scoredProviders.First();

            _logger.LogInformation(
                "Selected {Provider} for language {Language} with score {Score:F2}",
                selected.Provider.Name,
                language.Replace(Environment.NewLine, ""),
                selected.Score);

            return Task.FromResult<string?>(selected.Provider.Name);
        }

        private double CalculateLanguageScore(AudioProviderInfo provider, string language)
        {
            var baseScore = provider.Capabilities.QualityScore / 100.0;

            // Check predefined language expertise
            var languageScore = GetPredefinedLanguageScore(provider.Name, language);

            // Check historical performance
            if (_languageQualityScores.TryGetValue(provider.Name, out var scores) &&
                scores.TryGetValue(language, out var historicalScore))
            {
                languageScore = (languageScore * 0.4) + (historicalScore * 0.6);
            }

            // Factor in current metrics
            var performanceScore = provider.Metrics.SuccessRate * (1 - (provider.Metrics.AverageLatencyMs / 5000.0));

            return (baseScore * 0.3) + (languageScore * 0.5) + (performanceScore * 0.2);
        }

        private double GetPredefinedLanguageScore(string provider, string language)
        {
            var languageFamily = GetLanguageFamily(language);

            return (provider.ToLower(), languageFamily) switch
            {
                // Google excels at Asian languages
                ("google", "asian") => 0.95,
                ("google", "european") => 0.85,
                ("google", "english") => 0.90,

                // Azure strong in European languages
                ("azure", "european") => 0.95,
                ("azure", "english") => 0.90,
                ("azure", "asian") => 0.80,

                // OpenAI/Whisper very good at English
                ("openai", "english") => 0.95,
                ("openai", "european") => 0.85,
                ("openai", "asian") => 0.75,

                // Deepgram excellent for English real-time
                ("deepgram", "english") => 0.98,
                ("deepgram", "european") => 0.80,
                ("deepgram", "asian") => 0.70,

                // Default scores
                (_, "english") => 0.80,
                (_, _) => 0.70
            };
        }

        private string GetLanguageFamily(string language)
        {
            return language switch
            {
                "en" or "en-US" or "en-GB" => "english",
                "zh" or "ja" or "ko" or "th" or "vi" => "asian",
                "es" or "fr" or "de" or "it" or "pt" or "ru" or "pl" => "european",
                _ => "other"
            };
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

        private string DetectLanguageFromRequest(AudioTranscriptionRequest request)
        {
            // In a real implementation, we might:
            // 1. Use a language detection service on a sample of the audio
            // 2. Check metadata
            // 3. Use user preferences
            // For now, default to English
            return "en";
        }

        private Dictionary<string, Dictionary<string, double>> InitializeLanguageScores()
        {
            // Initialize with some baseline scores
            return new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
