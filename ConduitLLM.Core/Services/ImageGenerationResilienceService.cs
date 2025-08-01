using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides self-healing and automatic failover capabilities for image generation.
    /// </summary>
    public class ImageGenerationResilienceService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ImageGenerationResilienceService> _logger;
        private readonly IImageGenerationMetricsCollector _metricsCollector;
        private readonly IImageGenerationAlertingService _alertingService;
        private readonly ImageGenerationResilienceOptions _options;
        private readonly IPublishEndpoint? _publishEndpoint;
        
        private readonly ConcurrentDictionary<int, ProviderHealthState> _providerStates = new();
        private readonly ConcurrentDictionary<int, FailoverState> _failoverStates = new();
        private readonly ConcurrentDictionary<int, RecoveryAttempt> _recoveryAttempts = new();
        
        // Cache for providers
        private readonly ConcurrentDictionary<int, ConduitLLM.Configuration.Entities.Provider> _providerCache = new();
        
        private Timer? _healthCheckTimer;
        private Timer? _recoveryTimer;

        public ImageGenerationResilienceService(
            IServiceProvider serviceProvider,
            ILogger<ImageGenerationResilienceService> logger,
            IImageGenerationMetricsCollector metricsCollector,
            IImageGenerationAlertingService alertingService,
            IOptions<ImageGenerationResilienceOptions> options,
            IPublishEndpoint? publishEndpoint = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
            _options = options?.Value ?? new ImageGenerationResilienceOptions();
            _publishEndpoint = publishEndpoint;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Image generation resilience service is disabled");
                return Task.CompletedTask;
            }
            
            _logger.LogInformation(
                "Image generation resilience service started - Health check: {HealthInterval}min, Recovery: {RecoveryInterval}min",
                _options.HealthCheckIntervalMinutes, _options.RecoveryCheckIntervalMinutes);
            
            // Initialize provider states
            _ = RefreshProviderCacheAsync(stoppingToken);
            
            // Start health monitoring timer
            _healthCheckTimer = new Timer(
                async _ => await PerformHealthChecksAsync(stoppingToken),
                null,
                TimeSpan.FromSeconds(30), // Initial delay
                TimeSpan.FromMinutes(_options.HealthCheckIntervalMinutes));
            
            // Start recovery timer
            _recoveryTimer = new Timer(
                async _ => await PerformRecoveryChecksAsync(stoppingToken),
                null,
                TimeSpan.FromMinutes(1), // Initial delay
                TimeSpan.FromMinutes(_options.RecoveryCheckIntervalMinutes));
            
            return Task.CompletedTask;
        }

        private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            
            try
            {
                // Get current metrics
                var metrics = await _metricsCollector.GetMetricsSnapshotAsync(cancellationToken);
                
                // Check each provider
                foreach (var (providerName, status) in metrics.ProviderStatuses)
                {
                    // Try to find provider ID from name
                    var providerId = GetProviderIdFromName(providerName);
                    if (providerId.HasValue)
                    {
                        await CheckProviderHealthAsync(providerId.Value, status, metrics);
                    }
                    else
                    {
                        _logger.LogWarning("Could not find provider ID for provider name: {ProviderName}", providerName);
                    }
                }
                
                // Check for global issues
                await CheckGlobalHealthAsync(metrics);
                
                // Trigger alert evaluation
                await _alertingService.EvaluateMetricsAsync(metrics, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing resilience health checks");
            }
        }

        private async Task CheckProviderHealthAsync(
            int providerId,
            ProviderStatus status,
            ImageGenerationMetricsSnapshot metrics)
        {
            var state = _providerStates.GetOrAdd(providerId, new ProviderHealthState
            {
                ProviderId = providerId
            });
            
            // Update health state
            state.IsHealthy = status.IsHealthy;
            state.HealthScore = status.HealthScore;
            state.ConsecutiveFailures = status.ConsecutiveFailures;
            state.LastChecked = DateTime.UtcNow;
            
            // Check if provider needs intervention
            if (!status.IsHealthy || status.ConsecutiveFailures >= _options.FailureThreshold)
            {
                await HandleUnhealthyProviderAsync(providerId, state, status);
            }
            else if (state.IsQuarantined && status.HealthScore > _options.RecoveryHealthScoreThreshold)
            {
                // Provider appears to be recovering
                await AttemptProviderRecoveryAsync(providerId, state);
            }
            
            // Check for performance degradation
            if (status.AverageResponseTimeMs > _options.SlowResponseThresholdMs)
            {
                await HandleSlowProviderAsync(providerId, status);
            }
        }

        private async Task HandleUnhealthyProviderAsync(
            int providerId,
            ProviderHealthState state,
            ProviderStatus status)
        {
            var providerName = GetProviderName(providerId);
            _logger.LogWarning(
                "Provider {ProviderId} ({ProviderName}) is unhealthy - Score: {Score}, Failures: {Failures}",
                providerId, providerName, status.HealthScore, status.ConsecutiveFailures);
            
            // Check if already quarantined
            if (!state.IsQuarantined)
            {
                // Quarantine the provider
                await QuarantineProviderAsync(providerId, state, $"Health score: {status.HealthScore:F2}, Consecutive failures: {status.ConsecutiveFailures}");
                
                // Initiate failover if primary provider
                if (IsPrimaryProvider(providerId))
                {
                    await InitiateFailoverAsync(providerId, status);
                }
            }
            
            // Update recovery attempts
            _recoveryAttempts.AddOrUpdate(providerId,
                new RecoveryAttempt { ProviderId = providerId, AttemptCount = 1 },
                (_, attempt) => { attempt.AttemptCount++; return attempt; });
        }

        private async Task QuarantineProviderAsync(int providerId, ProviderHealthState state, string reason)
        {
            state.IsQuarantined = true;
            state.QuarantinedAt = DateTime.UtcNow;
            state.QuarantineReason = reason;
            
            var providerName = GetProviderName(providerId);
            _logger.LogWarning("Quarantined provider {ProviderId} ({ProviderName}): {Reason}", providerId, providerName, reason);
            
            // Update provider configuration to disable it
            using var scope = _serviceProvider.CreateScope();
            var mappingService = scope.ServiceProvider.GetService<Interfaces.Configuration.IModelProviderMappingService>();
            
            if (mappingService != null)
            {
                try
                {
                    // Get all mappings for this provider
                    var allMappings = await mappingService.GetAllMappingsAsync();
                    
                    // Get provider service to load provider information
                    var providerService = scope.ServiceProvider.GetService<ConduitLLM.Configuration.IProviderService>();
                    if (providerService == null)
                    {
                        _logger.LogWarning("IProviderService not available, cannot quarantine provider");
                        return;
                    }
                    
                    // Load all providers to match by ProviderType
                    var allProviders = await providerService.GetAllProvidersAsync();
                    var providersByType = allProviders
                        .Where(p => p.ProviderType.ToString() == providerName)
                        .Select(p => p.Id)
                        .ToList();
                    
                    var providerMappings = allMappings
                        .Where(m => providersByType.Contains(m.ProviderId))
                        .ToList();
                    
                    // TODO: Implement mapping updates when the service supports it
                    // foreach (var mapping in providerMappings)
                    // {
                    //     mapping.IsEnabled = false;
                    //     await mappingService.UpdateMappingAsync(mapping);
                    // }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to disable mappings for quarantined provider {Provider}", providerName);
                }
            }
            
            // Publish quarantine event
            if (_publishEndpoint != null)
            {
                await _publishEndpoint.Publish(new ProviderQuarantined
                {
                    ProviderId = providerId,
                    ProviderName = GetProviderName(providerId),
                    Reason = reason,
                    QuarantinedAt = state.QuarantinedAt.Value,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }
        }

        private async Task InitiateFailoverAsync(int failedProviderId, ProviderStatus status)
        {
            var failedProviderName = GetProviderName(failedProviderId);
            _logger.LogInformation("Initiating failover from provider {ProviderId} ({ProviderName})", failedProviderId, failedProviderName);
            
            var failoverState = new FailoverState
            {
                FailedProviderId = failedProviderId,
                InitiatedAt = DateTime.UtcNow,
                Reason = $"Provider unhealthy: {status.LastError}"
            };
            
            // Find alternative providers
            using var scope = _serviceProvider.CreateScope();
            var mappingService = scope.ServiceProvider.GetService<Interfaces.Configuration.IModelProviderMappingService>();
            
            if (mappingService != null)
            {
                var allMappings = await mappingService.GetAllMappingsAsync();
                
                // Get provider service to load provider information
                var providerService = scope.ServiceProvider.GetService<ConduitLLM.Configuration.IProviderService>();
                if (providerService == null)
                {
                    _logger.LogWarning("IProviderService not available, cannot initiate failover");
                    return;
                }
                
                // Load all providers
                var allProviders = await providerService.GetAllProvidersAsync();
                var providerLookup = allProviders.ToDictionary(p => p.Id, p => p);
                
                // Find image generation mappings not from the failed provider
                var imageProviders = allMappings
                    .Where(m => m.SupportsImageGeneration && m.IsEnabled && m.ProviderId != failedProviderId)
                    .GroupBy(m => m.ProviderId)
                    .ToList();
                
                // Select best alternative based on health scores
                int? selectedProviderId = null;
                double bestScore = 0;
                
                foreach (var providerGroup in imageProviders)
                {
                    var providerId = providerGroup.Key;
                    if (_providerStates.TryGetValue(providerId, out var state) && 
                        state.IsHealthy && 
                        state.HealthScore > bestScore)
                    {
                        selectedProviderId = providerId;
                        bestScore = state.HealthScore;
                    }
                }
                
                if (selectedProviderId.HasValue)
                {
                    failoverState.FailoverProviderId = selectedProviderId.Value;
                    failoverState.Status = FailoverStatus.Active;
                    
                    var selectedProviderName = GetProviderName(selectedProviderId.Value);
                    _logger.LogInformation(
                        "Failover initiated: {FailedId} ({FailedName}) -> {FailoverId} ({FailoverName})",
                        failedProviderId, failedProviderName, selectedProviderId.Value, selectedProviderName);
                    
                    // Update failover configuration
                    await UpdateFailoverConfigurationAsync(failedProviderId, selectedProviderId.Value);
                }
                else
                {
                    failoverState.Status = FailoverStatus.NoAlternative;
                    _logger.LogError("No healthy alternative providers available for failover");
                }
            }
            
            _failoverStates[failedProviderId] = failoverState;
        }

        private async Task UpdateFailoverConfigurationAsync(int failedProviderId, int failoverProviderId)
        {
            // This would update routing configuration to redirect traffic
            // In a real implementation, this might update a configuration service
            // or publish events that the routing layer would consume
            
            if (_publishEndpoint != null)
            {
                await _publishEndpoint.Publish(new ProviderFailoverInitiated
                {
                    FailedProviderId = failedProviderId,
                    FailedProviderName = GetProviderName(failedProviderId),
                    FailoverProviderId = failoverProviderId,
                    FailoverProviderName = GetProviderName(failoverProviderId),
                    InitiatedAt = DateTime.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }
        }

        private async Task HandleSlowProviderAsync(int providerId, ProviderStatus status)
        {
            var providerName = GetProviderName(providerId);
            _logger.LogWarning(
                "Provider {ProviderId} ({ProviderName}) experiencing slow response times: {ResponseTime}ms",
                providerId, providerName, status.AverageResponseTimeMs);
            
            // Reduce load on slow provider
            var state = _providerStates[providerId];
            if (!state.IsThrottled)
            {
                state.IsThrottled = true;
                state.ThrottleLevel = 0.5; // Reduce to 50% traffic
                
                _logger.LogInformation(
                    "Throttling provider {ProviderId} ({ProviderName}) to {Level:P0} traffic",
                    providerId, providerName, state.ThrottleLevel);
                
                // Update provider weight in load balancing
                await UpdateProviderWeightAsync(providerId, state.ThrottleLevel);
            }
        }

        private async Task PerformRecoveryChecksAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            
            try
            {
                // Check quarantined providers for recovery
                var quarantinedProviders = _providerStates
                    .Where(p => p.Value.IsQuarantined)
                    .ToList();
                
                foreach (var (providerId, state) in quarantinedProviders)
                {
                    await CheckProviderRecoveryAsync(providerId, state);
                }
                
                // Check active failovers
                var activeFailovers = _failoverStates
                    .Where(f => f.Value.Status == FailoverStatus.Active)
                    .ToList();
                
                foreach (var (originalProviderId, failoverState) in activeFailovers)
                {
                    await CheckFailoverRecoveryAsync(originalProviderId, failoverState);
                }
                
                // Perform self-healing actions
                await PerformSelfHealingAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing recovery checks");
            }
        }

        private async Task CheckProviderRecoveryAsync(int providerId, ProviderHealthState state)
        {
            if (!state.QuarantinedAt.HasValue)
                return;
            
            var quarantineDuration = DateTime.UtcNow - state.QuarantinedAt.Value;
            
            // Check if minimum quarantine time has passed
            if (quarantineDuration < _options.MinimumQuarantineTime)
                return;
            
            var providerName = GetProviderName(providerId);
            _logger.LogInformation("Checking recovery for quarantined provider {ProviderId} ({ProviderName})", providerId, providerName);
            
            // Perform health probe
            var isHealthy = await ProbeProviderHealthAsync(providerId);
            
            if (isHealthy)
            {
                await AttemptProviderRecoveryAsync(providerId, state);
            }
            else if (quarantineDuration > _options.MaximumQuarantineTime)
            {
                _logger.LogError(
                    "Provider {ProviderId} ({ProviderName}) exceeded maximum quarantine time without recovery",
                    providerId, providerName);
                
                // Mark as permanently failed
                state.IsPermanentlyFailed = true;
            }
        }

        private Task<bool> ProbeProviderHealthAsync(int providerId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                
                // Perform a lightweight health check
                // This would typically make a simple API call to verify the provider is responsive
                
                return Task.FromResult(true); // Simplified for this implementation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health probe failed for provider {ProviderId}", providerId);
                return Task.FromResult(false);
            }
        }

        private async Task AttemptProviderRecoveryAsync(int providerId, ProviderHealthState state)
        {
            var providerName = GetProviderName(providerId);
            _logger.LogInformation("Attempting recovery for provider {ProviderId} ({ProviderName})", providerId, providerName);
            
            // Re-enable provider with limited traffic
            state.IsQuarantined = false;
            state.IsThrottled = true;
            state.ThrottleLevel = 0.1; // Start with 10% traffic
            state.RecoveryStarted = DateTime.UtcNow;
            
            // Re-enable provider mappings
            using var scope = _serviceProvider.CreateScope();
            var mappingService = scope.ServiceProvider.GetService<Interfaces.Configuration.IModelProviderMappingService>();
            
            if (mappingService != null)
            {
                try
                {
                    // Get all mappings for this specific provider ID
                    var allMappings = await mappingService.GetAllMappingsAsync();
                    var providerMappings = allMappings
                        .Where(m => m.ProviderId == providerId)
                        .ToList();
                    
                    // TODO: Implement mapping updates when the service supports it
                    // foreach (var mapping in providerMappings)
                    // {
                    //     mapping.IsEnabled = true;
                    //     await mappingService.UpdateMappingAsync(mapping);
                    // }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to re-enable mappings for provider {ProviderId}", providerId);
                }
            }
            
            // Update provider weight for gradual recovery
            await UpdateProviderWeightAsync(providerId, state.ThrottleLevel);
            
            // Publish recovery event
            if (_publishEndpoint != null)
            {
                await _publishEndpoint.Publish(new ProviderRecoveryInitiated
                {
                    ProviderId = providerId,
                    ProviderName = GetProviderName(providerId),
                    ThrottleLevel = state.ThrottleLevel,
                    InitiatedAt = DateTime.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }
        }

        private async Task CheckFailoverRecoveryAsync(int originalProviderId, FailoverState failoverState)
        {
            // Check if original provider has recovered
            if (_providerStates.TryGetValue(originalProviderId, out var state) && 
                state.IsHealthy && 
                !state.IsQuarantined)
            {
                var originalProviderName = GetProviderName(originalProviderId);
                _logger.LogInformation(
                    "Original provider {ProviderId} ({ProviderName}) has recovered, reversing failover",
                    originalProviderId, originalProviderName);
                
                // Gradually shift traffic back
                failoverState.Status = FailoverStatus.Recovering;
                
                // Update routing to gradually restore traffic
                await RestoreOriginalProviderAsync(originalProviderId, failoverState.FailoverProviderId);
            }
        }

        private async Task RestoreOriginalProviderAsync(int originalProviderId, int failoverProviderId)
        {
            if (_publishEndpoint != null)
            {
                await _publishEndpoint.Publish(new ProviderFailoverReverted
                {
                    OriginalProviderId = originalProviderId,
                    OriginalProviderName = GetProviderName(originalProviderId),
                    FailoverProviderId = failoverProviderId,
                    FailoverProviderName = GetProviderName(failoverProviderId),
                    RevertedAt = DateTime.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }
            
            // Remove failover state after successful restoration
            _failoverStates.TryRemove(originalProviderId, out _);
        }

        private async Task PerformSelfHealingAsync()
        {
            // Check for common issues and attempt to fix them
            
            // 1. Clear stale cache entries
            await ClearStaleCacheEntriesAsync();
            
            // 2. Reset stuck circuit breakers
            await ResetStuckCircuitBreakersAsync();
            
            // 3. Rebalance provider load
            await RebalanceProviderLoadAsync();
            
            // 4. Clean up old metrics data
            await CleanupOldMetricsAsync();
        }

        private async Task ClearStaleCacheEntriesAsync()
        {
            // This would clear cache entries that might be causing issues
            _logger.LogDebug("Clearing stale cache entries");
            await Task.CompletedTask;
        }

        private async Task ResetStuckCircuitBreakersAsync()
        {
            // Reset circuit breakers that have been open too long
            var stuckProviders = _providerStates
                .Where(p => p.Value.IsQuarantined && 
                           p.Value.QuarantinedAt.HasValue &&
                           DateTime.UtcNow - p.Value.QuarantinedAt.Value > TimeSpan.FromHours(1))
                .ToList();
            
            foreach (var (providerId, state) in stuckProviders)
            {
                var providerName = GetProviderName(providerId);
                _logger.LogInformation("Resetting stuck circuit breaker for provider {ProviderId} ({ProviderName})", providerId, providerName);
                await CheckProviderRecoveryAsync(providerId, state);
            }
        }

        private async Task RebalanceProviderLoadAsync()
        {
            // Ensure load is properly distributed among healthy providers
            var healthyProviders = _providerStates
                .Where(p => p.Value.IsHealthy && !p.Value.IsQuarantined)
                .ToList();
            
            if (healthyProviders.Count > 1)
            {
                // Calculate optimal weights based on health scores
                var totalScore = healthyProviders.Sum(p => p.Value.HealthScore);
                
                foreach (var (providerId, state) in healthyProviders)
                {
                    var weight = state.HealthScore / totalScore;
                    await UpdateProviderWeightAsync(providerId, weight);
                }
            }
            
            await Task.CompletedTask;
        }

        private async Task CleanupOldMetricsAsync()
        {
            // Trigger metrics cleanup
            // TODO: Implement cleanup when method is available
            await Task.CompletedTask;
        }

        private async Task UpdateProviderWeightAsync(int providerId, double weight)
        {
            // This would update the provider's weight in the load balancing configuration
            var providerName = GetProviderName(providerId);
            _logger.LogDebug("Updated provider {ProviderId} ({ProviderName}) weight to {Weight:F2}", providerId, providerName, weight);
            await Task.CompletedTask;
        }

        private async Task CheckGlobalHealthAsync(ImageGenerationMetricsSnapshot metrics)
        {
            // Check for system-wide issues
            
            // High error rate across all providers
            if (metrics.SuccessRate < 90)
            {
                _logger.LogWarning("System-wide high error rate detected: {Rate:F1}%", 100 - metrics.SuccessRate);
                
                // Implement global mitigation strategies
                await ImplementGlobalMitigationAsync("high_error_rate");
            }
            
            // Queue backup
            if (metrics.QueueMetrics.TotalDepth > _options.QueueDepthThreshold)
            {
                _logger.LogWarning("Queue depth critical: {Depth} items", metrics.QueueMetrics.TotalDepth);
                
                // Implement queue management strategies
                await ImplementGlobalMitigationAsync("queue_backup");
            }
        }

        private async Task ImplementGlobalMitigationAsync(string issueType)
        {
            switch (issueType)
            {
                case "high_error_rate":
                    // Enable more aggressive retries
                    // Increase timeouts
                    // Enable fallback models
                    break;
                    
                case "queue_backup":
                    // Increase concurrency limits
                    // Enable request shedding for low-priority requests
                    // Scale out workers if possible
                    break;
            }
            
            await Task.CompletedTask;
        }

        private int? GetProviderIdFromName(string providerName)
        {
            // Try to find provider by name in cache
            var provider = _providerCache.Values.FirstOrDefault(p => 
                p.ProviderName == providerName || p.ProviderType.ToString() == providerName);
            return provider?.Id;
        }
        
        private string GetProviderName(int providerId)
        {
            if (_providerCache.TryGetValue(providerId, out var provider))
            {
                return provider.ProviderName ?? provider.ProviderType.ToString();
            }
            return $"Provider_{providerId}";
        }
        

        private async Task RefreshProviderCacheAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var providerService = scope.ServiceProvider.GetService<ConduitLLM.Configuration.IProviderService>();
            
            if (providerService == null)
            {
                _logger.LogWarning("IProviderService not available, cannot refresh provider cache");
                return;
            }
            
            try
            {
                var providers = await providerService.GetAllProvidersAsync();
                
                // Update the provider cache
                _providerCache.Clear();
                foreach (var provider in providers)
                {
                    _providerCache[provider.Id] = provider;
                    
                    // Initialize health state for enabled providers
                    if (provider.IsEnabled && !_providerStates.ContainsKey(provider.Id))
                    {
                        _providerStates[provider.Id] = new ProviderHealthState
                        {
                            ProviderId = provider.Id,
                            IsHealthy = true,
                            HealthScore = 1.0
                        };
                    }
                }
                
                _logger.LogInformation("Refreshed provider cache. Found {Count} providers", _providerCache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh provider cache");
            }
        }
        
        private bool IsPrimaryProvider(int providerId)
        {
            // Determine if this is a primary provider that requires immediate failover
            // In the new model, this should be based on provider configuration, not hardcoded
            // Determine if this is a primary provider that requires immediate failover
            if (_providerCache.TryGetValue(providerId, out var provider))
            {
                // TODO: Add IsPrimary flag to Provider entity
                // For now, check the provider name
                var name = provider.ProviderName ?? string.Empty;
                return name.Contains("Primary", StringComparison.OrdinalIgnoreCase) || 
                       name.Contains("Production", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Image generation resilience service is stopping");
            
            _healthCheckTimer?.Change(Timeout.Infinite, 0);
            _healthCheckTimer?.Dispose();
            
            _recoveryTimer?.Change(Timeout.Infinite, 0);
            _recoveryTimer?.Dispose();
            
            await base.StopAsync(cancellationToken);
        }

        private class ProviderHealthState
        {
            public int ProviderId { get; set; }
            public bool IsHealthy { get; set; } = true;
            public double HealthScore { get; set; } = 1.0;
            public int ConsecutiveFailures { get; set; }
            public DateTime LastChecked { get; set; }
            public bool IsQuarantined { get; set; }
            public DateTime? QuarantinedAt { get; set; }
            public string? QuarantineReason { get; set; }
            public bool IsThrottled { get; set; }
            public double ThrottleLevel { get; set; } = 1.0;
            public DateTime? RecoveryStarted { get; set; }
            public bool IsPermanentlyFailed { get; set; }
        }

        private class FailoverState
        {
            public int FailedProviderId { get; set; }
            public int FailoverProviderId { get; set; }
            public DateTime InitiatedAt { get; set; }
            public FailoverStatus Status { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        private enum FailoverStatus
        {
            Initiated,
            Active,
            Recovering,
            Completed,
            NoAlternative
        }

        private class RecoveryAttempt
        {
            public int ProviderId { get; set; }
            public int AttemptCount { get; set; }
            public DateTime LastAttempt { get; set; } = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Configuration options for image generation resilience.
    /// </summary>
    public class ImageGenerationResilienceOptions
    {
        public bool Enabled { get; set; } = true;
        public int HealthCheckIntervalMinutes { get; set; } = 2;
        public int RecoveryCheckIntervalMinutes { get; set; } = 5;
        public int FailureThreshold { get; set; } = 3;
        public double SlowResponseThresholdMs { get; set; } = 30000;
        public double RecoveryHealthScoreThreshold { get; set; } = 0.8;
        public TimeSpan MinimumQuarantineTime { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan MaximumQuarantineTime { get; set; } = TimeSpan.FromHours(24);
        public int QueueDepthThreshold { get; set; } = 100;
    }

    #region Events

    public class ProviderQuarantined
    {
        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime QuarantinedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }

    public class ProviderFailoverInitiated
    {
        public int FailedProviderId { get; set; }
        public string FailedProviderName { get; set; } = string.Empty;
        public int FailoverProviderId { get; set; }
        public string FailoverProviderName { get; set; } = string.Empty;
        public DateTime InitiatedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }

    public class ProviderRecoveryInitiated
    {
        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public double ThrottleLevel { get; set; }
        public DateTime InitiatedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }

    public class ProviderFailoverReverted
    {
        public int OriginalProviderId { get; set; }
        public string OriginalProviderName { get; set; } = string.Empty;
        public int FailoverProviderId { get; set; }
        public string FailoverProviderName { get; set; } = string.Empty;
        public DateTime RevertedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }

    #endregion
}