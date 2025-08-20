using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing virtual keys through the Admin API - Usage and Maintenance functionality
    /// </summary>
    public partial class AdminVirtualKeyService
    {
        /// <inheritdoc />
        public async Task PerformMaintenanceAsync()
        {
            _logger.LogInformation("Starting virtual key maintenance tasks");

            // TODO: Media Lifecycle Maintenance - Add the following tasks:
            // 1. Clean up expired media (based on MediaRecord.ExpiresAt)
            // 2. Clean up orphaned media (virtual key deleted but media remains)
            // 3. Prune old media based on retention policy (e.g., >90 days)
            // 4. Update storage usage statistics per virtual key
            // See: docs/TODO-Media-Lifecycle-Management.md for implementation plan

            try
            {
                // Get all virtual keys
                var allKeys = await _virtualKeyRepository.GetAllAsync();
                _logger.LogInformation("Processing maintenance for {KeyCount} virtual keys", allKeys.Count());

                int keysDisabled = 0;

                foreach (var key in allKeys)
                {
                    try
                    {
                        // Budget resets are no longer performed in the bank account model
                        // Only check and disable expired keys
                        if (key.IsEnabled && key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
                        {
                            key.IsEnabled = false;
                            key.UpdatedAt = DateTime.UtcNow;

                            var updated = await _virtualKeyRepository.UpdateAsync(key);
                            if (updated)
                            {
                                keysDisabled++;
                                _logger.LogInformation("Disabled expired virtual key {KeyId} ({KeyName})",
                                    key.Id, key.KeyName.Replace(Environment.NewLine, ""));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing maintenance for virtual key {KeyId}", key.Id);
                        // Continue processing other keys even if one fails
                    }
                }

                _logger.LogInformation("Virtual key maintenance completed. Keys disabled: {KeysDisabled}", keysDisabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during virtual key maintenance");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyByIdAsync(int id)
        {
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                return null;
            }
            return MapToDto(key);
        }

        /// <inheritdoc />
        public async Task<VirtualKeyGroupDto?> GetKeyGroupAsync(int id)
        {
            var group = await _groupRepository.GetByKeyIdAsync(id);
            if (group == null)
            {
                return null;
            }

            return new VirtualKeyGroupDto
            {
                Id = group.Id,
                ExternalGroupId = group.ExternalGroupId,
                GroupName = group.GroupName,
                Balance = group.Balance,
                LifetimeCreditsAdded = group.LifetimeCreditsAdded,
                LifetimeSpent = group.LifetimeSpent,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt,
                VirtualKeyCount = group.VirtualKeys?.Count ?? 0
            };
        }

        /// <inheritdoc />
        public async Task<VirtualKeyUsageDto?> GetUsageByKeyAsync(string keyValue)
        {
            if (string.IsNullOrEmpty(keyValue))
            {
                _logger.LogWarning("GetUsageByKeyAsync called with empty key value");
                return null;
            }

            if (!keyValue.StartsWith(VirtualKeyConstants.KeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("GetUsageByKeyAsync called with invalid key format (missing prefix)");
                return null;
            }

            // Hash the key for lookup
            var keyHash = ComputeSha256Hash(keyValue);
            
            // Get the virtual key by hash
            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                _logger.LogInformation("Virtual key not found for hash lookup");
                return null;
            }

            // Get the group information
            var group = await _groupRepository.GetByKeyIdAsync(virtualKey.Id);
            if (group == null)
            {
                _logger.LogWarning("Virtual key group not found for key {KeyId}", virtualKey.Id);
                return null;
            }

            // Get spending history for this specific key
            var spendHistory = await _spendHistoryRepository.GetByVirtualKeyIdAsync(virtualKey.Id);
            var totalRequests = spendHistory.Count();
            // Note: VirtualKeySpendHistory doesn't track individual tokens, only amounts
            // We'll need to estimate based on spending or leave it as 0
            var totalTokens = 0L; // Token tracking would require different data structure
            var lastUsedAt = spendHistory.OrderByDescending(s => s.Timestamp).FirstOrDefault()?.Timestamp;

            return new VirtualKeyUsageDto
            {
                KeyId = virtualKey.Id,
                KeyName = virtualKey.KeyName,
                GroupId = group.Id,
                GroupName = group.GroupName,
                Balance = group.Balance,
                LifetimeCreditsAdded = group.LifetimeCreditsAdded,
                LifetimeSpent = group.LifetimeSpent,
                TotalRequests = totalRequests,
                TotalTokens = totalTokens,
                IsEnabled = virtualKey.IsEnabled,
                ExpiresAt = virtualKey.ExpiresAt,
                CreatedAt = virtualKey.CreatedAt,
                LastUsedAt = lastUsedAt,
                RateLimitRpm = virtualKey.RateLimitRpm,
                RateLimitRpd = virtualKey.RateLimitRpd,
                AllowedModels = virtualKey.AllowedModels
            };
        }

        /// <summary>
        /// Maps a VirtualKey entity to a VirtualKeyDto
        /// </summary>
        /// <param name="key">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        private static VirtualKeyDto MapToDto(VirtualKey key)
        {
            return new VirtualKeyDto
            {
                Id = key.Id,
                KeyName = key.KeyName,
                KeyPrefix = GenerateKeyPrefix(key.KeyHash),
                AllowedModels = key.AllowedModels,
                VirtualKeyGroupId = key.VirtualKeyGroupId,
                IsEnabled = key.IsEnabled,
                ExpiresAt = key.ExpiresAt,
                CreatedAt = key.CreatedAt,
                UpdatedAt = key.UpdatedAt,
                Metadata = key.Metadata,
                RateLimitRpm = key.RateLimitRpm,
                RateLimitRpd = key.RateLimitRpd
            };
        }

        /// <summary>
        /// Generates a key prefix for display purposes
        /// </summary>
        /// <param name="keyHash">The key hash</param>
        /// <returns>A prefix showing part of the key</returns>
        private static string GenerateKeyPrefix(string keyHash)
        {
            // Handle null or empty keyHash to prevent exceptions in tests
            if (string.IsNullOrEmpty(keyHash))
            {
                return "condt_******...";
            }

            // Generate a prefix like "condt_abc123..." from the hash
            // This is for display purposes only
            var prefixLength = Math.Min(6, keyHash.Length);
            var shortPrefix = keyHash.Substring(0, prefixLength).ToLower();
            return $"condt_{shortPrefix}...";
        }
    }
}