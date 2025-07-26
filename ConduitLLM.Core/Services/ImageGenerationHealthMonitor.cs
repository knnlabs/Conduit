using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Advanced health monitoring service for image generation providers.
    /// </summary>
    public class ImageGenerationHealthMonitor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ImageGenerationHealthMonitor> _logger;
        private readonly IImageGenerationMetricsCollector _metricsCollector;
        private readonly IImageGenerationAlertingService _alertingService;
        private readonly ImageGenerationHealthOptions _options;
        private readonly IPublishEndpoint? _publishEndpoint;
        
        private readonly ConcurrentDictionary<int, ProviderHealthData> _providerHealth = new();
        private readonly ConcurrentDictionary<int, CircuitBreakerState> _circuitBreakers = new();
        private Timer? _healthCheckTimer;
        private Timer? _metricsEvaluationTimer;

        public ImageGenerationHealthMonitor(
            IServiceProvider serviceProvider,
            ILogger<ImageGenerationHealthMonitor> logger,
            IImageGenerationMetricsCollector metricsCollector,
            IImageGenerationAlertingService alertingService,
            IOptions<ImageGenerationHealthOptions> options,
            IPublishEndpoint? publishEndpoint = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
            _options = options?.Value ?? new ImageGenerationHealthOptions();
            _publishEndpoint = publishEndpoint;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Image generation health monitoring is disabled");
                return Task.CompletedTask;
            }
            
            _logger.LogInformation(
                "Image generation health monitoring started - Check interval: {CheckInterval} minutes, Metrics interval: {MetricsInterval} minutes",
                _options.HealthCheckIntervalMinutes, _options.MetricsEvaluationIntervalMinutes);
            
            // Start health check timer
            _healthCheckTimer = new Timer(
                async _ => await PerformHealthChecksAsync(stoppingToken),
                null,
                TimeSpan.FromMinutes(1), // Initial delay
                TimeSpan.FromMinutes(_options.HealthCheckIntervalMinutes));
            
            // Start metrics evaluation timer
            _metricsEvaluationTimer = new Timer(
                async _ => await EvaluateMetricsAsync(stoppingToken),
                null,
                TimeSpan.FromMinutes(2), // Initial delay
                TimeSpan.FromMinutes(_options.MetricsEvaluationIntervalMinutes));
            
            return Task.CompletedTask;
        }

        private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mappingService = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.IModelProviderMappingService>();
                var providerHealthRepository = scope.ServiceProvider.GetRequiredService<IProviderHealthRepository>();
                
                // Get all image generation capable providers
                var allMappings = await mappingService.GetAllMappingsAsync();
                var imageProviders = allMappings
                    .Where(m => m.SupportsImageGeneration)
                    .GroupBy(m => m.ProviderId)
                    .ToList();
                
                foreach (var providerGroup in imageProviders)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    var providerId = providerGroup.Key;
                    var models = providerGroup.Select(m => m.ModelAlias).ToList();
                    var providerType = providerGroup.First().ProviderType;
                    
                    try
                    {
                        await CheckProviderHealthAsync(providerId, providerType, models, providerHealthRepository);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking health for provider ID {ProviderId} ({ProviderType})", providerId, providerType);
                    }
                }
                
                // Update overall system health metrics
                await UpdateSystemHealthMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing image generation health checks");
            }
        }

        private async Task CheckProviderHealthAsync(
            int providerId,
            ProviderType providerType,
            List<string> models,
            IProviderHealthRepository healthRepository)
        {
            var healthData = _providerHealth.GetOrAdd(providerId, new ProviderHealthData
            {
                ProviderId = providerId,
                ProviderType = providerType,
                Models = models
            });
            
            var circuitBreaker = _circuitBreakers.GetOrAdd(providerId, new CircuitBreakerState());
            
            // Check if circuit breaker is open
            if (circuitBreaker.IsOpen && DateTime.UtcNow < circuitBreaker.NextRetryTime)
            {
                _logger.LogDebug("Circuit breaker open for {Provider}, skipping health check", providerType);
                return;
            }
            
            var stopwatch = Stopwatch.StartNew();
            var success = false;
            string? errorMessage = null;
            
            try
            {
                // Perform provider-specific health check
                success = await PerformProviderHealthCheckAsync(providerId, providerType, models.FirstOrDefault());
                stopwatch.Stop();
                
                if (success)
                {
                    // Reset circuit breaker on success
                    circuitBreaker.Reset();
                    healthData.ConsecutiveFailures = 0;
                    healthData.LastSuccessTime = DateTime.UtcNow;
                }
                else
                {
                    healthData.ConsecutiveFailures++;
                    errorMessage = "Health check failed";
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                healthData.ConsecutiveFailures++;
                errorMessage = ex.Message;
                _logger.LogWarning(ex, "Health check failed for {Provider}", providerType);
            }
            
            // Update health data
            healthData.LastCheckTime = DateTime.UtcNow;
            healthData.LastResponseTimeMs = stopwatch.ElapsedMilliseconds;
            healthData.IsHealthy = success;
            
            // Calculate health score
            var healthScore = CalculateHealthScore(healthData);
            healthData.HealthScore = healthScore;
            
            // Update circuit breaker
            if (!success)
            {
                circuitBreaker.RecordFailure();
                if (circuitBreaker.FailureCount >= _options.CircuitBreakerThreshold)
                {
                    circuitBreaker.Open(_options.CircuitBreakerTimeout);
                    _logger.LogWarning("Circuit breaker opened for {Provider}", providerType);
                }
            }
            
            // Record metrics
            _metricsCollector.RecordProviderHealth(providerType.ToString(), healthScore, success, errorMessage);
            
            // Save to repository
            // Provider type is already available, no need to parse
            
            var healthRecord = new ProviderHealthRecord
            {
                ProviderType = providerType,
                TimestampUtc = DateTime.UtcNow,
                Status = success ? ProviderHealthRecord.StatusType.Online : ProviderHealthRecord.StatusType.Offline,
                StatusMessage = success ? "Provider is healthy" : errorMessage ?? "Unknown error",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                EndpointUrl = GetProviderEndpoint(providerType)
            };
            
            await healthRepository.SaveStatusAsync(healthRecord);
            
            // Publish health change event if status changed
            if (healthData.PreviousHealthy != success && _publishEndpoint != null)
            {
                await PublishHealthChangeEventAsync(providerId, providerType, success, healthScore, errorMessage);
            }
            
            healthData.PreviousHealthy = success;
        }

        private async Task<bool> PerformProviderHealthCheckAsync(int providerId, ProviderType providerType, string? model)
        {
            using var scope = _serviceProvider.CreateScope();
            
            try
            {
                // Use provider ID-based lookup first
                var clientFactory = scope.ServiceProvider.GetService<ILLMClientFactory>();
                if (clientFactory != null)
                {
                    try
                    {
                        var client = clientFactory.GetClientByProviderId(providerId);
                        // If we can create a client, consider it healthy for now
                        return client != null;
                    }
                    catch (NotSupportedException)
                    {
                        // Fall back to name-based checks for backward compatibility
                    }
                }
                
                // Use provider metadata to check if it supports image generation
                if (clientFactory != null && !string.IsNullOrEmpty(model))
                {
                    try
                    {
                        var client = clientFactory.GetClient(model);
                        
                        // Check if the client supports health checks
                        if (client is IHealthCheckable healthCheckable)
                        {
                            var healthResult = await healthCheckable.CheckHealthAsync(scope.ServiceProvider.GetRequiredService<CancellationToken>());
                            return healthResult.IsHealthy;
                        }
                        
                        // Fallback to checking if client exists
                        return client != null;
                    }
                    catch
                    {
                        return false;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing health check for {Provider}", providerType);
                return false;
            }
        }

        private async Task<bool> CheckOpenAIImageHealthAsync(IServiceScope scope, int providerId)
        {
            try
            {
                var credentialRepository = scope.ServiceProvider.GetService<IProviderCredentialRepository>();
                if (credentialRepository == null) return false;
                
                var credentials = await credentialRepository.GetByIdAsync(providerId);
                if (credentials == null) return false;
                
                // Get the API key from ProviderKeyCredentials
                var primaryKey = credentials.ProviderKeyCredentials?.FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ??
                                credentials.ProviderKeyCredentials?.FirstOrDefault(k => k.IsEnabled);
                
                if (primaryKey?.ApiKey == null) return false;
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {primaryKey.ApiKey}");
                httpClient.Timeout = TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds);
                
                // Check models endpoint
                var response = await httpClient.GetAsync("https://api.openai.com/v1/models");
                if (!response.IsSuccessStatusCode) return false;
                
                // Verify DALL-E models are available
                var content = await response.Content.ReadAsStringAsync();
                return content.Contains("dall-e", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> CheckMiniMaxImageHealthAsync(IServiceScope scope, int providerId)
        {
            try
            {
                var credentialRepository = scope.ServiceProvider.GetService<IProviderCredentialRepository>();
                if (credentialRepository == null) return false;
                
                var credentials = await credentialRepository.GetByIdAsync(providerId);
                if (credentials == null) return false;
                
                // Get the API key from ProviderKeyCredentials
                var primaryKey = credentials.ProviderKeyCredentials?.FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ??
                                credentials.ProviderKeyCredentials?.FirstOrDefault(k => k.IsEnabled);
                
                if (primaryKey?.ApiKey == null) return false;
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {primaryKey.ApiKey}");
                httpClient.Timeout = TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds);
                
                // MiniMax doesn't have a dedicated health endpoint
                // We'll check if we can reach their API
                var response = await httpClient.GetAsync("https://api.minimax.chat/v1/text/chatcompletion_v2");
                
                // We expect 400 or 401 for GET request, which still indicates API is reachable
                return response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                       response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed ||
                       response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> CheckReplicateHealthAsync(IServiceScope scope, int providerId)
        {
            try
            {
                var credentialRepository = scope.ServiceProvider.GetService<IProviderCredentialRepository>();
                if (credentialRepository == null) return false;
                
                var credentials = await credentialRepository.GetByIdAsync(providerId);
                if (credentials == null) return false;
                
                // Get the API key from ProviderKeyCredentials
                var primaryKey = credentials.ProviderKeyCredentials?.FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ??
                                credentials.ProviderKeyCredentials?.FirstOrDefault(k => k.IsEnabled);
                
                if (primaryKey?.ApiKey == null) return false;
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {primaryKey.ApiKey}");
                httpClient.Timeout = TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds);
                
                // Check account endpoint
                var response = await httpClient.GetAsync("https://api.replicate.com/v1/account");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private double CalculateHealthScore(ProviderHealthData healthData)
        {
            var score = 1.0;
            
            // Deduct for consecutive failures
            if (healthData.ConsecutiveFailures > 0)
            {
                score -= Math.Min(0.5, healthData.ConsecutiveFailures * 0.1);
            }
            
            // Deduct for slow response times
            if (healthData.LastResponseTimeMs > _options.SlowResponseThresholdMs)
            {
                var slownessFactor = (healthData.LastResponseTimeMs - _options.SlowResponseThresholdMs) / 
                                   (double)_options.SlowResponseThresholdMs;
                score -= Math.Min(0.3, slownessFactor * 0.1);
            }
            
            // Deduct for recent failures
            if (!healthData.IsHealthy)
            {
                score -= 0.3;
            }
            
            // Bonus for recent successes
            if (healthData.LastSuccessTime.HasValue)
            {
                var timeSinceSuccess = DateTime.UtcNow - healthData.LastSuccessTime.Value;
                if (timeSinceSuccess < TimeSpan.FromMinutes(5))
                {
                    score += 0.1;
                }
            }
            
            return Math.Max(0, Math.Min(1, score));
        }

        private async Task EvaluateMetricsAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            
            try
            {
                // Get current metrics snapshot
                var metrics = await _metricsCollector.GetMetricsSnapshotAsync(cancellationToken);
                
                // Evaluate against alert rules
                await _alertingService.EvaluateMetricsAsync(metrics, cancellationToken);
                
                // Check for automatic recovery actions
                await CheckAutomaticRecoveryActionsAsync(metrics);
                
                // Log summary
                _logger.LogInformation(
                    "Metrics evaluation complete - Active generations: {Active}, Success rate: {SuccessRate:F1}%, " +
                    "Avg response time: {AvgTime:F0}ms, Queue depth: {QueueDepth}",
                    metrics.ActiveGenerations,
                    metrics.SuccessRate,
                    metrics.AverageResponseTimeMs,
                    metrics.QueueMetrics.TotalDepth);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating image generation metrics");
            }
        }

        private async Task CheckAutomaticRecoveryActionsAsync(ImageGenerationMetricsSnapshot metrics)
        {
            // Check for providers that might need recovery
            foreach (var (providerName, status) in metrics.ProviderStatuses)
            {
                // Try to parse provider name to type
                if (Enum.TryParse<ProviderType>(providerName, true, out var providerType))
                {
                    // Find the provider ID by type from our health data
                    var healthDataEntry = _providerHealth.Values.FirstOrDefault(h => h.ProviderType == providerType);
                    if (healthDataEntry != null && !status.IsHealthy && _circuitBreakers.TryGetValue(healthDataEntry.ProviderId, out var circuitBreaker))
                    {
                        // Check if it's time to retry
                        if (circuitBreaker.IsOpen && DateTime.UtcNow >= circuitBreaker.NextRetryTime)
                        {
                            _logger.LogInformation("Attempting recovery for provider {Provider} (ID: {ProviderId})", providerType, healthDataEntry.ProviderId);
                            circuitBreaker.AttemptReset();
                        }
                    }
                }
            }
            
            await Task.CompletedTask;
        }

        private async Task UpdateSystemHealthMetricsAsync()
        {
            // Calculate overall system health
            var healthyProviders = _providerHealth.Values.Count(p => p.IsHealthy);
            var totalProviders = Math.Max(1, _providerHealth.Count);
            var systemHealthScore = healthyProviders / (double)totalProviders;
            
            // Get resource utilization
            var process = Process.GetCurrentProcess();
            var cpuPercent = 0.0; // Would need proper CPU calculation
            var memoryMb = process.WorkingSet64 / (1024.0 * 1024.0);
            var threadCount = process.Threads.Count;
            
            _metricsCollector.RecordResourceUtilization(cpuPercent, memoryMb, 0, threadCount);
            
            await Task.CompletedTask;
        }

        private async Task PublishHealthChangeEventAsync(
            int providerId,
            ProviderType providerType,
            bool isHealthy,
            double healthScore,
            string? errorMessage)
        {
            if (_publishEndpoint == null) return;
            
            try
            {
                await _publishEndpoint.Publish(new ProviderHealthChanged
                {
                    ProviderId = providerId,
                    ProviderType = providerType,
                    IsHealthy = isHealthy,
                    Status = $"Health score: {healthScore:F2}, Status: {(isHealthy ? "Healthy" : "Unhealthy")}" +
                            (string.IsNullOrEmpty(errorMessage) ? "" : $", Error: {errorMessage}"),
                    CorrelationId = Guid.NewGuid().ToString()
                });
                
                _logger.LogInformation(
                    "Published health change event for {Provider}: {Status}",
                    providerType, isHealthy ? "Healthy" : "Unhealthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish health change event for {Provider}", providerType);
            }
        }

        private string GetProviderEndpoint(ProviderType providerType)
        {
            // This method should be replaced with getting the URL from provider metadata
            // For now, return unknown
            return "Provider-specific";
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Image generation health monitoring is stopping");
            
            _healthCheckTimer?.Change(Timeout.Infinite, 0);
            _healthCheckTimer?.Dispose();
            
            _metricsEvaluationTimer?.Change(Timeout.Infinite, 0);
            _metricsEvaluationTimer?.Dispose();
            
            await base.StopAsync(cancellationToken);
        }

        private class ProviderHealthData
        {
            public int ProviderId { get; set; }
            public ProviderType ProviderType { get; set; }
            public List<string> Models { get; set; } = new();
            public bool IsHealthy { get; set; }
            public bool PreviousHealthy { get; set; }
            public double HealthScore { get; set; } = 1.0;
            public int ConsecutiveFailures { get; set; }
            public DateTime LastCheckTime { get; set; }
            public DateTime? LastSuccessTime { get; set; }
            public double LastResponseTimeMs { get; set; }
        }

        private class CircuitBreakerState
        {
            public bool IsOpen { get; private set; }
            public int FailureCount { get; private set; }
            public DateTime NextRetryTime { get; private set; }
            public DateTime LastFailureTime { get; private set; }
            
            public void RecordFailure()
            {
                FailureCount++;
                LastFailureTime = DateTime.UtcNow;
            }
            
            public void Open(TimeSpan timeout)
            {
                IsOpen = true;
                NextRetryTime = DateTime.UtcNow.Add(timeout);
            }
            
            public void Reset()
            {
                IsOpen = false;
                FailureCount = 0;
                NextRetryTime = DateTime.MinValue;
            }
            
            public void AttemptReset()
            {
                if (IsOpen && DateTime.UtcNow >= NextRetryTime)
                {
                    IsOpen = false;
                    // Keep failure count to track if it fails again
                }
            }
        }
    }

    /// <summary>
    /// Configuration options for image generation health monitoring.
    /// </summary>
    public class ImageGenerationHealthOptions
    {
        public bool Enabled { get; set; } = true;
        public int HealthCheckIntervalMinutes { get; set; } = 5;
        public int MetricsEvaluationIntervalMinutes { get; set; } = 1;
        public int HealthCheckTimeoutSeconds { get; set; } = 30;
        public int SlowResponseThresholdMs { get; set; } = 5000;
        public int CircuitBreakerThreshold { get; set; } = 5;
        public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(10);
    }
}