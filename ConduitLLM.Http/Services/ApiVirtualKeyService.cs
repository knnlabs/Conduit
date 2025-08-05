using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// API-specific implementation of IVirtualKeyService that directly uses repositories
    /// </summary>
    /// <remarks>
    /// This provides a lightweight implementation of IVirtualKeyService for the API project,
    /// without requiring dependencies on the WebUI project.
    /// </remarks>
    public class ApiVirtualKeyService : IVirtualKeyService
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
                var keyValue = GenerateSecureKey();
                var keyWithPrefix = $"condt_{keyValue}";
                
                // Hash the key for storage
                var keyHash = HashKey(keyWithPrefix);
                
                // Create or get the virtual key group
                int groupId;
                if (request.VirtualKeyGroupId.HasValue)
                {
                    // Use existing group
                    var existingGroup = await _groupRepository.GetByIdAsync(request.VirtualKeyGroupId.Value);
                    if (existingGroup == null)
                    {
                        throw new InvalidOperationException($"Virtual key group {request.VirtualKeyGroupId} not found");
                    }
                    groupId = existingGroup.Id;
                }
                else
                {
                    // Create a new group with the same name as the key
                    var newGroup = new VirtualKeyGroup
                    {
                        GroupName = $"{request.KeyName} Group",
                        Balance = 0, // Start with zero balance
                        LifetimeCreditsAdded = 0,
                        LifetimeSpent = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    groupId = await _groupRepository.CreateAsync(newGroup);
                }

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
                        _logger.LogInformation("Created new virtual key: {KeyName} (ID: {KeyId})", created.KeyName.Replace(Environment.NewLine, ""), created.Id);
                        
                        // Return the response with the actual key (only shown once)
                        return new CreateVirtualKeyResponseDto
                        {
                            VirtualKey = keyWithPrefix,
                            KeyInfo = MapToDto(created)
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
                
                return MapToDto(virtualKey);
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
                var virtualKeys = await _virtualKeyRepository.GetAllAsync();
                return virtualKeys.Select(MapToDto).ToList();
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
                    _logger.LogInformation("Updated virtual key: {KeyName} (ID: {KeyId})", virtualKey.KeyName.Replace(Environment.NewLine, ""), id);
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
                    _logger.LogInformation("Deleted virtual key: {KeyName} (ID: {KeyId})", virtualKey.KeyName.Replace(Environment.NewLine, ""), id);
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
                    await _groupRepository.AdjustBalanceAsync(group.Id, group.LifetimeSpent);
                    
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
            var keyHash = HashKey(key);
            _logger.LogDebug("Validating key for authentication: {KeyPrefix}..., Hash: {Hash}", 
                key.Length > 10 ? key.Substring(0, 10) : key, keyHash);
            
            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                _logger.LogWarning("No matching virtual key found for hash: {Hash}", keyHash);
                return null;
            }

            // Check if key is enabled
            if (!virtualKey.IsEnabled)
            {
                _logger.LogWarning("Virtual key is disabled: {KeyName} (ID: {KeyId})", 
                    virtualKey.KeyName?.Replace(Environment.NewLine, "") ?? "Unknown", virtualKey.Id);
                return null;
            }

            // Check expiration
            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Virtual key has expired: {KeyName} (ID: {KeyId}), expired at {ExpiryDate}",
                    virtualKey.KeyName?.Replace(Environment.NewLine, "") ?? "Unknown", virtualKey.Id, virtualKey.ExpiresAt);
                return null;
            }

            // Check if model is allowed (but skip balance check for authentication)
            if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
            {
                bool isModelAllowed = IsModelAllowed(requestedModel, virtualKey.AllowedModels);
                if (!isModelAllowed)
                {
                    _logger.LogWarning("Virtual key {KeyName} (ID: {KeyId}) attempted to access restricted model: {RequestedModel}",
                        virtualKey.KeyName?.Replace(Environment.NewLine, "") ?? "Unknown", virtualKey.Id, 
                        requestedModel.Replace(Environment.NewLine, ""));
                    return null;
                }
            }

            // Authentication validation passed
            _logger.LogDebug("Virtual key authenticated successfully: {KeyName} (ID: {KeyId})",
                virtualKey.KeyName?.Replace(Environment.NewLine, "") ?? "Unknown", virtualKey.Id);
            return virtualKey;
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
            var keyHash = HashKey(key);
            _logger.LogDebug("Validating key: {KeyPrefix}..., Hash: {Hash}", 
                key.Length > 10 ? key.Substring(0, 10) : key, keyHash);
            
            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                _logger.LogWarning("No matching virtual key found for hash: {Hash}", keyHash);
                return null;
            }

            // Check if key is enabled
            if (!virtualKey.IsEnabled)
            {
                _logger.LogWarning("Virtual key is disabled: {KeyName} (ID: {KeyId})", virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id);
                return null;
            }

            // Check expiration
            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Virtual key has expired: {KeyName} (ID: {KeyId}), expired at {ExpiryDate}",
                    virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id, virtualKey.ExpiresAt);
                return null;
            }

            // Check group balance
            var group = await _groupRepository.GetByIdAsync(virtualKey.VirtualKeyGroupId);
            if (group != null && group.Balance <= 0)
            {
                _logger.LogWarning("Virtual key group budget depleted: {KeyName} (ID: {KeyId}), group {GroupId} has balance {Balance}",
                    virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id, group.Id, group.Balance);
                return null;
            }

            // Check if model is allowed, if model restrictions are in place
            if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
            {
                bool isModelAllowed = IsModelAllowed(requestedModel, virtualKey.AllowedModels);

                if (!isModelAllowed)
                {
                    _logger.LogWarning("Virtual key {KeyName} (ID: {KeyId}) attempted to access restricted model: {RequestedModel}",
                        virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id, requestedModel.Replace(Environment.NewLine, ""));
                    return null;
                }
            }

            // All validations passed
            _logger.LogInformation("Validated virtual key successfully: {KeyName} (ID: {KeyId})",
                virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id);
            return virtualKey;
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
                var newBalance = await _groupRepository.AdjustBalanceAsync(group.Id, -cost);
                
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
        public Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            // Budget duration and periodic resets are no longer supported in the bank account model
            // Groups have a balance that is manually managed - there are no automatic resets
            _logger.LogDebug("ResetBudgetIfExpiredAsync called for key {KeyId} - budget resets are not supported in bank account model", keyId);
            
            return Task.FromResult(false); // No reset performed
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await _virtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
        }

        // Helper method to check if a model is allowed
        private bool IsModelAllowed(string requestedModel, string allowedModels)
        {
            if (string.IsNullOrEmpty(allowedModels))
                return true; // No restrictions

            var allowedModelsList = allowedModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // First check for exact match
            if (allowedModelsList.Any(m => string.Equals(m, requestedModel, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Then check for wildcard/prefix matches
            foreach (var allowedModel in allowedModelsList)
            {
                // Handle wildcards like "gpt-4*" to match any GPT-4 model
                if (allowedModel.EndsWith("*", StringComparison.OrdinalIgnoreCase) &&
                    allowedModel.Length > 1)
                {
                    string prefix = allowedModel.Substring(0, allowedModel.Length - 1);
                    if (requestedModel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
        
        // Helper method to generate a secure random key
        private string GenerateSecureKey()
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[32]; // 256 bits
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "")
                .Substring(0, 32); // Take first 32 characters for consistency
        }
        
        // Helper method to hash a key using SHA256
        private string HashKey(string key)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(key);
            var hash = sha256.ComputeHash(bytes);
            
            // Convert to hex string to match Admin API format
            var builder = new StringBuilder();
            foreach (byte b in hash)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
        
        // Helper method to map VirtualKey entity to VirtualKeyDto
        private VirtualKeyDto MapToDto(VirtualKey virtualKey)
        {
            return new VirtualKeyDto
            {
                Id = virtualKey.Id,
                KeyName = virtualKey.KeyName,
                KeyPrefix = "condt_****", // Don't expose the actual key
                AllowedModels = virtualKey.AllowedModels,
                VirtualKeyGroupId = virtualKey.VirtualKeyGroupId,
                IsEnabled = virtualKey.IsEnabled,
                ExpiresAt = virtualKey.ExpiresAt,
                CreatedAt = virtualKey.CreatedAt,
                UpdatedAt = virtualKey.UpdatedAt,
                Metadata = virtualKey.Metadata,
                RateLimitRpm = virtualKey.RateLimitRpm,
                RateLimitRpd = virtualKey.RateLimitRpd,
                Description = virtualKey.Description,
                // Compatibility properties
                Name = virtualKey.KeyName,
                IsActive = virtualKey.IsEnabled,
                RateLimit = virtualKey.RateLimitRpm
            };
        }
    }
}
