using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing virtual keys through the Admin API
    /// </summary>
    public class AdminVirtualKeyService : IAdminVirtualKeyService
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly ILogger<AdminVirtualKeyService> _logger;
        private const int KeyLengthBytes = 32; // Generate a 256-bit key
        
        /// <summary>
        /// Initializes a new instance of the AdminVirtualKeyService class
        /// </summary>
        /// <param name="virtualKeyRepository">The virtual key repository</param>
        /// <param name="spendHistoryRepository">The spend history repository</param>
        /// <param name="logger">The logger</param>
        public AdminVirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            ILogger<AdminVirtualKeyService> logger)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc />
        public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            _logger.LogInformation("Generating new virtual key with name: {KeyName}", request.KeyName);
            
            // Generate a secure random key
            var keyBytes = new byte[KeyLengthBytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(keyBytes);
            var apiKey = Convert.ToBase64String(keyBytes);
            
            // Add the standard prefix
            apiKey = VirtualKeyConstants.KeyPrefix + apiKey;
            
            // Hash the key for storage
            var keyHash = ComputeSha256Hash(apiKey);
            
            // Create the virtual key entity
            var virtualKey = new VirtualKey
            {
                KeyName = request.KeyName,
                KeyHash = keyHash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt,
                MaxBudget = request.MaxBudget,
                CurrentSpend = 0,
                IsEnabled = true,
                AllowedModels = request.AllowedModels,
                Metadata = request.Metadata,
                BudgetDuration = request.BudgetDuration,
                BudgetStartDate = DetermineBudgetStartDate(request.BudgetDuration),
                RateLimitRpm = request.RateLimitRpm,
                RateLimitRpd = request.RateLimitRpd
            };
            
            // Save to database
            var id = await _virtualKeyRepository.CreateAsync(virtualKey);
            
            // The entity is saved with an ID, now retrieve it to get all properties
            virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null)
            {
                throw new InvalidOperationException($"Failed to retrieve newly created virtual key with ID {id}");
            }
            
            // Initialize spend history
            if (request.MaxBudget.HasValue && request.MaxBudget.Value > 0)
            {
                var history = new VirtualKeySpendHistory
                {
                    VirtualKeyId = virtualKey.Id,
                    Amount = 0,
                    Date = DateTime.UtcNow,
                    Timestamp = DateTime.UtcNow
                };
                
                await _spendHistoryRepository.CreateAsync(history);
            }
            
            // Map to response DTO
            var keyDto = MapToDto(virtualKey);
            
            // Return response with the generated key
            return new CreateVirtualKeyResponseDto
            {
                VirtualKey = apiKey,
                KeyInfo = keyDto
            };
        }
        
        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            _logger.LogInformation("Getting virtual key info for ID: {KeyId}", id);
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return null;
            }
            
            return MapToDto(key);
        }
        
        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            _logger.LogInformation("Listing all virtual keys");
            
            var keys = await _virtualKeyRepository.GetAllAsync();
            
            return keys.ConvertAll(MapToDto);
        }
        
        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            _logger.LogInformation("Updating virtual key with ID: {KeyId}", id);
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return false;
            }
            
            // Update properties
            if (request.KeyName != null)
                key.KeyName = request.KeyName;
                
            if (request.AllowedModels != null)
                key.AllowedModels = request.AllowedModels;
                
            if (request.MaxBudget.HasValue)
                key.MaxBudget = request.MaxBudget;
                
            if (request.BudgetDuration != null)
                key.BudgetDuration = request.BudgetDuration;
                
            if (request.IsEnabled.HasValue)
                key.IsEnabled = request.IsEnabled.Value;
                
            if (request.ExpiresAt.HasValue)
                key.ExpiresAt = request.ExpiresAt;
                
            if (request.Metadata != null)
                key.Metadata = request.Metadata;
                
            if (request.RateLimitRpm.HasValue)
                key.RateLimitRpm = request.RateLimitRpm;
                
            if (request.RateLimitRpd.HasValue)
                key.RateLimitRpd = request.RateLimitRpd;
            
            key.UpdatedAt = DateTime.UtcNow;
            
            // Save changes
            var result = await _virtualKeyRepository.UpdateAsync(key);
            
            return result;
        }
        
        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            _logger.LogInformation("Deleting virtual key with ID: {KeyId}", id);
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return false;
            }
            
            return await _virtualKeyRepository.DeleteAsync(id);
        }
        
        /// <inheritdoc />
        public async Task<bool> ResetSpendAsync(int id)
        {
            _logger.LogInformation("Resetting spend for virtual key with ID: {KeyId}", id);
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return false;
            }
            
            // Record the spend history before resetting
            if (key.CurrentSpend > 0)
            {
                var spendHistory = new VirtualKeySpendHistory
                {
                    VirtualKeyId = key.Id,
                    Amount = key.CurrentSpend,
                    Date = DateTime.UtcNow,
                    Timestamp = DateTime.UtcNow
                };
                
                await _spendHistoryRepository.CreateAsync(spendHistory);
            }
            
            // Reset spend amount
            key.CurrentSpend = 0;
            
            // Reset budget start date based on the budget duration
            if (!string.IsNullOrEmpty(key.BudgetDuration) && 
                !key.BudgetDuration.Equals(VirtualKeyConstants.BudgetPeriods.Total, StringComparison.OrdinalIgnoreCase))
            {
                key.BudgetStartDate = DetermineBudgetStartDate(key.BudgetDuration);
            }
            
            key.UpdatedAt = DateTime.UtcNow;
            
            return await _virtualKeyRepository.UpdateAsync(key);
        }
        
        /// <inheritdoc />
        public async Task<VirtualKeyValidationResult> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            _logger.LogInformation("Validating virtual key and checking if model {Model} is allowed", requestedModel ?? "any");
            
            var result = new VirtualKeyValidationResult { IsValid = false };
            
            if (string.IsNullOrEmpty(key))
            {
                result.ErrorMessage = "Key cannot be empty";
                return result;
            }
            
            if (!key.StartsWith(VirtualKeyConstants.KeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = "Invalid key format: doesn't start with required prefix";
                return result;
            }
            
            // Hash the key for lookup
            string keyHash = ComputeSha256Hash(key);
            
            // Look up the key in the database
            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                result.ErrorMessage = "Key not found";
                return result;
            }
            
            // Check if key is enabled
            if (!virtualKey.IsEnabled)
            {
                result.ErrorMessage = "Key is disabled";
                return result;
            }
            
            // Check expiration
            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                result.ErrorMessage = "Key has expired";
                return result;
            }
            
            // Check budget
            if (virtualKey.MaxBudget.HasValue && virtualKey.CurrentSpend >= virtualKey.MaxBudget.Value)
            {
                result.ErrorMessage = "Budget depleted";
                return result;
            }
            
            // Check if model is allowed (if specified)
            if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
            {
                bool isModelAllowed = IsModelAllowed(requestedModel, virtualKey.AllowedModels);
                
                if (!isModelAllowed)
                {
                    result.ErrorMessage = $"Model {requestedModel} is not allowed for this key";
                    return result;
                }
            }
            
            // All validations passed
            result.IsValid = true;
            result.VirtualKeyId = virtualKey.Id;
            result.KeyName = virtualKey.KeyName;
            result.AllowedModels = virtualKey.AllowedModels;
            result.MaxBudget = virtualKey.MaxBudget;
            result.CurrentSpend = virtualKey.CurrentSpend;
            
            return result;
        }
        
        /// <inheritdoc />
        public async Task<bool> UpdateSpendAsync(int id, decimal cost)
        {
            _logger.LogInformation("Updating spend for virtual key ID {KeyId} by {Cost}", id, cost);
            
            if (cost <= 0)
            {
                return true; // No cost to add, consider it successful
            }
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return false;
            }
            
            // Update spend
            key.CurrentSpend += cost;
            key.UpdatedAt = DateTime.UtcNow;
            
            return await _virtualKeyRepository.UpdateAsync(key);
        }
        
        /// <inheritdoc />
        public async Task<BudgetCheckResult> CheckBudgetAsync(int id)
        {
            _logger.LogInformation("Checking budget period for virtual key ID {KeyId}", id);
            
            var result = new BudgetCheckResult
            {
                WasReset = false,
                NewBudgetStartDate = null
            };
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null || 
                string.IsNullOrEmpty(key.BudgetDuration) || 
                !key.BudgetStartDate.HasValue)
            {
                return result; // No reset needed or possible
            }
            
            DateTime now = DateTime.UtcNow;
            bool needsReset = false;
            
            // Calculate when the current budget period should end
            if (key.BudgetDuration.Equals(VirtualKeyConstants.BudgetPeriods.Monthly, 
                                        StringComparison.OrdinalIgnoreCase))
            {
                // For monthly, check if we're in a new month from the start date
                DateTime startDate = key.BudgetStartDate.Value;
                DateTime periodEnd = new DateTime(
                    startDate.Year + (startDate.Month == 12 ? 1 : 0),
                    startDate.Month == 12 ? 1 : startDate.Month + 1,
                    1,
                    0, 0, 0,
                    DateTimeKind.Utc).AddDays(-1); // Last day of the month
                
                needsReset = now > periodEnd;
            }
            else if (key.BudgetDuration.Equals(VirtualKeyConstants.BudgetPeriods.Daily, 
                                          StringComparison.OrdinalIgnoreCase))
            {
                // For daily, check if we're on a different calendar day (UTC)
                needsReset = now.Date > key.BudgetStartDate.Value.Date;
            }
            
            if (needsReset)
            {
                // Record the spend history before resetting
                if (key.CurrentSpend > 0)
                {
                    var spendHistory = new VirtualKeySpendHistory
                    {
                        VirtualKeyId = key.Id,
                        Amount = key.CurrentSpend,
                        Date = DateTime.UtcNow,
                        Timestamp = DateTime.UtcNow
                    };
                    await _spendHistoryRepository.CreateAsync(spendHistory);
                }
                
                // Reset the spend
                key.CurrentSpend = 0;
                
                // Set new budget start date
                key.BudgetStartDate = DetermineBudgetStartDate(key.BudgetDuration);
                key.UpdatedAt = now;
                
                bool success = await _virtualKeyRepository.UpdateAsync(key);
                
                if (success)
                {
                    result.WasReset = true;
                    result.NewBudgetStartDate = key.BudgetStartDate;
                }
            }
            
            return result;
        }
        
        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> GetValidationInfoAsync(int id)
        {
            _logger.LogInformation("Getting validation info for virtual key ID {KeyId}", id);
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                return null;
            }
            
            return new VirtualKeyValidationInfoDto
            {
                Id = key.Id,
                KeyName = key.KeyName,
                AllowedModels = key.AllowedModels,
                MaxBudget = key.MaxBudget,
                CurrentSpend = key.CurrentSpend,
                BudgetDuration = key.BudgetDuration,
                BudgetStartDate = key.BudgetStartDate,
                IsEnabled = key.IsEnabled,
                ExpiresAt = key.ExpiresAt,
                RateLimitRpm = key.RateLimitRpm,
                RateLimitRpd = key.RateLimitRpd
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
                MaxBudget = key.MaxBudget,
                CurrentSpend = key.CurrentSpend,
                BudgetDuration = key.BudgetDuration,
                BudgetStartDate = key.BudgetStartDate,
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
        
        /// <summary>
        /// Computes a SHA256 hash of the input string
        /// </summary>
        /// <param name="input">The input to hash</param>
        /// <returns>The hash as a hexadecimal string</returns>
        private static string ComputeSha256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            
            var builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            
            return builder.ToString();
        }
        
        /// <summary>
        /// Determines the appropriate budget start date based on budget duration
        /// </summary>
        /// <param name="budgetDuration">The budget duration string</param>
        /// <returns>The appropriate start date for the budget period</returns>
        private static DateTime? DetermineBudgetStartDate(string? budgetDuration)
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
        /// <param name="requestedModel">The model being requested</param>
        /// <param name="allowedModels">Comma-separated string of allowed models</param>
        /// <returns>True if the model is allowed, false otherwise</returns>
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
    }
}