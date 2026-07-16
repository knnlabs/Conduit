using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Interfaces;
using VirtualKeyUtilities = ConduitLLM.Configuration.Utilities.VirtualKeyUtilities;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;
using ConduitLLM.Configuration.Messaging;

using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// High-performance Virtual Key service with Redis caching and immediate invalidation
    /// Maintains security guarantees while providing ~50x performance improvement
    /// </summary>
    public class CachedApiVirtualKeyService : VirtualKeyServiceBase, IVirtualKeyService
    {
        private readonly ConduitLLM.Core.Interfaces.IVirtualKeyCache _cache;
        private readonly ILogger<CachedApiVirtualKeyService> _logger;

        public CachedApiVirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            IVirtualKeyGroupRepository groupRepository,
            ConduitLLM.Core.Interfaces.IVirtualKeyCache cache,
            IEventBus? eventBus,
            ILogger<CachedApiVirtualKeyService> logger)
            : base(virtualKeyRepository, groupRepository, spendHistoryRepository, eventBus, logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Virtual Key Hooks (Cache Invalidation)

        protected override async Task OnVirtualKeyUpdatedAsync(VirtualKey key, string[] changedProperties)
        {
            await _cache.InvalidateVirtualKeyAsync(key.KeyHash);
        }

        protected override async Task OnVirtualKeyDeletedAsync(VirtualKey key)
        {
            await _cache.InvalidateVirtualKeyAsync(key.KeyHash);
        }

        #endregion

        /// <summary>
        /// Validates virtual key for authentication only (no balance check)
        /// </summary>
        /// <param name="key">The virtual key to validate</param>
        /// <param name="requestedModel">Optional model to check against allowed models</param>
        /// <returns>The virtual key if valid for authentication, null otherwise</returns>
        public async Task<VirtualKey?> ValidateVirtualKeyForAuthenticationAsync(string key, string? requestedModel = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Empty key provided for authentication validation");
                return null;
            }

            try
            {
                var keyHash = VirtualKeyUtilities.HashKey(key);
                _logger.LogDebug("Validating key for authentication: {KeyPrefix}..., Hash: {Hash}",
                    LoggingSanitizer.S(key.Length > 10 ? key.Substring(0, 10) : key), keyHash);

                // Use cache with database fallback
                var virtualKey = await _cache.GetVirtualKeyAsync(keyHash, async hash =>
                {
                    // This fallback only runs on cache miss
                    var dbKey = await VirtualKeyRepository.GetByKeyHashAsync(hash);
                    _logger.LogDebug("Database fallback executed for Virtual Key authentication validation");
                    return dbKey;
                });

                if (virtualKey == null)
                {
                    _logger.LogWarning("No matching virtual key found for hash: {Hash}", keyHash);
                    return null;
                }

                // Validate without balance check
                var validationResult = await VirtualKeyValidationHelper.ValidateVirtualKeyAsync(
                    virtualKey,
                    requestedModel,
                    checkBalance: false,
                    groupRepository: null,
                    _logger);

                return validationResult.IsValid ? virtualKey : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating virtual key for authentication");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Empty key provided for validation");
                return null;
            }

            try
            {
                var keyHash = VirtualKeyUtilities.HashKey(key);
                _logger.LogDebug("Validating key: {KeyPrefix}..., Hash: {Hash}",
                    LoggingSanitizer.S(key.Length > 10 ? key.Substring(0, 10) : key), keyHash);

                // Use cache with database fallback
                var virtualKey = await _cache.GetVirtualKeyAsync(keyHash, async hash =>
                {
                    // This fallback only runs on cache miss
                    var dbKey = await VirtualKeyRepository.GetByKeyHashAsync(hash);
                    _logger.LogDebug("Database fallback executed for Virtual Key validation");
                    return dbKey;
                });

                if (virtualKey == null)
                {
                    _logger.LogWarning("No matching virtual key found for hash: {Hash}", keyHash);
                    return null;
                }

                // Validate with balance check
                var validationResult = await VirtualKeyValidationHelper.ValidateVirtualKeyAsync(
                    virtualKey,
                    requestedModel,
                    checkBalance: true,
                    GroupRepository,
                    _logger);

                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Virtual key {KeyId} validation failed: {Reason}",
                        virtualKey.Id, validationResult.Reason ?? "unknown");

                    // Handle 402 status code for insufficient balance
                    if (validationResult.StatusCode == 402)
                    {
                        // Note: This violates clean architecture but is pragmatic
                        // TODO: Find a better way to handle this
                        try
                        {
                            var httpContext = new Microsoft.AspNetCore.Http.HttpContextAccessor().HttpContext;
                            if (httpContext != null)
                            {
                                httpContext.Response.StatusCode = 402;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Could not set 402 status — HTTP context not available for insufficient balance response");
                        }
                    }
                    return null;
                }

                return virtualKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating virtual key");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            if (cost <= 0)
            {
                _logger.LogDebug("Spend update for key {KeyId} has zero or negative cost {Cost} - skipping", keyId, cost);
                return true; // No cost to add, consider it successful
            }

            try
            {
                if (IsEventPublishingEnabled)
                {
                    // Event-driven approach - publish SpendUpdateRequested event
                    var requestId = Guid.NewGuid().ToString();

                    await PublishEventAsync(
                        new SpendUpdateRequested
                        {
                            KeyId = keyId,
                            Amount = cost,
                            RequestId = requestId,
                            CorrelationId = Guid.NewGuid().ToString()
                        },
                        $"spend update for key {keyId}",
                        new { KeyId = keyId, Amount = cost, RequestId = requestId });

                    // Event-driven approach returns true immediately - processing happens asynchronously
                    // The SpendUpdateProcessor will handle the actual database update and cache invalidation
                    return true;
                }
                else
                {
                    // FALLBACK: Direct database update approach when event bus not configured
                    _logger.LogDebug("Event publishing not configured - using direct database update for key {KeyId}", keyId);

                    var virtualKey = await VirtualKeyRepository.GetByIdAsync(keyId);
                    if (virtualKey == null)
                    {
                        _logger.LogWarning("Virtual key {KeyId} not found for spend update", keyId);
                        return false;
                    }

                    // Get the key's group and adjust its balance
                    var group = await GroupRepository.GetByKeyIdAsync(keyId);
                    if (group == null)
                    {
                        _logger.LogWarning("No group found for virtual key with ID {KeyId}", keyId);
                        return false;
                    }

                    var newBalance = await GroupRepository.AdjustBalanceAsync(group.Id, -cost);

                    // Invalidate cache after spend update
                    await _cache.InvalidateVirtualKeyAsync(virtualKey.KeyHash);

                    _logger.LogInformation("Updated spend for key ID {KeyId} in group {GroupId}. New balance: {NewBalance}",
                        keyId, group.Id, newBalance);

                    bool success = true;

                    return success;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating spend for key ID {KeyId}.", keyId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await VirtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
        }

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public async Task<ConduitLLM.Core.Interfaces.VirtualKeyCacheStats> GetCacheStatsAsync()
        {
            return await _cache.GetStatsAsync();
        }
    }
}
