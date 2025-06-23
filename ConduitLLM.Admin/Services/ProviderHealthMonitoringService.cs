using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MassTransit;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Background service that monitors provider health by performing periodic health checks
    /// </summary>
    public class ProviderHealthMonitoringService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProviderHealthMonitoringService> _logger;
        private readonly ProviderHealthOptions _options;
        private readonly IPublishEndpoint? _publishEndpoint;
        private Timer? _timer;

        /// <summary>
        /// Initializes a new instance of the ProviderHealthMonitoringService
        /// </summary>
        public ProviderHealthMonitoringService(
            IServiceProvider serviceProvider,
            ILogger<ProviderHealthMonitoringService> logger,
            IOptions<ProviderHealthOptions> options,
            IPublishEndpoint? publishEndpoint = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new ProviderHealthOptions();
            _publishEndpoint = publishEndpoint;
        }

        /// <summary>
        /// Starts the background service
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Provider health monitoring is disabled");
                return Task.CompletedTask;
            }

            var intervalMinutes = _options.DefaultCheckIntervalMinutes;
            _logger.LogInformation("Provider health monitoring service started with interval: {Interval} minutes", 
                intervalMinutes);

            // Create timer for periodic health checks
            _timer = new Timer(
                async _ => await PerformHealthChecksAsync(stoppingToken),
                null,
                TimeSpan.FromMinutes(1), // Initial delay of 1 minute
                TimeSpan.FromMinutes(intervalMinutes));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs health checks for all enabled providers
        /// </summary>
        private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var providerHealthRepository = scope.ServiceProvider.GetRequiredService<IProviderHealthRepository>();
                var providerCredentialRepository = scope.ServiceProvider.GetRequiredService<IProviderCredentialRepository>();
                var llmClientFactory = scope.ServiceProvider.GetService<ILLMClientFactory>();

                // Get all enabled providers with monitoring enabled
                var providers = await providerCredentialRepository.GetAllAsync();
                var enabledProviders = providers.Where(p => p.IsEnabled).ToList();

                if (!enabledProviders.Any())
                {
                    _logger.LogDebug("No enabled providers to monitor");
                    return;
                }

                // Get provider health configurations
                var healthConfigs = await providerHealthRepository.GetAllConfigurationsAsync();
                var configDict = healthConfigs.ToDictionary(c => c.ProviderName);

                foreach (var provider in enabledProviders)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Check if monitoring is enabled for this provider
                    if (!configDict.TryGetValue(provider.ProviderName, out var config) || !config.MonitoringEnabled)
                    {
                        _logger.LogDebug("Health monitoring not enabled for provider: {Provider}", provider.ProviderName);
                        continue;
                    }

                    try
                    {
                        await CheckProviderHealthAsync(provider, providerHealthRepository);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking health for provider: {Provider}", provider.ProviderName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing provider health checks");
            }
        }

        /// <summary>
        /// Checks the health of a specific provider
        /// </summary>
        private async Task CheckProviderHealthAsync(
            ProviderCredential provider, 
            IProviderHealthRepository healthRepository)
        {
            var startTime = DateTime.UtcNow;
            var previousStatus = await healthRepository.GetLatestStatusAsync(provider.ProviderName);
            
            var healthRecord = new ProviderHealthRecord
            {
                ProviderName = provider.ProviderName,
                TimestampUtc = startTime,
                EndpointUrl = GetProviderEndpoint(provider.ProviderName)
            };

            try
            {
                // Perform a simple health check based on provider type
                var isHealthy = await PerformProviderSpecificHealthCheckAsync(provider);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                healthRecord.Status = isHealthy ? ProviderHealthRecord.StatusType.Online : ProviderHealthRecord.StatusType.Offline;
                healthRecord.StatusMessage = isHealthy ? "Provider is healthy" : "Provider health check failed";
                healthRecord.ResponseTimeMs = responseTime;

                _logger.LogInformation("Health check for {Provider}: {Status} ({ResponseTime}ms)", 
                    provider.ProviderName, healthRecord.Status, responseTime);
            }
            catch (Exception ex)
            {
                healthRecord.Status = ProviderHealthRecord.StatusType.Offline;
                healthRecord.StatusMessage = $"Health check failed: {ex.Message}";
                healthRecord.ErrorCategory = DetermineErrorCategory(ex);
                healthRecord.ErrorDetails = ex.ToString();
                healthRecord.ResponseTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogWarning(ex, "Health check failed for provider: {Provider}", provider.ProviderName);
            }

            // Save the health record
            await healthRepository.SaveStatusAsync(healthRecord);
            await healthRepository.UpdateLastCheckedTimeAsync(provider.ProviderName);

            // Publish event if status changed
            if (previousStatus?.Status != healthRecord.Status && _publishEndpoint != null)
            {
                try
                {
                    await _publishEndpoint.Publish(new ProviderHealthChanged
                    {
                        ProviderId = provider.Id,
                        ProviderName = provider.ProviderName,
                        IsHealthy = healthRecord.Status == ProviderHealthRecord.StatusType.Online,
                        Status = $"{healthRecord.Status}: {healthRecord.StatusMessage} (Response time: {healthRecord.ResponseTimeMs}ms)",
                        CorrelationId = Guid.NewGuid().ToString()
                    });

                    _logger.LogInformation("Published ProviderHealthChanged event for {Provider}: {PreviousStatus} -> {CurrentStatus}",
                        provider.ProviderName, previousStatus?.Status, healthRecord.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish ProviderHealthChanged event for {Provider}", provider.ProviderName);
                }
            }
        }

        /// <summary>
        /// Performs provider-specific health check
        /// </summary>
        private async Task<bool> PerformProviderSpecificHealthCheckAsync(ProviderCredential provider)
        {
            // For now, perform a simple connectivity check
            // In a full implementation, this would call provider-specific health endpoints
            
            switch (provider.ProviderName.ToLowerInvariant())
            {
                case "openai":
                    return await CheckOpenAIHealthAsync(provider);
                case "anthropic":
                    return await CheckAnthropicHealthAsync(provider);
                case "google":
                    return await CheckGoogleHealthAsync(provider);
                default:
                    // For unknown providers, assume healthy if credentials exist
                    return !string.IsNullOrEmpty(provider.ApiKey);
            }
        }

        /// <summary>
        /// Checks OpenAI provider health
        /// </summary>
        private async Task<bool> CheckOpenAIHealthAsync(ProviderCredential provider)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {provider.ApiKey}");
                httpClient.Timeout = TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds);

                // Call OpenAI models endpoint as a health check
                var response = await httpClient.GetAsync("https://api.openai.com/v1/models");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks Anthropic provider health
        /// </summary>
        private async Task<bool> CheckAnthropicHealthAsync(ProviderCredential provider)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("x-api-key", provider.ApiKey);
                httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                httpClient.Timeout = TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds);

                // For Anthropic, we'll check if we can reach their API
                // Note: Anthropic doesn't have a dedicated health endpoint
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                request.Content = new StringContent("{\"model\":\"claude-3-haiku-20240307\",\"messages\":[],\"max_tokens\":1}", 
                    System.Text.Encoding.UTF8, "application/json");
                
                var response = await httpClient.SendAsync(request);
                // We expect 400 (bad request) due to empty messages, which still indicates the API is reachable
                return response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks Google provider health
        /// </summary>
        private async Task<bool> CheckGoogleHealthAsync(ProviderCredential provider)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds);

                // Call Google AI models endpoint
                var response = await httpClient.GetAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={provider.ApiKey}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the provider endpoint URL
        /// </summary>
        private string GetProviderEndpoint(string providerName)
        {
            return providerName.ToLowerInvariant() switch
            {
                "openai" => "https://api.openai.com",
                "anthropic" => "https://api.anthropic.com",
                "google" => "https://generativelanguage.googleapis.com",
                "azure" => "https://azure.openai.com",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Determines the error category from an exception
        /// </summary>
        private string DetermineErrorCategory(Exception ex)
        {
            return ex switch
            {
                HttpRequestException => "Network",
                TaskCanceledException => "Timeout",
                UnauthorizedAccessException => "Authentication",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Disposes the timer when stopping
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Provider health monitoring service is stopping");
            
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
            
            await base.StopAsync(cancellationToken);
        }
    }
}