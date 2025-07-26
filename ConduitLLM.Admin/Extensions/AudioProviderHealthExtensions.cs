using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for configuring audio provider health checks.
    /// </summary>
    public static class AudioProviderHealthExtensions
    {
        /// <summary>
        /// Adds health checks for audio providers.
        /// </summary>
        public static IHealthChecksBuilder AddAudioProviderHealthChecks(
            this IHealthChecksBuilder builder,
            string? tag = null)
        {
            return builder.Add(new HealthCheckRegistration(
                "audio_providers",
                serviceProvider => new AudioProviderHealthCheck(
                    serviceProvider.GetRequiredService<IAudioProviderConfigRepository>(),
                    serviceProvider.GetRequiredService<ILLMClientFactory>(),
                    serviceProvider.GetRequiredService<ILogger<AudioProviderHealthCheck>>()),
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tag != null ? new[] { tag } : null));
        }
    }

    /// <summary>
    /// Health check for audio provider connectivity and capabilities.
    /// </summary>
    public class AudioProviderHealthCheck : IHealthCheck
    {
        private readonly IAudioProviderConfigRepository _repository;
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILogger<AudioProviderHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioProviderHealthCheck"/> class.
        /// </summary>
        public AudioProviderHealthCheck(
            IAudioProviderConfigRepository repository,
            ILLMClientFactory clientFactory,
            ILogger<AudioProviderHealthCheck> logger)
        {
            _repository = repository;
            _clientFactory = clientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Performs the health check.
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var configs = await _repository.GetAllAsync();
                var enabledConfigs = configs.Where(c =>
                    c.ProviderCredential?.IsEnabled == true &&
                    (c.TranscriptionEnabled || c.TextToSpeechEnabled || c.RealtimeEnabled))
                    .ToList();

                if (!enabledConfigs.Any())
                {
                    return HealthCheckResult.Unhealthy("No audio providers are enabled");
                }

                var results = new Dictionary<string, object>();
                var healthyProviders = 0;
                var totalProviders = enabledConfigs.Count;

                foreach (var config in enabledConfigs)
                {
                    var providerType = config.ProviderCredential.ProviderType;
                    var providerHealth = new Dictionary<string, string>();

                    try
                    {
                        var client = _clientFactory.GetClientByProviderId(config.ProviderCredential.Id);

                        // Check each enabled capability
                        if (config.TranscriptionEnabled && client is IAudioTranscriptionClient transcriptionClient)
                        {
                            var supported = await transcriptionClient.SupportsTranscriptionAsync(cancellationToken: cancellationToken);
                            providerHealth["transcription"] = supported ? "healthy" : "not supported";
                        }

                        if (config.TextToSpeechEnabled && client is ITextToSpeechClient ttsClient)
                        {
                            var voices = await ttsClient.ListVoicesAsync(apiKey: null, cancellationToken);
                            providerHealth["tts"] = voices.Any() ? "healthy" : "no voices available";
                        }

                        if (config.RealtimeEnabled && client is IRealtimeAudioClient realtimeClient)
                        {
                            var capabilities = await realtimeClient.GetCapabilitiesAsync(cancellationToken);
                            providerHealth["realtime"] = capabilities != null ? "healthy" : "not available";
                        }

                        if (providerHealth.Values.Any(v => v.Contains("healthy")))
                        {
                            healthyProviders++;
                        }

                        results[$"provider_{providerType}"] = providerHealth;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check health for provider {Provider}", providerType);
                        results[$"provider_{providerType}"] = new { error = ex.Message };
                    }
                }

                results["healthy_providers"] = healthyProviders;
                results["total_providers"] = totalProviders;

                if (healthyProviders == 0)
                {
                    return HealthCheckResult.Unhealthy("All audio providers are unhealthy", data: results);
                }
                else if (healthyProviders < totalProviders)
                {
                    return HealthCheckResult.Degraded(
                        $"{totalProviders - healthyProviders} audio provider(s) are unhealthy",
                        data: results);
                }
                else
                {
                    return HealthCheckResult.Healthy("All audio providers are healthy", data: results);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking audio provider health");
                return HealthCheckResult.Unhealthy("Failed to check audio provider health", ex);
            }
        }
    }
}
