using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Routing.AudioRoutingStrategies;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Advanced audio router with support for multiple routing strategies.
    /// </summary>
    public class AdvancedAudioRouter : IAdvancedAudioRouter
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IModelCapabilityService _capabilityService;
        private readonly ILogger<AdvancedAudioRouter> _logger;
        private readonly Dictionary<string, IAudioRoutingStrategy> _strategies;
        private readonly ConcurrentDictionary<string, AudioProviderInfo> _providerInfoCache = new();
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private DateTime _lastRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        private IAudioRoutingStrategy _currentStrategy;

        /// <inheritdoc />
        public IReadOnlyList<IAudioRoutingStrategy> RoutingStrategies => _strategies.Values.ToList();

        /// <inheritdoc />
        public string CurrentStrategyName => _currentStrategy.Name;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdvancedAudioRouter"/> class.
        /// </summary>
        public AdvancedAudioRouter(
            ILLMClientFactory clientFactory,
            IModelCapabilityService capabilityService,
            ILogger<AdvancedAudioRouter> logger,
            ILoggerFactory loggerFactory,
            IOptions<RouterOptions> options)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _capabilityService = capabilityService ?? throw new ArgumentNullException(nameof(capabilityService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize routing strategies
            _strategies = new Dictionary<string, IAudioRoutingStrategy>
            {
                ["LatencyBased"] = new LatencyBasedRoutingStrategy(
                    loggerFactory.CreateLogger<LatencyBasedRoutingStrategy>()),
                ["LanguageOptimized"] = new LanguageOptimizedRoutingStrategy(
                    loggerFactory.CreateLogger<LanguageOptimizedRoutingStrategy>()),
                ["CostOptimized"] = new CostOptimizedRoutingStrategy(
                    loggerFactory.CreateLogger<CostOptimizedRoutingStrategy>()),
                ["QualityBased"] = new QualityBasedRoutingStrategy(
                    loggerFactory.CreateLogger<QualityBasedRoutingStrategy>())
            };

            // Set default strategy
            var defaultStrategy = options.Value.DefaultAudioRoutingStrategy ?? "LatencyBased";
            _currentStrategy = _strategies.GetValueOrDefault(defaultStrategy) ?? _strategies["LatencyBased"];

            _logger.LogInformation("Initialized AdvancedAudioRouter with strategy: {Strategy}", _currentStrategy.Name);
        }

        /// <inheritdoc />
        public void SetRoutingStrategy(string strategyName)
        {
            if (_strategies.TryGetValue(strategyName, out var strategy))
            {
                _currentStrategy = strategy;
                _logger.LogInformation("Changed routing strategy to: {Strategy}", strategyName);
            }
            else
            {
                _logger.LogWarning("Unknown routing strategy: {Strategy}", strategyName);
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AudioProviderInfo>> GetProviderInfoAsync(
            CancellationToken cancellationToken = default)
        {
            await RefreshProviderInfoIfNeededAsync(cancellationToken);
            return _providerInfoCache.Values.ToList();
        }

        /// <inheritdoc />
        public async Task RefreshProviderInfoAsync(CancellationToken cancellationToken = default)
        {
            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Refreshing provider information");

                // For now, hardcode known audio providers
                // In a real implementation, this would query the database
                var audioProviders = new List<string>
                {
                    "OpenAI",
                    "ElevenLabs",
                    "Google",
                    "Azure",
                    "Deepgram"
                };

                foreach (var provider in audioProviders)
                {
                    var info = await BuildProviderInfoAsync(provider, cancellationToken);
                    _providerInfoCache[provider] = info;
                }

                _lastRefresh = DateTime.UtcNow;
                _logger.LogInformation("Refreshed info for {Count} audio providers", _providerInfoCache.Count);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            AudioTranscriptionRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var providerInfo = await GetProviderInfoAsync(cancellationToken);
            var providerName = await _currentStrategy.SelectTranscriptionProviderAsync(
                request,
                providerInfo,
                cancellationToken);

            if (string.IsNullOrEmpty(providerName))
            {
                _logger.LogWarning("No suitable transcription provider found");
                return null;
            }

            var client = _clientFactory.GetClientByProvider(providerName);
            return client as IAudioTranscriptionClient;
        }

        /// <inheritdoc />
        public async Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            TextToSpeechRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var providerInfo = await GetProviderInfoAsync(cancellationToken);
            var providerName = await _currentStrategy.SelectTextToSpeechProviderAsync(
                request,
                providerInfo,
                cancellationToken);

            if (string.IsNullOrEmpty(providerName))
            {
                _logger.LogWarning("No suitable TTS provider found");
                return null;
            }

            var client = _clientFactory.GetClientByProvider(providerName);
            return client as ITextToSpeechClient;
        }

        /// <inheritdoc />
        public async Task<IRealtimeAudioClient?> GetRealtimeClientAsync(
            RealtimeSessionConfig config,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            await RefreshProviderInfoIfNeededAsync(cancellationToken);

            var eligibleProviders = _providerInfoCache.Values
                .Where(p => p.IsAvailable && p.Capabilities.SupportsRealtime)
                .OrderBy(p => p.Metrics.AverageLatencyMs)
                .ToList();

            foreach (var provider in eligibleProviders)
            {
                var client = _clientFactory.GetClientByProvider(provider.Name);
                if (client is IRealtimeAudioClient realtimeClient)
                {
                    _logger.LogInformation("Selected {Provider} for real-time audio", provider.Name);
                    return realtimeClient;
                }
            }

            _logger.LogWarning("No real-time audio provider available");
            return null;
        }

        /// <inheritdoc />
        public async Task<string?> GetBestProviderForLanguageAsync(
            string language,
            AudioRequestType requestType,
            CancellationToken cancellationToken = default)
        {
            // Temporarily switch to language-optimized strategy
            var originalStrategy = _currentStrategy;
            _currentStrategy = _strategies["LanguageOptimized"];

            try
            {
                var providerInfo = await GetProviderInfoAsync(cancellationToken);

                return requestType switch
                {
                    AudioRequestType.Transcription => await _currentStrategy.SelectTranscriptionProviderAsync(
                        new AudioTranscriptionRequest { Language = language, AudioData = Array.Empty<byte>(), AudioFormat = AudioFormat.Mp3 },
                        providerInfo,
                        cancellationToken),
                    AudioRequestType.TextToSpeech => await _currentStrategy.SelectTextToSpeechProviderAsync(
                        new TextToSpeechRequest { Language = language, Voice = "default", Input = "" },
                        providerInfo,
                        cancellationToken),
                    _ => null
                };
            }
            finally
            {
                _currentStrategy = originalStrategy;
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetLowestLatencyProviderAsync(
            double maxLatencyMs,
            AudioRequestType requestType,
            CancellationToken cancellationToken = default)
        {
            var providerInfo = await GetProviderInfoAsync(cancellationToken);

            var eligibleProviders = providerInfo
                .Where(p => p.IsAvailable && p.Metrics.AverageLatencyMs <= maxLatencyMs)
                .OrderBy(p => p.Metrics.AverageLatencyMs)
                .FirstOrDefault();

            return eligibleProviders?.Name;
        }

        /// <inheritdoc />
        public async Task<string?> GetMostCostEffectiveProviderAsync(
            AudioRequestType requestType,
            double minQualityScore = 70,
            CancellationToken cancellationToken = default)
        {
            // Temporarily switch to cost-optimized strategy
            var originalStrategy = _currentStrategy;
            _currentStrategy = _strategies["CostOptimized"];

            try
            {
                var providerInfo = await GetProviderInfoAsync(cancellationToken);

                return requestType switch
                {
                    AudioRequestType.Transcription => await _currentStrategy.SelectTranscriptionProviderAsync(
                        new AudioTranscriptionRequest { AudioData = Array.Empty<byte>(), AudioFormat = AudioFormat.Mp3 },
                        providerInfo,
                        cancellationToken),
                    AudioRequestType.TextToSpeech => await _currentStrategy.SelectTextToSpeechProviderAsync(
                        new TextToSpeechRequest { Voice = "default", Input = "" },
                        providerInfo,
                        cancellationToken),
                    _ => null
                };
            }
            finally
            {
                _currentStrategy = originalStrategy;
            }
        }

        /// <inheritdoc />
        public async Task ReportProviderFailureAsync(
            string provider,
            string errorCode,
            CancellationToken cancellationToken = default)
        {
            if (_providerInfoCache.TryGetValue(provider, out var info))
            {
                // Update metrics to reflect failure
                info.Metrics.SuccessRate = Math.Max(0, info.Metrics.SuccessRate - 0.01);
                info.Metrics.LastUpdated = DateTime.UtcNow;

                // Report to current strategy
                var metrics = new AudioRequestMetrics
                {
                    Success = false,
                    ErrorCode = errorCode,
                    LatencyMs = 0,
                    Timestamp = DateTime.UtcNow
                };

                await _currentStrategy.UpdateMetricsAsync(provider, metrics, cancellationToken);

                _logger.LogWarning(
                    "Reported failure for provider {Provider}: {ErrorCode}",
                    provider,
                    errorCode);
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> GetAvailableTranscriptionProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var providerInfo = await GetProviderInfoAsync(cancellationToken);
            return providerInfo
                .Where(p => p.IsAvailable)
                .Select(p => p.Name)
                .ToList();
        }

        /// <inheritdoc />
        public async Task<List<string>> GetAvailableTextToSpeechProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var providerInfo = await GetProviderInfoAsync(cancellationToken);
            return providerInfo
                .Where(p => p.IsAvailable && p.Capabilities.SupportedVoices.Any())
                .Select(p => p.Name)
                .ToList();
        }

        /// <inheritdoc />
        public async Task<List<string>> GetAvailableRealtimeProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var providerInfo = await GetProviderInfoAsync(cancellationToken);
            return providerInfo
                .Where(p => p.IsAvailable && p.Capabilities.SupportsRealtime)
                .Select(p => p.Name)
                .ToList();
        }

        /// <inheritdoc />
        public bool ValidateAudioOperation(
            AudioOperation operation,
            string provider,
            AudioRequestBase request,
            out string validationError)
        {
            validationError = string.Empty;

            // Basic validation
            if (string.IsNullOrEmpty(provider))
            {
                validationError = "Provider name is required";
                return false;
            }

            if (request == null)
            {
                validationError = "Request cannot be null";
                return false;
            }

            // Operation-specific validation
            switch (operation)
            {
                case AudioOperation.Transcription:
                    if (request is AudioTranscriptionRequest transcriptionRequest)
                    {
                        if (transcriptionRequest.AudioData == null || transcriptionRequest.AudioData.Length == 0)
                        {
                            validationError = "Audio data is required for transcription";
                            return false;
                        }
                    }
                    break;

                case AudioOperation.TextToSpeech:
                    if (request is TextToSpeechRequest ttsRequest)
                    {
                        if (string.IsNullOrWhiteSpace(ttsRequest.Input))
                        {
                            validationError = "Input text is required for TTS";
                            return false;
                        }
                    }
                    break;
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<AudioRoutingStatistics> GetRoutingStatisticsAsync(
            string? provider = null,
            CancellationToken cancellationToken = default)
        {
            var providerInfo = await GetProviderInfoAsync(cancellationToken);

            if (!string.IsNullOrEmpty(provider))
            {
                var info = providerInfo.FirstOrDefault(p => p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase));
                if (info != null)
                {
                    return new AudioRoutingStatistics
                    {
                        Provider = provider,
                        TotalRequests = (int)(info.Metrics.SampleSize),
                        SuccessRate = info.Metrics.SuccessRate,
                        AverageLatencyMs = info.Metrics.AverageLatencyMs,
                        LastUpdated = info.Metrics.LastUpdated
                    };
                }
            }

            // Return aggregate statistics
            return new AudioRoutingStatistics
            {
                Provider = "All",
                TotalRequests = providerInfo.Sum(p => p.Metrics.SampleSize),
                SuccessRate = providerInfo.Average(p => p.Metrics.SuccessRate),
                AverageLatencyMs = providerInfo.Average(p => p.Metrics.AverageLatencyMs),
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <inheritdoc />
        public async Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            var request = new AudioTranscriptionRequest
            {
                Language = language,
                AudioData = Array.Empty<byte>(),
                AudioFormat = AudioFormat.Mp3
            };

            return await GetTranscriptionClientAsync(request, string.Empty, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            string? voice = null,
            CancellationToken cancellationToken = default)
        {
            var request = new TextToSpeechRequest
            {
                Voice = voice ?? "alloy",
                Input = "test"
            };

            return await GetTextToSpeechClientAsync(request, string.Empty, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IRealtimeAudioClient?> GetRealtimeClientAsync(
            string? model = null,
            CancellationToken cancellationToken = default)
        {
            var config = new RealtimeSessionConfig
            {
                Model = model
            };

            return await GetRealtimeClientAsync(config, string.Empty, cancellationToken);
        }

        private async Task RefreshProviderInfoIfNeededAsync(CancellationToken cancellationToken)
        {
            if (DateTime.UtcNow - _lastRefresh > _cacheExpiration)
            {
                await RefreshProviderInfoAsync(cancellationToken);
            }
        }

        private async Task<AudioProviderInfo> BuildProviderInfoAsync(
            string provider,
            CancellationToken cancellationToken)
        {
            // For now, return default capabilities
            object? capabilities = null;

            return new AudioProviderInfo
            {
                Name = provider,
                IsAvailable = await CheckProviderAvailabilityAsync(provider, cancellationToken),
                Capabilities = MapToAudioProviderCapabilities(capabilities),
                Metrics = await GetProviderMetricsAsync(provider, cancellationToken),
                Region = GetProviderRegion(provider),
                Costs = GetProviderCosts(provider)
            };
        }

        private async Task<bool> CheckProviderAvailabilityAsync(string provider, CancellationToken cancellationToken)
        {
            try
            {
                var client = _clientFactory.GetClientByProvider(provider);

                // Quick availability check
                if (client is IAudioTranscriptionClient sttClient)
                {
                    return await sttClient.SupportsTranscriptionAsync(cancellationToken: cancellationToken);
                }
                else if (client is ITextToSpeechClient ttsClient)
                {
                    return await ttsClient.SupportsTextToSpeechAsync(cancellationToken: cancellationToken);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private AudioProviderRoutingCapabilities MapToAudioProviderCapabilities(object? capabilities)
        {
            // For now, return default capabilities
            // In a real implementation, this would parse the ModelCapabilities object
            return new AudioProviderRoutingCapabilities
            {
                SupportsStreaming = true,
                SupportedLanguages = new List<string> { "en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko" },
                SupportedFormats = new List<string> { "mp3", "wav", "flac", "ogg" },
                MaxAudioDurationSeconds = 3600,
                SupportsRealtime = false,
                SupportedVoices = new List<string>(),
                SupportsCustomVocabulary = false,
                QualityScore = 80
            };
        }

        private Task<AudioProviderMetrics> GetProviderMetricsAsync(
            string provider,
            CancellationToken cancellationToken)
        {
            // In a real implementation, this would fetch from monitoring/metrics service
            var metrics = new AudioProviderMetrics
            {
                AverageLatencyMs = provider switch
                {
                    "OpenAI" => 150,
                    "Deepgram" => 50,
                    "ElevenLabs" => 200,
                    "Google" => 100,
                    "Azure" => 120,
                    _ => 300
                },
                P95LatencyMs = 0, // Would be calculated from real metrics
                SuccessRate = 0.98,
                CurrentLoad = 0.3,
                LastUpdated = DateTime.UtcNow,
                SampleSize = 1000
            };

            return Task.FromResult(metrics);
        }

        private string? GetProviderRegion(string provider)
        {
            // This would be configured or detected
            return provider switch
            {
                "OpenAI" => "us-west",
                "Google" => "us-central",
                "Azure" => "us-east",
                _ => null
            };
        }

        private AudioProviderCosts GetProviderCosts(string provider)
        {
            // These would come from configuration
            return provider switch
            {
                "OpenAI" => new AudioProviderCosts
                {
                    TranscriptionPerMinute = 0.006m,
                    TextToSpeechPer1kChars = 0.015m,
                    RealtimePerMinute = 0.06m
                },
                "Deepgram" => new AudioProviderCosts
                {
                    TranscriptionPerMinute = 0.0043m,
                    TextToSpeechPer1kChars = 0.015m,
                    RealtimePerMinute = 0.0m
                },
                "ElevenLabs" => new AudioProviderCosts
                {
                    TranscriptionPerMinute = 0.0m,
                    TextToSpeechPer1kChars = 0.30m,
                    RealtimePerMinute = 0.0m
                },
                "Google" => new AudioProviderCosts
                {
                    TranscriptionPerMinute = 0.006m,
                    TextToSpeechPer1kChars = 0.016m,
                    RealtimePerMinute = 0.0m
                },
                _ => new AudioProviderCosts
                {
                    TranscriptionPerMinute = 0.01m,
                    TextToSpeechPer1kChars = 0.02m,
                    RealtimePerMinute = 0.1m
                }
            };
        }
    }
}
