using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;
using MassTransit;

using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// High-performance Virtual Key service with Redis caching and immediate invalidation
    /// Maintains security guarantees while providing ~50x performance improvement
    /// </summary>
    public class CachedApiVirtualKeyService : EventPublishingServiceBase, IVirtualKeyService
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly IVirtualKeyGroupRepository _groupRepository;
        private readonly ConduitLLM.Core.Interfaces.IVirtualKeyCache _cache;
        private readonly ILogger<CachedApiVirtualKeyService> _logger;

        public CachedApiVirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            IVirtualKeyGroupRepository groupRepository,
            ConduitLLM.Core.Interfaces.IVirtualKeyCache cache,
            IPublishEndpoint? publishEndpoint,
            ILogger<CachedApiVirtualKeyService> logger)
            : base(publishEndpoint, logger)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Log event publishing configuration status
            LogEventPublishingConfiguration(nameof(CachedApiVirtualKeyService));
        }

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
                    key.Length > 10 ? key.Substring(0, 10) : key, keyHash);
                
                // Use cache with database fallback
                var virtualKey = await _cache.GetVirtualKeyAsync(keyHash, async hash => 
                {
                    // This fallback only runs on cache miss
                    var dbKey = await _virtualKeyRepository.GetByKeyHashAsync(hash);
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
                    key.Length > 10 ? key.Substring(0, 10) : key, keyHash);
                
                // Use cache with database fallback
                var virtualKey = await _cache.GetVirtualKeyAsync(keyHash, async hash => 
                {
                    // This fallback only runs on cache miss
                    var dbKey = await _virtualKeyRepository.GetByKeyHashAsync(hash);
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
                    _groupRepository, 
                    _logger);

                if (!validationResult.IsValid)
                {
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
                        catch { /* Ignore if no HTTP context */ }
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
        public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            try
            {
                // Generate a new key with prefix
                var keyValue = VirtualKeyUtilities.GenerateSecureKey();
                var keyWithPrefix = $"condt_{keyValue}";
                
                // Hash the key for storage
                var keyHash = VirtualKeyUtilities.HashKey(keyWithPrefix);
                
                // VirtualKeyGroupId is now required
                var groupId = request.VirtualKeyGroupId;

                // Create the virtual key entity
                var virtualKey = new VirtualKey
                {
                    KeyName = request.KeyName ?? string.Empty,
                    KeyHash = keyHash,
                    AllowedModels = request.AllowedModels,
                    VirtualKeyGroupId = groupId,
                    IsEnabled = true,
                    ExpiresAt = request.ExpiresAt,
                    Metadata = request.Metadata,
                    RateLimitRpm = request.RateLimitRpm,
                    RateLimitRpd = request.RateLimitRpd,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                // Save to database
                var createdId = await _virtualKeyRepository.CreateAsync(virtualKey);
                
                if (createdId > 0)
                {
                    // Retrieve the created virtual key to get all populated fields
                    var created = await _virtualKeyRepository.GetByIdAsync(createdId);
                    if (created != null)
                    {
                        _logger.LogInformation("Created new virtual key: {KeyName} (ID: {KeyId})", created.KeyName.Replace(Environment.NewLine, ""), created.Id);
                        
                        // Return the response with the actual key (only shown once)
                        return new CreateVirtualKeyResponseDto
                        {
                            VirtualKey = keyWithPrefix,
                            KeyInfo = VirtualKeyUtilities.MapToDto(created)
                        };
                    }
                }
                
                throw new InvalidOperationException("Failed to create virtual key");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating virtual key");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            try
            {
                var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
                if (virtualKey == null)
                {
                    _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                    return null;
                }
                
                return VirtualKeyUtilities.MapToDto(virtualKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual key info for ID {KeyId}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            try
            {
                var virtualKeys = await _virtualKeyRepository.GetAllAsync();
                return virtualKeys.Select(VirtualKeyUtilities.MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing virtual keys");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            try
            {
                var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
                if (virtualKey == null)
                {
                    _logger.LogWarning("Virtual key with ID {KeyId} not found for update", id);
                    return false;
                }
                
                // Update fields only if provided (null means no change)
                if (request.KeyName != null)
                    virtualKey.KeyName = request.KeyName;
                    
                if (request.AllowedModels != null)
                    virtualKey.AllowedModels = string.IsNullOrEmpty(request.AllowedModels) ? null : request.AllowedModels;
                    
                if (request.VirtualKeyGroupId.HasValue)
                    virtualKey.VirtualKeyGroupId = request.VirtualKeyGroupId.Value;
                    
                if (request.IsEnabled.HasValue)
                    virtualKey.IsEnabled = request.IsEnabled.Value;
                    
                if (request.ExpiresAt.HasValue)
                    virtualKey.ExpiresAt = request.ExpiresAt.Value;
                    
                if (request.Metadata != null)
                    virtualKey.Metadata = string.IsNullOrEmpty(request.Metadata) ? null : request.Metadata;
                    
                if (request.RateLimitRpm.HasValue)
                    virtualKey.RateLimitRpm = request.RateLimitRpm.Value;
                    
                if (request.RateLimitRpd.HasValue)
                    virtualKey.RateLimitRpd = request.RateLimitRpd.Value;
                
                virtualKey.UpdatedAt = DateTime.UtcNow;
                
                var success = await _virtualKeyRepository.UpdateAsync(virtualKey);
                
                if (success)
                {
                    // SECURITY CRITICAL: Immediately invalidate cache
                    await _cache.InvalidateVirtualKeyAsync(virtualKey.KeyHash);
                    _logger.LogInformation("Updated virtual key: {KeyName} (ID: {KeyId})", virtualKey.KeyName.Replace(Environment.NewLine, ""), id);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating virtual key with ID {KeyId}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            try
            {
                var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
                if (virtualKey == null)
                {
                    _logger.LogWarning("Virtual key with ID {KeyId} not found for deletion", id);
                    return false;
                }
                
                var success = await _virtualKeyRepository.DeleteAsync(id);
                
                if (success)
                {
                    // SECURITY CRITICAL: Immediately invalidate cache
                    await _cache.InvalidateVirtualKeyAsync(virtualKey.KeyHash);
                    _logger.LogInformation("Deleted virtual key: {KeyName} (ID: {KeyId})", virtualKey.KeyName.Replace(Environment.NewLine, ""), id);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting virtual key with ID {KeyId}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ResetSpendAsync(int id)
        {
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null) return false;

            try
            {
                // Budget tracking is now at the group level
                // This method is deprecated but kept for compatibility
                _logger.LogWarning("ResetSpendAsync called for key {KeyId} - this operation is no longer supported", id);
                
                // Still invalidate cache for consistency
                await _cache.InvalidateVirtualKeyAsync(virtualKey.KeyHash);
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting spend for virtual key with ID {KeyId}", id);
                return false;
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
                    
                    var virtualKey = await _virtualKeyRepository.GetByIdAsync(keyId);
                    if (virtualKey == null) 
                    {
                        _logger.LogWarning("Virtual key {KeyId} not found for spend update", keyId);
                        return false;
                    }

                    // Get the key's group and adjust its balance
                    var group = await _groupRepository.GetByKeyIdAsync(keyId);
                    if (group == null)
                    {
                        _logger.LogWarning("No group found for virtual key with ID {KeyId}", keyId);
                        return false;
                    }

                    var newBalance = await _groupRepository.AdjustBalanceAsync(group.Id, -cost);
                    
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
        [Obsolete("Budget resets are no longer supported in the bank account model")]
        public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            // Budget resets are no longer supported in the bank account model
            // This method is kept for backward compatibility but always returns false
            _logger.LogDebug("ResetBudgetIfExpiredAsync called for key {KeyId} - no action taken (bank account model)", keyId);
            await Task.CompletedTask;
            return false;
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await _virtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
        }

        /// <summary>
        /// Bulk update spend and invalidate affected keys
        /// NOTE: This method is deprecated in the group-based model
        /// </summary>
        [Obsolete("Bulk spend updates are no longer supported. Spend is tracked at the group level.")]
        public async Task<bool> BulkUpdateSpendAsync(Dictionary<string, decimal> spendUpdates)
        {
            _logger.LogWarning("BulkUpdateSpendAsync is deprecated. Spend tracking is now at the group level.");
            
            // Still invalidate cache for the affected keys
            var keyHashes = spendUpdates.Keys.ToArray();
            await _cache.InvalidateVirtualKeysAsync(keyHashes);
            
            return false;
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