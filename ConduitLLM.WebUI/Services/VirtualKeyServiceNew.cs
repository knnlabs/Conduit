using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for managing virtual keys using the repository pattern
    /// </summary>
    public class VirtualKeyServiceNew : IVirtualKeyServiceNew
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly ILogger<VirtualKeyServiceNew> _logger;
        private const int KeyLengthBytes = 32; // Generate a 256-bit key

        /// <summary>
        /// Initializes a new instance of the VirtualKeyService
        /// </summary>
        /// <param name="virtualKeyRepository">The virtual key repository</param>
        /// <param name="spendHistoryRepository">The spend history repository</param>
        /// <param name="logger">The logger</param>
        public VirtualKeyServiceNew(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            ILogger<VirtualKeyServiceNew>? logger = null)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<VirtualKeyServiceNew>();
        }

        /// <summary>
        /// Generates a new virtual key and saves its hash to the database.
        /// </summary>
        public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            var newKey = GenerateNewKeyString();
            var keyHash = HashKey(newKey);

            var virtualKeyEntity = new VirtualKey
            {
                KeyName = request.KeyName,
                KeyHash = keyHash,
                AllowedModels = request.AllowedModels,
                MaxBudget = request.MaxBudget,
                BudgetDuration = request.BudgetDuration,
                BudgetStartDate = DetermineBudgetStartDate(request.BudgetDuration),
                IsEnabled = true,
                ExpiresAt = request.ExpiresAt?.ToUniversalTime(),
                Metadata = request.Metadata,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RateLimitRpm = request.RateLimitRpm,
                RateLimitRpd = request.RateLimitRpd
            };

            int newKeyId = await _virtualKeyRepository.CreateAsync(virtualKeyEntity);

            // Reload the entity to ensure we have the correct ID
            virtualKeyEntity = await _virtualKeyRepository.GetByIdAsync(newKeyId);
            
            if (virtualKeyEntity == null)
            {
                throw new InvalidOperationException($"Unable to retrieve virtual key with ID {newKeyId} after creation");
            }

            var keyDto = MapToDto(virtualKeyEntity);

            return new CreateVirtualKeyResponseDto
            {
                VirtualKey = newKey, // Return the actual key only upon creation
                KeyInfo = keyDto
            };
        }

        /// <summary>
        /// Retrieves information about a specific virtual key.
        /// </summary>
        public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            return virtualKey == null ? null : MapToDto(virtualKey);
        }

        /// <summary>
        /// Retrieves a list of all virtual keys.
        /// </summary>
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            var virtualKeys = await _virtualKeyRepository.GetAllAsync();
            return virtualKeys.Select(vk => MapToDto(vk)).ToList();
        }

        /// <summary>
        /// Updates an existing virtual key.
        /// </summary>
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null) return false;

            virtualKey.KeyName = request.KeyName ?? virtualKey.KeyName;
            virtualKey.AllowedModels = request.AllowedModels ?? virtualKey.AllowedModels;
            virtualKey.MaxBudget = request.MaxBudget ?? virtualKey.MaxBudget;
            virtualKey.BudgetDuration = request.BudgetDuration ?? virtualKey.BudgetDuration;
            
            if (request.BudgetDuration != null)
                virtualKey.BudgetStartDate = DetermineBudgetStartDate(request.BudgetDuration);
            
            virtualKey.IsEnabled = request.IsEnabled ?? virtualKey.IsEnabled;
            virtualKey.ExpiresAt = request.ExpiresAt?.ToUniversalTime() ?? virtualKey.ExpiresAt;
            
            if (request.Metadata != null)
                virtualKey.Metadata = string.IsNullOrEmpty(request.Metadata) ? null : request.Metadata;
            
            virtualKey.UpdatedAt = DateTime.UtcNow;
            virtualKey.RateLimitRpm = request.RateLimitRpm;
            virtualKey.RateLimitRpd = request.RateLimitRpd;

            return await _virtualKeyRepository.UpdateAsync(virtualKey);
        }

        /// <summary>
        /// Deletes a virtual key by its ID.
        /// </summary>
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            return await _virtualKeyRepository.DeleteAsync(id);
        }

        /// <summary>
        /// Resets the current spend for a virtual key and potentially resets the budget start date.
        /// </summary>
        public async Task<bool> ResetSpendAsync(int id)
        {
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null) return false;

            try
            {
                // Record the spend history before resetting
                if (virtualKey.CurrentSpend > 0)
                {
                    var spendHistory = new VirtualKeySpendHistory
                    {
                        VirtualKeyId = virtualKey.Id,
                        Amount = virtualKey.CurrentSpend,
                        Date = DateTime.UtcNow
                    };
                    await _spendHistoryRepository.CreateAsync(spendHistory);
                }

                // Reset spend to zero
                virtualKey.CurrentSpend = 0;

                // Reset budget start date based on current budget duration
                if (!string.IsNullOrEmpty(virtualKey.BudgetDuration) &&
                    !virtualKey.BudgetDuration.Equals("Total", StringComparison.OrdinalIgnoreCase))
                {
                    virtualKey.BudgetStartDate = DetermineBudgetStartDate(virtualKey.BudgetDuration);
                }

                virtualKey.UpdatedAt = DateTime.UtcNow;

                return await _virtualKeyRepository.UpdateAsync(virtualKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting spend for virtual key with ID {KeyId}", id);
                throw;
            }
        }

        /// <summary>
        /// Validates a provided virtual key string against stored key hashes.
        /// Checks if the key exists, is enabled, and has not expired.
        /// </summary>
        public async Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Empty key provided for validation");
                return null;
            }

            if (!key.StartsWith(VirtualKeyConstants.KeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid key format: doesn't start with required prefix");
                return null;
            }

            // Hash the key for comparison
            string keyHash = HashKey(key);

            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                _logger.LogWarning("No matching virtual key found");
                return null;
            }

            // Check if key is enabled
            if (!virtualKey.IsEnabled)
            {
                _logger.LogWarning("Virtual key is disabled: {KeyName} (ID: {KeyId})", virtualKey.KeyName, virtualKey.Id);
                return null;
            }

            // Check expiration
            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Virtual key has expired: {KeyName} (ID: {KeyId}), expired at {ExpiryDate}", 
                    virtualKey.KeyName, virtualKey.Id, virtualKey.ExpiresAt);
                return null;
            }

            // Check budget
            if (virtualKey.MaxBudget.HasValue && virtualKey.CurrentSpend >= virtualKey.MaxBudget.Value)
            {
                _logger.LogWarning("Virtual key budget depleted: {KeyName} (ID: {KeyId}), spent {CurrentSpend}, budget {MaxBudget}", 
                    virtualKey.KeyName, virtualKey.Id, virtualKey.CurrentSpend, virtualKey.MaxBudget);
                return null;
            }

            // Check if model is allowed, if model restrictions are in place
            if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
            {
                bool isModelAllowed = IsModelAllowed(requestedModel, virtualKey.AllowedModels);
                
                if (!isModelAllowed)
                {
                    _logger.LogWarning("Virtual key {KeyName} (ID: {KeyId}) attempted to access restricted model: {RequestedModel}", 
                        virtualKey.KeyName, virtualKey.Id, requestedModel);
                    return null;
                }
            }

            // All validations passed
            _logger.LogInformation("Validated virtual key successfully: {KeyName} (ID: {KeyId})", 
                virtualKey.KeyName, virtualKey.Id);
            return virtualKey;
        }

        /// <summary>
        /// Updates the spend for a specific virtual key.
        /// </summary>
        public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            if (cost <= 0) return true; // No cost to add, consider it successful

            var virtualKey = await _virtualKeyRepository.GetByIdAsync(keyId);
            if (virtualKey == null) return false;

            try
            {
                // Update spend and timestamp
                virtualKey.CurrentSpend += cost;
                virtualKey.UpdatedAt = DateTime.UtcNow;

                bool success = await _virtualKeyRepository.UpdateAsync(virtualKey);
                if (success)
                {
                    _logger.LogInformation("Updated spend for key ID {KeyId}. New spend: {CurrentSpend}", keyId, virtualKey.CurrentSpend);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating spend for key ID {KeyId}.", keyId);
                throw;
            }
        }

        /// <summary>
        /// Checks if the budget period for a key has expired based on its duration and start date,
        /// and resets the spend and start date if necessary.
        /// </summary>
        public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
            if (virtualKey == null || 
                string.IsNullOrEmpty(virtualKey.BudgetDuration) || 
                !virtualKey.BudgetStartDate.HasValue)
            {
                // Can't reset budget if key doesn't exist or has no budget duration/start date
                return false;
            }

            DateTime now = DateTime.UtcNow;
            bool needsReset = false;

            // Calculate when the current budget period should end
            if (virtualKey.BudgetDuration.Equals(VirtualKeyConstants.BudgetPeriods.Monthly, 
                                              StringComparison.OrdinalIgnoreCase))
            {
                // For monthly, check if we're in a new month from the start date
                DateTime startDate = virtualKey.BudgetStartDate.Value;
                DateTime periodEnd = new DateTime(
                    startDate.Year + (startDate.Month == 12 ? 1 : 0),
                    startDate.Month == 12 ? 1 : startDate.Month + 1,
                    1,
                    0, 0, 0,
                    DateTimeKind.Utc).AddDays(-1); // Last day of the month
                
                needsReset = now > periodEnd;
            }
            else if (virtualKey.BudgetDuration.Equals(VirtualKeyConstants.BudgetPeriods.Daily, 
                                                  StringComparison.OrdinalIgnoreCase))
            {
                // For daily, check if we're on a different calendar day (UTC)
                needsReset = now.Date > virtualKey.BudgetStartDate.Value.Date;
            }

            if (needsReset)
            {
                try
                {
                    // Record the spend history before resetting
                    if (virtualKey.CurrentSpend > 0)
                    {
                        var spendHistory = new VirtualKeySpendHistory
                        {
                            VirtualKeyId = virtualKey.Id,
                            Amount = virtualKey.CurrentSpend,
                            Date = DateTime.UtcNow
                        };
                        await _spendHistoryRepository.CreateAsync(spendHistory, cancellationToken);
                    }

                    _logger.LogInformation(
                        "Resetting budget for key ID {KeyId}. Previous spend: {PreviousSpend}, Previous start date: {PreviousStartDate}", 
                        keyId, virtualKey.CurrentSpend, virtualKey.BudgetStartDate);
                    
                    // Reset the spend
                    virtualKey.CurrentSpend = 0;
                    
                    // Set new budget start date
                    virtualKey.BudgetStartDate = DetermineBudgetStartDate(virtualKey.BudgetDuration);
                    virtualKey.UpdatedAt = now;
                    
                    bool success = await _virtualKeyRepository.UpdateAsync(virtualKey, cancellationToken);
                    
                    if (success)
                    {
                        _logger.LogInformation(
                            "Budget reset completed for key ID {KeyId}. New start date: {NewStartDate}", 
                            keyId, virtualKey.BudgetStartDate);
                    }
                    
                    return success;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting budget for key ID {KeyId}", keyId);
                    throw;
                }
            }

            return false; // No reset needed
        }

        /// <summary>
        /// Gets detailed info about a virtual key for validation and budget checking.
        /// </summary>
        public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await _virtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
        }

        // --- Helper Methods ---

        private string GenerateNewKeyString()
        {
            byte[] randomBytes = RandomNumberGenerator.GetBytes(KeyLengthBytes);
            string base64Key = Convert.ToBase64String(randomBytes)
                                    .Replace('+', '-')
                                    .Replace('/', '_')
                                    .TrimEnd('=');
            return VirtualKeyConstants.KeyPrefix + base64Key; // Use constant KeyPrefix
        }

        private string HashKey(string key)
        {
            using var sha256 = SHA256.Create();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] hashBytes = sha256.ComputeHash(keyBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private DateTime? DetermineBudgetStartDate(string? budgetDuration)
        {
            if (string.IsNullOrEmpty(budgetDuration) || 
                budgetDuration.Equals(VirtualKeyConstants.BudgetPeriods.Total, StringComparison.OrdinalIgnoreCase))
                return null;
            if (budgetDuration.Equals(VirtualKeyConstants.BudgetPeriods.Monthly, StringComparison.OrdinalIgnoreCase))
                return new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return DateTime.UtcNow.Date; // Default to start of current day (UTC)
        }

        /// <summary>
        /// Checks if a requested model is allowed based on the AllowedModels string
        /// </summary>
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

        /// <summary>
        /// Maps a VirtualKey entity to a VirtualKeyDto.
        /// </summary>
        private VirtualKeyDto MapToDto(VirtualKey entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
            
            return new VirtualKeyDto
            {
                Id = entity.Id,
                KeyName = entity.KeyName,
                AllowedModels = entity.AllowedModels,
                MaxBudget = entity.MaxBudget,
                CurrentSpend = entity.CurrentSpend,
                BudgetDuration = entity.BudgetDuration,
                BudgetStartDate = entity.BudgetStartDate,
                IsEnabled = entity.IsEnabled,
                ExpiresAt = entity.ExpiresAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Metadata = entity.Metadata,
                RateLimitRpm = entity.RateLimitRpm,
                RateLimitRpd = entity.RateLimitRpd
            };
        }
    }
}