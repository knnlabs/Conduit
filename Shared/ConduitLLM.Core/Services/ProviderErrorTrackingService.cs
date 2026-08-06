using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for tracking and managing provider API errors
    /// </summary>
    public class ProviderErrorTrackingService : IProviderErrorTrackingService
    {
        public const string AllKeysDisabledReason = "All provider keys disabled automatically";
        private static readonly TimeSpan DisableGuardTtl = TimeSpan.FromSeconds(30);

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
                        $"Auto-disabled due to {error.ErrorType}: {error.ErrorMessage}",
                        error.ErrorType,
                        isAutomatic: true,
                        errorMessage: error.ErrorMessage);
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

            // Count distinct request IDs so retries within one request cannot disable a key.
            var occurrenceCount = await _errorStore.GetDistinctFatalRequestCountAsync(
                keyId, errorType, policy.TimeWindow);

            if (occurrenceCount >= policy.RequiredOccurrences)
            {
                _logger.LogWarning(
                    "Key {KeyId} will be disabled: {Count} distinct requests returned {ErrorType} within {Window}",
                    keyId, occurrenceCount, errorType, policy.TimeWindow);
                return true;
            }

            _logger.LogWarning(
                "Key {KeyId} recorded {Count}/{RequiredCount} distinct {ErrorType} failures within {Window}; not disabling yet",
                keyId, occurrenceCount, policy.RequiredOccurrences, errorType, policy.TimeWindow);
            return false;
        }

        public async Task DisableKeyAsync(
            int keyId,
            string reason,
            ProviderErrorType errorType = ProviderErrorType.Unknown,
            bool isAutomatic = false,
            string? errorMessage = null)
        {
            if (!await _errorStore.TryAcquireKeyDisableAsync(keyId, DisableGuardTtl))
            {
                _logger.LogDebug(
                    "Another request is already disabling key {KeyId}; skipping duplicate disable",
                    keyId);
                return;
            }

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

            if (!key.IsEnabled)
            {
                _logger.LogDebug("Key {KeyId} is already disabled", keyId);
                return;
            }

            var allKeys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                keyRepo.GetByProviderIdPaginatedAsync, key.ProviderId);
            var propagateBalanceFailure =
                errorType == ProviderErrorType.InsufficientBalance &&
                key.ProviderAccountGroup > 0;
            var affectedKeys = propagateBalanceFailure
                ? allKeys
                    .Where(candidate =>
                        candidate.IsEnabled &&
                        candidate.ProviderAccountGroup == key.ProviderAccountGroup)
                    .ToList()
                : new List<ProviderKeyCredential> { key };
            if (affectedKeys.All(candidate => candidate.Id != keyId))
            {
                affectedKeys.Add(key);
            }

            var affectedKeyIds = affectedKeys.Select(candidate => candidate.Id).ToHashSet();
            var wasPrimary = affectedKeys.Any(candidate => candidate.IsPrimary);
            var fallbackKey = allKeys.FirstOrDefault(candidate =>
                candidate.IsEnabled && !affectedKeyIds.Contains(candidate.Id));
            var disabledAt = DateTime.UtcNow;
            var effectiveReason = propagateBalanceFailure
                ? $"Shared account group {key.ProviderAccountGroup} balance exhausted " +
                  $"(triggered by key {keyId}): {reason}"
                : reason;

            foreach (var affectedKey in affectedKeys)
            {
                // A primary key cannot remain primary while disabled because of the
                // database constraint.
                affectedKey.IsPrimary = false;
                affectedKey.IsEnabled = false;
                await keyRepo.UpdateAsync(affectedKey);

                await _errorStore.MarkKeyDisabledAsync(
                    affectedKey.Id, disabledAt, errorType);
                await _errorStore.AddDisabledKeyToProviderAsync(
                    key.ProviderId, affectedKey.Id);
            }

            if (wasPrimary && fallbackKey != null)
            {
                await keyRepo.SetPrimaryKeyAsync(key.ProviderId, fallbackKey.Id);
            }

            _logger.LogWarning(
                "Disabled provider keys {KeyIds} for provider {ProviderId}: {Reason}",
                string.Join(",", affectedKeyIds),
                key.ProviderId,
                effectiveReason);

            // The provider itself is only disabled when this was its last enabled key.
            if (fallbackKey == null)
            {
                var provider = await providerRepo.GetByIdAsync(key.ProviderId);
                if (provider != null && provider.IsEnabled)
                {
                    provider.IsEnabled = false;
                    await providerRepo.UpdateAsync(provider);

                    _logger.LogWarning(
                        "Disabled provider {ProviderId} ({ProviderName}) - all keys are disabled",
                        provider.Id, provider.ProviderName);

                    await _errorStore.MarkProviderDisabledAsync(
                        provider.Id, disabledAt, AllKeysDisabledReason);
                }
            }

            var eventBus = scope.ServiceProvider.GetService<IEventBus>();
            if (eventBus != null)
            {
                await eventBus.PublishAsync(new ProviderKeyDisabledEvent
                {
                    KeyId = keyId,
                    ProviderId = key.ProviderId,
                    Reason = effectiveReason,
                    ErrorType = errorType.ToString(),
                    ErrorMessage = errorMessage ?? reason,
                    DisabledAt = disabledAt,
                    IsAutomatic = isAutomatic,
                    AffectedKeyIds = affectedKeyIds.OrderBy(id => id).ToArray(),
                    ProviderAccountGroup = propagateBalanceFailure
                        ? key.ProviderAccountGroup
                        : (short)0
                });
            }
        }

        public async Task<IReadOnlyList<ProviderErrorInfo>> GetRecentErrorsAsync(
            int? providerId = null,
            int? keyId = null,
            int limit = 100)
        {
            bool hasFilter = providerId.HasValue || keyId.HasValue;
            // When filtering, fetch more entries to compensate for post-filter reduction
            int fetchLimit = hasFilter ? limit * 5 : limit;
            // Cap to prevent excessive Redis reads
            if (fetchLimit > 5000)
                fetchLimit = 5000;

            var entries = await _errorStore.GetRecentErrorsAsync(fetchLimit);
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

                if (errors.Count >= limit)
                    break;
            }

            return errors;
        }

        public async Task<Dictionary<int, int>> GetErrorCountsByKeyAsync(int providerId, TimeSpan window)
        {
            using var scope = _scopeFactory.CreateScope();
            var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();

            var keys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                keyRepo.GetByProviderIdPaginatedAsync, providerId);
            var keyIds = keys.Select(k => k.Id).ToList();

            var errorCounts = await _errorStore.GetErrorCountsByKeysAsync(providerId, keyIds, window);

            return errorCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
        }

        public async Task ClearErrorsForKeyAsync(int keyId, int? providerId = null)
        {
            await _errorStore.ClearErrorsForKeyAsync(keyId, providerId);
        }

        public async Task ClearProviderDisabledAsync(int providerId)
        {
            await _errorStore.ClearProviderDisabledAsync(providerId);
        }

        public async Task<KeyErrorDetails?> GetKeyErrorDetailsAsync(int keyId)
        {
            using var scope = _scopeFactory.CreateScope();
            var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();

            var key = await keyRepo.GetByIdAsync(keyId);
            if (key == null)
                return null;

            var details = await _errorStore.GetKeyErrorDetailsAsync(keyId)
                ?? new KeyErrorDetails { KeyId = keyId };
            details.KeyId = keyId;
            details.KeyName = key.KeyName ?? $"Key {keyId}";
            details.IsDisabled = !key.IsEnabled;

            return details;
        }

        public async Task<ProviderErrorSummary?> GetProviderSummaryAsync(int providerId)
        {
            var summaryData = await _errorStore.GetProviderSummaryAsync(providerId);

            if (summaryData == null)
                return null;

            summaryData.ProviderId = providerId;
            return summaryData;
        }

        public async Task<ErrorStatistics> GetErrorStatisticsAsync(TimeSpan window)
        {
            var stats = await _errorStore.GetErrorStatisticsAsync(window);

            // Count disabled keys
            using (var scope = _scopeFactory.CreateScope())
            {
                var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
                var keys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    keyRepo.GetPaginatedAsync);
                stats.DisabledKeys = keys.Count(k => !k.IsEnabled);
            }

            return stats;
        }
    }
}
