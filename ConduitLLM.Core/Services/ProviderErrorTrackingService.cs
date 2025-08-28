using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for tracking and managing provider API errors
    /// </summary>
    public class ProviderErrorTrackingService : IProviderErrorTrackingService
    {
        private readonly IRedisErrorStore _errorStore;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ProviderErrorTrackingService> _logger;

        public ProviderErrorTrackingService(
            IRedisErrorStore errorStore,
            IServiceScopeFactory scopeFactory,
            ILogger<ProviderErrorTrackingService> logger)
        {
            _errorStore = errorStore ?? throw new ArgumentNullException(nameof(errorStore));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task TrackErrorAsync(ProviderErrorInfo error)
        {
            try
            {
                if (error.IsFatal)
                {
                    await _errorStore.TrackFatalErrorAsync(error.KeyCredentialId, error);
                }
                else
                {
                    await _errorStore.TrackWarningAsync(error.KeyCredentialId, error);
                }
                
                // Update provider summary
                await _errorStore.UpdateProviderSummaryAsync(error.ProviderId, error.IsFatal);
                
                // Add to global feed
                await _errorStore.AddToGlobalFeedAsync(error);
                
                // Check if we should disable the key
                if (error.IsFatal && await ShouldDisableKeyAsync(error.KeyCredentialId, error.ErrorType))
                {
                    await DisableKeyAsync(error.KeyCredentialId, 
                        $"Auto-disabled due to {error.ErrorType}: {error.ErrorMessage}");
                }
                
                _logger.LogInformation(
                    "Tracked {ErrorType} error for key {KeyId}: {Message}",
                    error.ErrorType, error.KeyCredentialId, error.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track provider error for key {KeyId}", error.KeyCredentialId);
                // Don't throw - error tracking should not break the main flow
            }
        }

        // Removed private methods - now using IRedisErrorStore

        public async Task<bool> ShouldDisableKeyAsync(int keyId, ProviderErrorType errorType)
        {
            // Check if we have a disable policy for this error type
            if (!ErrorThresholdConfiguration.FatalErrorPolicies.TryGetValue(errorType, out var policy))
            {
                return false;
            }
            
            // Immediate disable for certain error types
            if (policy.DisableImmediately)
            {
                _logger.LogWarning("Key {KeyId} will be disabled immediately due to {ErrorType}", 
                    keyId, errorType);
                return true;
            }
            
            // Check occurrence count within time window
            var fatalData = await _errorStore.GetFatalErrorDataAsync(keyId);
            
            if (fatalData != null && 
                fatalData.ErrorType == errorType.ToString() &&
                fatalData.LastSeen.HasValue)
            {
                var timeSinceLastError = DateTime.UtcNow - fatalData.LastSeen.Value;
                
                if (timeSinceLastError <= policy.TimeWindow && 
                    fatalData.Count >= policy.RequiredOccurrences)
                {
                    _logger.LogWarning(
                        "Key {KeyId} will be disabled: {Count} occurrences of {ErrorType} within {Window}",
                        keyId, fatalData.Count, errorType, policy.TimeWindow);
                    return true;
                }
            }
            
            return false;
        }

        public async Task DisableKeyAsync(int keyId, string reason)
        {
            try
            {
                // Update database
                using var scope = _scopeFactory.CreateScope();
                var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
                var providerRepo = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
                
                var key = await keyRepo.GetByIdAsync(keyId);
                if (key == null)
                {
                    _logger.LogWarning("Attempted to disable non-existent key {KeyId}", keyId);
                    return;
                }

                // Check if this is a primary key
                if (key.IsPrimary)
                {
                    // For primary keys, disable the provider instead
                    var provider = await providerRepo.GetByIdAsync(key.ProviderId);
                    if (provider != null && provider.IsEnabled)
                    {
                        provider.IsEnabled = false;
                        await providerRepo.UpdateAsync(provider);
                        
                        _logger.LogWarning(
                            "Disabled provider {ProviderId} ({ProviderName}) due to primary key failure: {Reason}",
                            provider.Id, provider.ProviderName, reason);
                        
                        // Update Redis to track provider disable
                        await _errorStore.MarkProviderDisabledAsync(provider.Id, DateTime.UtcNow, reason);
                        
                        // Publish event for UI update (could create a ProviderDisabledEvent)
                        var publishEndpoint = scope.ServiceProvider.GetService<MassTransit.IPublishEndpoint>();
                        if (publishEndpoint != null)
                        {
                            // Still publish key disabled event so UI knows something happened
                            await publishEndpoint.Publish(new ProviderKeyDisabledEvent
                            {
                                KeyId = keyId,
                                ProviderId = key.ProviderId,
                                Reason = $"Provider disabled: {reason}",
                                DisabledAt = DateTime.UtcNow
                            });
                        }
                    }
                }
                else if (key.IsEnabled)
                {
                    // For secondary keys, disable the key normally
                    key.IsEnabled = false;
                    await keyRepo.UpdateAsync(key);
                    
                    _logger.LogWarning("Disabled secondary key {KeyId} for provider {ProviderId}: {Reason}",
                        keyId, key.ProviderId, reason);
                    
                    // Update Redis
                    await _errorStore.MarkKeyDisabledAsync(keyId, DateTime.UtcNow);
                    await _errorStore.AddDisabledKeyToProviderAsync(key.ProviderId, keyId);
                    
                    // Check if all keys are now disabled - if so, disable the provider
                    var allKeys = await keyRepo.GetByProviderIdAsync(key.ProviderId);
                    if (allKeys.All(k => !k.IsEnabled))
                    {
                        var provider = await providerRepo.GetByIdAsync(key.ProviderId);
                        if (provider != null && provider.IsEnabled)
                        {
                            provider.IsEnabled = false;
                            await providerRepo.UpdateAsync(provider);
                            
                            _logger.LogWarning(
                                "Disabled provider {ProviderId} ({ProviderName}) - all keys are disabled",
                                provider.Id, provider.ProviderName);
                            
                            await _errorStore.MarkProviderDisabledAsync(provider.Id, DateTime.UtcNow, "All keys disabled");
                        }
                    }
                    
                    // Publish event for UI update
                    var publishEndpoint = scope.ServiceProvider.GetService<MassTransit.IPublishEndpoint>();
                    if (publishEndpoint != null)
                    {
                        await publishEndpoint.Publish(new ProviderKeyDisabledEvent
                        {
                            KeyId = keyId,
                            ProviderId = key.ProviderId,
                            Reason = reason,
                            DisabledAt = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable key {KeyId}", keyId);
                throw;
            }
        }

        public async Task<IReadOnlyList<ProviderErrorInfo>> GetRecentErrorsAsync(
            int? providerId = null, 
            int? keyId = null,
            int limit = 100)
        {
            var entries = await _errorStore.GetRecentErrorsAsync(limit);
            var errors = new List<ProviderErrorInfo>();
            
            foreach (var entry in entries)
            {
                // Apply filters
                if (providerId.HasValue && entry.ProviderId != providerId.Value)
                    continue;
                if (keyId.HasValue && entry.KeyId != keyId.Value)
                    continue;
                
                errors.Add(new ProviderErrorInfo
                {
                    KeyCredentialId = entry.KeyId,
                    ProviderId = entry.ProviderId,
                    ErrorType = Enum.Parse<ProviderErrorType>(entry.ErrorType),
                    ErrorMessage = entry.Message,
                    OccurredAt = entry.Timestamp
                });
            }
            
            return errors;
        }

        public async Task<Dictionary<int, int>> GetErrorCountsByKeyAsync(int providerId, TimeSpan window)
        {
            using var scope = _scopeFactory.CreateScope();
            var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
            
            var keys = await keyRepo.GetByProviderIdAsync(providerId);
            var keyIds = keys.Select(k => k.Id).ToList();
            
            var errorCounts = await _errorStore.GetErrorCountsByKeysAsync(providerId, keyIds, window);
            
            return errorCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
        }

        public async Task ClearErrorsForKeyAsync(int keyId)
        {
            await _errorStore.ClearErrorsForKeyAsync(keyId);
        }

        public async Task<KeyErrorDetails?> GetKeyErrorDetailsAsync(int keyId)
        {
            using var scope = _scopeFactory.CreateScope();
            var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
            
            var key = await keyRepo.GetByIdAsync(keyId);
            if (key == null)
                return null;
            
            var errorData = await _errorStore.GetKeyErrorDataAsync(keyId);
            
            var details = new KeyErrorDetails
            {
                KeyId = keyId,
                KeyName = key.KeyName ?? $"Key {keyId}",
                IsDisabled = !key.IsEnabled,
                DisabledAt = errorData?.FatalError?.DisabledAt
            };
            
            if (errorData?.FatalError != null)
            {
                var fatal = errorData.FatalError;
                details.FatalError = new FatalErrorInfo
                {
                    ErrorType = Enum.Parse<ProviderErrorType>(fatal.ErrorType ?? "Unknown"),
                    Count = fatal.Count,
                    FirstSeen = fatal.FirstSeen ?? DateTime.UtcNow,
                    LastSeen = fatal.LastSeen ?? DateTime.UtcNow,
                    LastErrorMessage = fatal.LastErrorMessage ?? "",
                    LastStatusCode = fatal.LastStatusCode
                };
            }
            
            if (errorData?.RecentWarnings != null)
            {
                foreach (var warning in errorData.RecentWarnings)
                {
                    details.RecentWarnings.Add(new WarningInfo
                    {
                        Type = Enum.Parse<ProviderErrorType>(warning.Type),
                        Message = warning.Message,
                        Timestamp = warning.Timestamp
                    });
                }
            }
            
            return details;
        }

        public async Task<ProviderErrorSummary?> GetProviderSummaryAsync(int providerId)
        {
            var summaryData = await _errorStore.GetProviderSummaryAsync(providerId);
            
            if (summaryData == null)
                return null;
            
            return new ProviderErrorSummary
            {
                ProviderId = providerId,
                TotalErrors = summaryData.TotalErrors,
                FatalErrors = summaryData.FatalErrors,
                Warnings = summaryData.Warnings,
                DisabledKeyIds = summaryData.DisabledKeyIds,
                LastError = summaryData.LastError
            };
        }

        public async Task<ErrorStatistics> GetErrorStatisticsAsync(TimeSpan window)
        {
            var statsData = await _errorStore.GetErrorStatisticsAsync(window);
            
            var stats = new ErrorStatistics
            {
                TotalErrors = statsData.TotalErrors,
                FatalErrors = statsData.FatalErrors,
                Warnings = statsData.Warnings,
                ErrorsByType = statsData.ErrorsByType
            };
            
            // Count disabled keys
            using (var scope = _scopeFactory.CreateScope())
            {
                var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
                var keys = await keyRepo.GetAllAsync();
                stats.DisabledKeys = keys.Count(k => !k.IsEnabled);
            }
            
            return stats;
        }
    }
}