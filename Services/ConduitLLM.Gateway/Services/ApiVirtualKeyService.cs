using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Services;

using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using VirtualKeyUtilities = ConduitLLM.Configuration.Utilities.VirtualKeyUtilities;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// API-specific implementation of IVirtualKeyService that directly uses repositories
    /// </summary>
    /// <remarks>
    /// This provides a lightweight implementation of IVirtualKeyService for the API project,
    /// without requiring dependencies on the WebAdmin project.
    /// </remarks>
    public class ApiVirtualKeyService : Core.Interfaces.IVirtualKeyService
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeyGroupRepository _groupRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly ILogger<ApiVirtualKeyService> _logger;

        /// <summary>
        /// Initializes a new instance of the ApiVirtualKeyService
        /// </summary>
        public ApiVirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeyGroupRepository groupRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            ILogger<ApiVirtualKeyService> logger)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                var existingGroup = await _groupRepository.GetByIdAsync(request.VirtualKeyGroupId);
                if (existingGroup == null)
                {
                    throw new InvalidOperationException($"Virtual key group {request.VirtualKeyGroupId} not found. Ensure the group exists before creating keys.");
                }
                var groupId = existingGroup.Id;

                // Create the virtual key entity
                var virtualKey = new VirtualKey
                {
                    KeyName = request.KeyName,
                    KeyHash = keyHash,
                    AllowedModels = request.AllowedModels,
                    VirtualKeyGroupId = groupId, // Assign to group
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
                        _logger.LogInformation("Created new virtual key: {KeyName} (ID: {KeyId})", LoggingSanitizer.S(created.KeyName), created.Id);
                        
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
                _logger.LogError(ex,
                "Error generating virtual key");
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
                    _logger.LogWarning("Virtual key with ID {KeyId} not found",
                id);
                    return null;
                }
                
                return VirtualKeyUtilities.MapToDto(virtualKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error retrieving virtual key info for ID {KeyId}",
                id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            try
            {
                var virtualKeys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    _virtualKeyRepository.GetPaginatedAsync);
                return [..virtualKeys.Select(VirtualKeyUtilities.MapToDto)];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error listing virtual keys");
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
                    _logger.LogWarning("Virtual key with ID {KeyId} not found for update",
                id);
                    return false;
                }
                
                // Update fields only if provided (null means no change)
                if (request.KeyName != null)
                    virtualKey.KeyName = request.KeyName;
                    
                if (request.AllowedModels != null)
                    virtualKey.AllowedModels = string.IsNullOrEmpty(request.AllowedModels) ? null : request.AllowedModels;
                    
                // Note: Budget changes are now handled at the group level, not the key level
                    
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
                    _logger.LogInformation("Updated virtual key: {KeyName} (ID: {KeyId})", LoggingSanitizer.S(virtualKey.KeyName), id);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error updating virtual key with ID {KeyId}",
                id);
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
                    _logger.LogWarning("Virtual key with ID {KeyId} not found for deletion",
                id);
                    return false;
                }
                
                var success = await _virtualKeyRepository.DeleteAsync(id);
                
                if (success)
                {
                    _logger.LogInformation("Deleted virtual key: {KeyName} (ID: {KeyId})", LoggingSanitizer.S(virtualKey.KeyName), id);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error deleting virtual key with ID {KeyId}",
                id);
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
                // Get the virtual key's group
                var group = await _groupRepository.GetByIdAsync(virtualKey.VirtualKeyGroupId);
                if (group == null)
                {
                    _logger.LogError("Virtual key {KeyId} has invalid group ID {GroupId}", id, virtualKey.VirtualKeyGroupId);
                    return false;
                }

                // Record the spend history before resetting
                if (group.LifetimeSpent > 0)
                {
                    var spendHistory = new VirtualKeySpendHistory
                    {
                        VirtualKeyId = virtualKey.Id,
                        Amount = group.LifetimeSpent,
                        Date = DateTime.UtcNow
                    };
                    await _spendHistoryRepository.CreateAsync(spendHistory);
                }

                // Reset the group's spent amount (add back what was spent)
                if (group.LifetimeSpent > 0)
                {
                    await _groupRepository.AdjustBalanceAsync(
                        group.Id,
                        group.LifetimeSpent,
                        $"Spend reset for virtual key #{virtualKey.Id}",
                        "System",
                        ReferenceType.System,
                        virtualKey.Id.ToString());

                    // Reset lifetime spent
                    group.LifetimeSpent = 0;
                    group.UpdatedAt = DateTime.UtcNow;
                    await _groupRepository.UpdateAsync(group);
                }

                // Update the virtual key timestamp
                virtualKey.UpdatedAt = DateTime.UtcNow;
                return await _virtualKeyRepository.UpdateAsync(virtualKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error resetting spend for virtual key with ID {KeyId}",
                id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> ValidateVirtualKeyForAuthenticationAsync(string key, string? requestedModel = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Empty key provided for authentication validation");
                return null;
            }

            // Hash the incoming key before looking it up
            var keyHash = VirtualKeyUtilities.HashKey(key);
            _logger.LogDebug("Validating key for authentication: {KeyPrefix}..., Hash: {Hash}",
                LoggingSanitizer.S(key.Length > 10 ? key.Substring(0, 10) : key), keyHash);

            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                _logger.LogWarning("No matching virtual key found for hash: {Hash}", keyHash);
                return null;
            }

            // Delegate to shared validation helper (no balance check for authentication)
            var result = await VirtualKeyValidationHelper.ValidateVirtualKeyAsync(
                virtualKey, requestedModel, checkBalance: false, _groupRepository, _logger);

            return result.IsValid ? virtualKey : null;
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Empty key provided for validation");
                return null;
            }

            // Hash the incoming key before looking it up
            var keyHash = VirtualKeyUtilities.HashKey(key);
            _logger.LogDebug("Validating key: {KeyPrefix}..., Hash: {Hash}",
                LoggingSanitizer.S(key.Length > 10 ? key.Substring(0, 10) : key), keyHash);

            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                _logger.LogWarning("No matching virtual key found for hash: {Hash}", keyHash);
                return null;
            }

            // Delegate to shared validation helper (with balance check)
            var result = await VirtualKeyValidationHelper.ValidateVirtualKeyAsync(
                virtualKey, requestedModel, checkBalance: true, _groupRepository, _logger);

            return result.IsValid ? virtualKey : null;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            if (cost <= 0) return true; // No cost to add, consider it successful

            var virtualKey = await _virtualKeyRepository.GetByIdAsync(keyId);
            if (virtualKey == null) return false;

            try
            {
                // Get the key's group
                var group = await _groupRepository.GetByIdAsync(virtualKey.VirtualKeyGroupId);
                if (group == null)
                {
                    _logger.LogError("Virtual key {KeyId} has invalid group ID {GroupId}", keyId, virtualKey.VirtualKeyGroupId);
                    return false;
                }

                // Update the group balance
                var newBalance = await _groupRepository.AdjustBalanceAsync(
                    group.Id,
                    -cost,
                    $"API usage by virtual key #{keyId}",
                    "System",
                    ReferenceType.VirtualKey,
                    keyId.ToString());

                // Update virtual key timestamp
                virtualKey.UpdatedAt = DateTime.UtcNow;
                bool success = await _virtualKeyRepository.UpdateAsync(virtualKey);
                
                if (success)
                {
                    _logger.LogInformation("Updated spend for key ID {KeyId} in group {GroupId}. New balance: {Balance}",
                        keyId, group.Id, newBalance);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error updating spend for key ID {KeyId}.",
                keyId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await _virtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
        }

    }
}
