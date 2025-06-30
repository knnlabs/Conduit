using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for calculating audio operation costs with provider-specific pricing models.
    /// </summary>
    public class AudioCostCalculationService : IAudioCostCalculationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AudioCostCalculationService> _logger;

        // Provider-specific pricing models (per minute rates)
        private readonly Dictionary<string, ProviderPricingModel> _pricingModels = new()
        {
            ["openai"] = new ProviderPricingModel
            {
                TranscriptionRates = new Dictionary<string, decimal>
                {
                    ["whisper-1"] = 0.006m // $0.006 per minute
                },
                TextToSpeechRates = new Dictionary<string, Dictionary<string, decimal>>
                {
                    ["tts-1"] = new Dictionary<string, decimal>
                    {
                        ["per-character"] = 0.000015m // $15 per 1M characters
                    },
                    ["tts-1-hd"] = new Dictionary<string, decimal>
                    {
                        ["per-character"] = 0.00003m // $30 per 1M characters
                    }
                },
                RealtimeRates = new Dictionary<string, RealtimeRateModel>
                {
                    ["gpt-4o-realtime-preview"] = new RealtimeRateModel
                    {
                        InputAudioPerMinute = 0.10m, // $0.10 per minute input
                        OutputAudioPerMinute = 0.20m, // $0.20 per minute output
                        InputTokenRate = 0.000005m,   // $5 per 1M input tokens
                        OutputTokenRate = 0.000015m   // $15 per 1M output tokens
                    }
                }
            },
            ["elevenlabs"] = new ProviderPricingModel
            {
                TextToSpeechRates = new Dictionary<string, Dictionary<string, decimal>>
                {
                    ["eleven_monolingual_v1"] = new Dictionary<string, decimal>
                    {
                        ["per-character"] = 0.00003m // $30 per 1M characters
                    },
                    ["eleven_multilingual_v2"] = new Dictionary<string, decimal>
                    {
                        ["per-character"] = 0.00006m // $60 per 1M characters
                    },
                    ["eleven_turbo_v2"] = new Dictionary<string, decimal>
                    {
                        ["per-character"] = 0.000018m // $18 per 1M characters
                    }
                }
            },
            ["ultravox"] = new ProviderPricingModel
            {
                RealtimeRates = new Dictionary<string, RealtimeRateModel>
                {
                    ["fixie-ai/ultravox-70b"] = new RealtimeRateModel
                    {
                        InputAudioPerMinute = 0.001m,  // $0.001 per minute
                        OutputAudioPerMinute = 0.001m, // $0.001 per minute
                        MinimumDuration = 1 // 1 minute minimum
                    }
                }
            },
            ["groq"] = new ProviderPricingModel
            {
                TranscriptionRates = new Dictionary<string, decimal>
                {
                    ["whisper-large-v3"] = 0.0001m // $0.0001 per minute
                }
            },
            ["deepgram"] = new ProviderPricingModel
            {
                TranscriptionRates = new Dictionary<string, decimal>
                {
                    ["nova-2"] = 0.0043m, // $0.0043 per minute
                    ["nova-2-medical"] = 0.0145m, // $0.0145 per minute
                    ["nova-2-meeting"] = 0.0125m // $0.0125 per minute
                }
            }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioCostCalculationService"/> class.
        /// </summary>
        public AudioCostCalculationService(
            IServiceProvider serviceProvider,
            ILogger<AudioCostCalculationService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<AudioCostResult> CalculateTranscriptionCostAsync(
            string provider,
            string model,
            double durationSeconds,
            string? virtualKey = null,
            CancellationToken cancellationToken = default)
        {
            var durationMinutes = durationSeconds / 60.0;

            // Check database for custom pricing first
            var customRate = await GetCustomRateAsync(provider, "transcription", model);
            if (customRate.HasValue)
            {
                return new AudioCostResult
                {
                    Provider = provider,
                    Operation = "transcription",
                    Model = model,
                    UnitCount = durationMinutes,
                    UnitType = "minutes",
                    RatePerUnit = customRate.Value,
                    TotalCost = (double)(customRate.Value * (decimal)durationMinutes),
                    VirtualKey = virtualKey
                };
            }

            // Use built-in pricing model
            if (_pricingModels.TryGetValue(provider.ToLowerInvariant(), out var pricingModel) &&
                pricingModel.TranscriptionRates.TryGetValue(model, out var rate))
            {
                return new AudioCostResult
                {
                    Provider = provider,
                    Operation = "transcription",
                    Model = model,
                    UnitCount = durationMinutes,
                    UnitType = "minutes",
                    RatePerUnit = rate,
                    TotalCost = (double)(rate * (decimal)durationMinutes),
                    VirtualKey = virtualKey
                };
            }

            // Default fallback rate
            var defaultRate = 0.01m; // $0.01 per minute
_logger.LogWarning("No pricing found for {Provider}/{Model}, using default rate".Replace(Environment.NewLine, ""), provider.Replace(Environment.NewLine, ""), model.Replace(Environment.NewLine, ""));

            return new AudioCostResult
            {
                Provider = provider,
                Operation = "transcription",
                Model = model,
                UnitCount = durationMinutes,
                UnitType = "minutes",
                RatePerUnit = defaultRate,
                TotalCost = (double)(defaultRate * (decimal)durationMinutes),
                VirtualKey = virtualKey,
                IsEstimate = true
            };
        }

        /// <inheritdoc />
        public async Task<AudioCostResult> CalculateTextToSpeechCostAsync(
            string provider,
            string model,
            int characterCount,
            string? voice = null,
            string? virtualKey = null,
            CancellationToken cancellationToken = default)
        {
            // Check database for custom pricing first
            var customRate = await GetCustomRateAsync(provider, "text-to-speech", model);
            if (customRate.HasValue)
            {
                var units = characterCount / 1000.0; // Convert to thousands
                return new AudioCostResult
                {
                    Provider = provider,
                    Operation = "text-to-speech",
                    Model = model,
                    UnitCount = units,
                    UnitType = "1k-characters",
                    RatePerUnit = customRate.Value,
                    TotalCost = (double)(customRate.Value * (decimal)units),
                    VirtualKey = virtualKey,
                    Voice = voice
                };
            }

            // Use built-in pricing model
            if (_pricingModels.TryGetValue(provider.ToLowerInvariant(), out var pricingModel) &&
                pricingModel.TextToSpeechRates.TryGetValue(model, out var modelRates) &&
                modelRates.TryGetValue("per-character", out var rate))
            {
                return new AudioCostResult
                {
                    Provider = provider,
                    Operation = "text-to-speech",
                    Model = model,
                    UnitCount = characterCount,
                    UnitType = "characters",
                    RatePerUnit = rate,
                    TotalCost = (double)(rate * characterCount),
                    VirtualKey = virtualKey,
                    Voice = voice
                };
            }

            // Default fallback rate
            var defaultRate = 0.00002m; // $20 per 1M characters
_logger.LogWarning("No pricing found for {Provider}/{Model}, using default rate".Replace(Environment.NewLine, ""), provider.Replace(Environment.NewLine, ""), model.Replace(Environment.NewLine, ""));

            return new AudioCostResult
            {
                Provider = provider,
                Operation = "text-to-speech",
                Model = model,
                UnitCount = characterCount,
                UnitType = "characters",
                RatePerUnit = defaultRate,
                TotalCost = (double)(defaultRate * characterCount),
                VirtualKey = virtualKey,
                Voice = voice,
                IsEstimate = true
            };
        }

        /// <inheritdoc />
        public async Task<AudioCostResult> CalculateRealtimeCostAsync(
            string provider,
            string model,
            double inputAudioSeconds,
            double outputAudioSeconds,
            int? inputTokens = null,
            int? outputTokens = null,
            string? virtualKey = null,
            CancellationToken cancellationToken = default)
        {
            var inputMinutes = inputAudioSeconds / 60.0;
            var outputMinutes = outputAudioSeconds / 60.0;

            // Check database for custom pricing first
            var customRate = await GetCustomRateAsync(provider, "realtime", model);
            if (customRate.HasValue)
            {
                var totalMinutes = inputMinutes + outputMinutes;
                return new AudioCostResult
                {
                    Provider = provider,
                    Operation = "realtime",
                    Model = model,
                    UnitCount = totalMinutes,
                    UnitType = "minutes",
                    RatePerUnit = customRate.Value,
                    TotalCost = (double)(customRate.Value * (decimal)totalMinutes),
                    VirtualKey = virtualKey
                };
            }

            // Use built-in pricing model
            if (_pricingModels.TryGetValue(provider.ToLowerInvariant(), out var pricingModel) &&
                pricingModel.RealtimeRates.TryGetValue(model, out var realtimeRate))
            {
                // For negative values (refunds), don't apply minimum duration
                var effectiveInputMinutes = inputMinutes < 0 ? inputMinutes : Math.Max(inputMinutes, realtimeRate.MinimumDuration);
                var effectiveOutputMinutes = outputMinutes < 0 ? outputMinutes : Math.Max(outputMinutes, realtimeRate.MinimumDuration);
                
                var audioCost = (double)(
                    realtimeRate.InputAudioPerMinute * (decimal)effectiveInputMinutes +
                    realtimeRate.OutputAudioPerMinute * (decimal)effectiveOutputMinutes);

                var tokenCost = 0.0;
                if (inputTokens.HasValue && realtimeRate.InputTokenRate.HasValue)
                {
                    tokenCost += (double)(realtimeRate.InputTokenRate.Value * inputTokens.Value);
                }
                if (outputTokens.HasValue && realtimeRate.OutputTokenRate.HasValue)
                {
                    tokenCost += (double)(realtimeRate.OutputTokenRate.Value * outputTokens.Value);
                }

                return new AudioCostResult
                {
                    Provider = provider,
                    Operation = "realtime",
                    Model = model,
                    UnitCount = inputMinutes + outputMinutes,
                    UnitType = "minutes",
                    RatePerUnit = realtimeRate.InputAudioPerMinute, // Average rate
                    TotalCost = audioCost + tokenCost,
                    VirtualKey = virtualKey,
                    DetailedBreakdown = new Dictionary<string, double>
                    {
                        ["audio_cost"] = audioCost,
                        ["token_cost"] = tokenCost,
                        ["input_minutes"] = inputMinutes,
                        ["output_minutes"] = outputMinutes,
                        ["input_tokens"] = inputTokens ?? 0,
                        ["output_tokens"] = outputTokens ?? 0
                    }
                };
            }

            // Default fallback rate
            var defaultInputRate = 0.05m; // $0.05 per minute input
            var defaultOutputRate = 0.10m; // $0.10 per minute output
            _logger.LogWarning("No pricing found for {Provider}/{Model}, using default rates", provider, model);

            var fallbackCost = (double)(
                defaultInputRate * (decimal)inputMinutes +
                defaultOutputRate * (decimal)outputMinutes);

            return new AudioCostResult
            {
                Provider = provider,
                Operation = "realtime",
                Model = model,
                UnitCount = inputMinutes + outputMinutes,
                UnitType = "minutes",
                RatePerUnit = (defaultInputRate + defaultOutputRate) / 2,
                TotalCost = fallbackCost,
                VirtualKey = virtualKey,
                IsEstimate = true
            };
        }

        /// <inheritdoc />
        public async Task<AudioCostResult> CalculateAudioCostAsync(
            string provider,
            string operation,
            string model,
            double durationSeconds,
            int characterCount,
            string? virtualKey = null,
            CancellationToken cancellationToken = default)
        {
            return operation.ToLowerInvariant() switch
            {
                "transcription" => await CalculateTranscriptionCostAsync(provider, model, durationSeconds, virtualKey, cancellationToken),
                "text-to-speech" or "tts" => await CalculateTextToSpeechCostAsync(provider, model, characterCount, null, virtualKey, cancellationToken),
                "realtime" => await CalculateRealtimeCostAsync(provider, model, durationSeconds / 2, durationSeconds / 2, null, null, virtualKey, cancellationToken),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
        }

        private async Task<decimal?> GetCustomRateAsync(string provider, string operation, string model)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetService<IAudioCostRepository>();

            if (repository == null) return null;

            try
            {
                var cost = await repository.GetCurrentCostAsync(provider, operation, model);
                if (cost != null && cost.IsActive)
                {
                    return cost.CostPerUnit;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get custom rate for {Provider}/{Operation}/{Model}",
                    provider, operation, model.Replace(Environment.NewLine, ""));
            }

            return null;
        }
    }

    /// <summary>
    /// Provider-specific pricing model.
    /// </summary>
    internal class ProviderPricingModel
    {
        public Dictionary<string, decimal> TranscriptionRates { get; set; } = new();
        public Dictionary<string, Dictionary<string, decimal>> TextToSpeechRates { get; set; } = new();
        public Dictionary<string, RealtimeRateModel> RealtimeRates { get; set; } = new();
    }

    /// <summary>
    /// Real-time pricing model.
    /// </summary>
    internal class RealtimeRateModel
    {
        public decimal InputAudioPerMinute { get; set; }
        public decimal OutputAudioPerMinute { get; set; }
        public decimal? InputTokenRate { get; set; }
        public decimal? OutputTokenRate { get; set; }
        public double MinimumDuration { get; set; } = 0;
    }

    /// <summary>
    /// Result of audio cost calculation.
    /// </summary>
    public class AudioCostResult
    {
        public string Provider { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public double UnitCount { get; set; }
        public string UnitType { get; set; } = string.Empty;
        public decimal RatePerUnit { get; set; }
        public double TotalCost { get; set; }
        public string? VirtualKey { get; set; }
        public string? Voice { get; set; }
        public bool IsEstimate { get; set; }
        public Dictionary<string, double>? DetailedBreakdown { get; set; }
    }

    /// <summary>
    /// Interface for audio cost calculation service.
    /// </summary>
    public interface IAudioCostCalculationService
    {
        /// <summary>
        /// Calculates the cost of audio transcription.
        /// </summary>
        Task<AudioCostResult> CalculateTranscriptionCostAsync(
            string provider,
            string model,
            double durationSeconds,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the cost of text-to-speech.
        /// </summary>
        Task<AudioCostResult> CalculateTextToSpeechCostAsync(
            string provider,
            string model,
            int characterCount,
            string? voice = null,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the cost of real-time audio session.
        /// </summary>
        Task<AudioCostResult> CalculateRealtimeCostAsync(
            string provider,
            string model,
            double inputAudioSeconds,
            double outputAudioSeconds,
            int? inputTokens = null,
            int? outputTokens = null,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generic method to calculate audio costs.
        /// </summary>
        Task<AudioCostResult> CalculateAudioCostAsync(
            string provider,
            string operation,
            string model,
            double durationSeconds,
            int characterCount,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);
    }
}
