using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides self-healing and automatic failover capabilities for image generation - Failover functionality
    /// </summary>
    public partial class ImageGenerationResilienceService
    {
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
            var mappingService = scope.ServiceProvider.GetService<IModelProviderMappingService>();
            
            if (mappingService != null)
            {
                try
                {
                    // Get all mappings for this provider
                    var allMappings = await mappingService.GetAllMappingsAsync();
                    
                    // Get provider service to load provider information
                    var providerService = scope.ServiceProvider.GetService<IProviderService>();
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
            var mappingService = scope.ServiceProvider.GetService<IModelProviderMappingService>();
            
            if (mappingService != null)
            {
                var allMappings = await mappingService.GetAllMappingsAsync();
                
                // Get provider service to load provider information
                var providerService = scope.ServiceProvider.GetService<IProviderService>();
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
    }
}