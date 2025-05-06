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
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for managing virtual keys using the repository pattern
    /// </summary>
    /// <remarks>
    /// The VirtualKeyService is responsible for creating, reading, updating, deleting, and validating 
    /// virtual API keys that are used for authentication and authorization in the LLM API. This implementation
    /// uses the repository pattern for data access, improving testability and separation of concerns.
    /// 
    /// Virtual keys provide several features:
    /// - Authentication for API requests
    /// - Budget tracking and spending limits
    /// - Rate limiting
    /// - Model restrictions
    /// - Expiration dates
    /// </remarks>
    public class VirtualKeyService : ConduitLLM.WebUI.Interfaces.IVirtualKeyService
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly ILogger<VirtualKeyService> _logger;
        private const int KeyLengthBytes = 32; // Generate a 256-bit key

        /// <summary>
        /// Initializes a new instance of the VirtualKeyService
        /// </summary>
        /// <param name="virtualKeyRepository">The virtual key repository</param>
        /// <param name="spendHistoryRepository">The spend history repository</param>
        /// <param name="logger">The logger</param>
        public VirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            ILogger<VirtualKeyService>? logger = null)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<VirtualKeyService>();
        }

        /// <summary>
        /// Generates a new virtual key and saves its hash to the database.
        /// </summary>
        /// <param name="request">The request DTO containing properties for the new virtual key including 
        /// name, allowed models, budget limits, expiration, and rate limiting.</param>
        /// <returns>A response DTO containing both the newly generated key (shown only once) and the key information.</returns>
        /// <remarks>
        /// This method generates a secure random string for the key, computes its hash, and stores the hash 
        /// (not the actual key) in the database. The actual key is returned only once in the response and
        /// should be securely stored by the client as it cannot be retrieved again.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the key entity cannot be retrieved after creation</exception>
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
        /// Retrieves information about a specific virtual key by its ID.
        /// </summary>
        /// <param name="id">The ID of the virtual key to retrieve</param>
        /// <returns>A DTO containing the virtual key information, or null if no key with the specified ID exists</returns>
        /// <remarks>
        /// This method fetches the virtual key entity from the repository and maps it to a DTO that
        /// contains all the key information except the actual key hash. This is used for displaying
        /// key information in the UI and API responses.
        /// </remarks>
        public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            return virtualKey == null ? null : MapToDto(virtualKey);
        }

        /// <summary>
        /// Retrieves a list of all virtual keys in the system.
        /// </summary>
        /// <returns>A list of DTOs containing information about all virtual keys</returns>
        /// <remarks>
        /// This method retrieves all virtual key entities from the repository and maps each one
        /// to a DTO. It's typically used for displaying all keys in the admin UI or for export purposes.
        /// </remarks>
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            var virtualKeys = await _virtualKeyRepository.GetAllAsync();
            return virtualKeys.Select(vk => MapToDto(vk)).ToList();
        }

        /// <summary>
        /// Updates an existing virtual key with new properties.
        /// </summary>
        /// <param name="id">The ID of the virtual key to update</param>
        /// <param name="request">The request DTO containing the properties to update</param>
        /// <returns>True if the update was successful, false if the key doesn't exist or the update failed</returns>
        /// <remarks>
        /// This method updates the properties of an existing virtual key. Only the properties that are
        /// provided in the request DTO will be updated; null properties will not change the existing values.
        /// If the budget duration is updated, the budget start date will be recalculated automatically.
        /// </remarks>
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
        /// <param name="id">The ID of the virtual key to delete</param>
        /// <returns>True if the deletion was successful, false if the key doesn't exist or the deletion failed</returns>
        /// <remarks>
        /// This method permanently removes a virtual key from the database. Any requests using this key
        /// will fail after deletion. Consider disabling keys instead of deleting them if you want to maintain
        /// historical data or might need to re-enable them later.
        /// </remarks>
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            return await _virtualKeyRepository.DeleteAsync(id);
        }

        /// <summary>
        /// Resets the current spend for a virtual key and potentially resets the budget start date.
        /// </summary>
        /// <param name="id">The ID of the virtual key to reset</param>
        /// <returns>True if the reset was successful, false if the key doesn't exist or the reset failed</returns>
        /// <remarks>
        /// This method resets the current spend amount to zero for a virtual key. Before resetting, it records
        /// the current spend in the spend history table for record-keeping. If the key has a budget duration
        /// other than "Total", the budget start date will also be reset to the current period.
        /// </remarks>
        /// <exception cref="Exception">Propagates any exceptions that occur during the database operations</exception>
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
        /// Checks if the key exists, is enabled, has not expired, and has available budget.
        /// </summary>
        /// <param name="key">The virtual key string to validate</param>
        /// <param name="requestedModel">Optional. The model being requested, to check against allowed models</param>
        /// <returns>The VirtualKey entity if validation succeeds, null if validation fails for any reason</returns>
        /// <remarks>
        /// This method performs comprehensive validation of a virtual key, including:
        /// - Checking if the key exists in the database (by comparing hashes)
        /// - Verifying the key is enabled
        /// - Checking if the key has expired
        /// - Ensuring the key has sufficient budget remaining
        /// - Validating that the requested model is allowed for this key (if model restrictions exist)
        /// 
        /// If any validation check fails, null is returned and the reason is logged. If all checks pass,
        /// the full VirtualKey entity is returned for further use.
        /// </remarks>
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
        /// Updates the spend for a specific virtual key by adding the specified cost amount.
        /// </summary>
        /// <param name="keyId">The ID of the virtual key to update</param>
        /// <param name="cost">The cost amount to add to the current spend</param>
        /// <returns>True if the update was successful, false if the key doesn't exist or the update failed</returns>
        /// <remarks>
        /// This method adds the specified cost to the virtual key's current spend. This is typically called
        /// after a successful API request to update the key's usage. If the cost is zero or negative, no
        /// update is performed and the method returns true.
        /// </remarks>
        /// <exception cref="Exception">Propagates any exceptions that occur during the database operations</exception>
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
        /// <param name="keyId">The ID of the virtual key to check</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if a budget reset was performed, false if no reset was needed or possible</returns>
        /// <remarks>
        /// This method is typically called periodically to check if a key's budget period has expired.
        /// It supports different budget periods:
        /// - For monthly budgets, it checks if we're in a new month from the start date
        /// - For daily budgets, it checks if we're on a different calendar day (UTC)
        /// 
        /// If the budget period has expired, it records the current spend in the history table,
        /// resets the current spend to zero, and updates the budget start date to the current period.
        /// </remarks>
        /// <exception cref="Exception">Propagates any exceptions that occur during the database operations</exception>
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
        /// <param name="keyId">The ID of the virtual key to retrieve</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>The full VirtualKey entity if found, null otherwise</returns>
        /// <remarks>
        /// This method retrieves the full VirtualKey entity for use in validation and budget checking.
        /// It returns the raw entity rather than a DTO, which is useful for internal operations that need
        /// access to all properties of the key.
        /// </remarks>
        public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await _virtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
        }

        // --- Helper Methods ---

        /// <summary>
        /// Generates a cryptographically secure random string for use as a virtual key.
        /// </summary>
        /// <returns>A URL-safe Base64 encoded string with the specified prefix.</returns>
        /// <remarks>
        /// This method uses a cryptographically secure random number generator to create
        /// a string of the length specified by KeyLengthBytes. The resulting string is
        /// URL-safe (using '-' and '_' instead of '+' and '/') and has the standard prefix
        /// prepended to identify it as a Conduit virtual key.
        /// </remarks>
        private string GenerateNewKeyString()
        {
            byte[] randomBytes = RandomNumberGenerator.GetBytes(KeyLengthBytes);
            string base64Key = Convert.ToBase64String(randomBytes)
                                    .Replace('+', '-')
                                    .Replace('/', '_')
                                    .TrimEnd('=');
            return VirtualKeyConstants.KeyPrefix + base64Key; // Use constant KeyPrefix
        }

        /// <summary>
        /// Computes a SHA-256 hash of the provided virtual key string.
        /// </summary>
        /// <param name="key">The virtual key string to hash</param>
        /// <returns>A lowercase hexadecimal string representation of the SHA-256 hash</returns>
        /// <remarks>
        /// This method is used for securely storing and validating virtual keys. Only the hash of
        /// the key is stored in the database, never the key itself. During validation, the incoming
        /// key is hashed using the same algorithm and compared to the stored hash.
        /// </remarks>
        private string HashKey(string key)
        {
            using var sha256 = SHA256.Create();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] hashBytes = sha256.ComputeHash(keyBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Determines the appropriate budget start date based on the budget duration.
        /// </summary>
        /// <param name="budgetDuration">The budget duration string ("Total", "Monthly", or "Daily")</param>
        /// <returns>A DateTime representing the start of the current budget period, or null for "Total" budgets</returns>
        /// <remarks>
        /// This method calculates the start date for the current budget period:
        /// - For "Total" budgets (or null duration), returns null (no periodic reset)
        /// - For "Monthly" budgets, returns the first day of the current month
        /// - For other durations (including "Daily"), returns the start of the current day (UTC)
        /// </remarks>
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
        /// Checks if a requested model is allowed based on the AllowedModels string.
        /// </summary>
        /// <param name="requestedModel">The model being requested by the client</param>
        /// <param name="allowedModels">The comma-separated string of allowed models or model patterns</param>
        /// <returns>True if the requested model is allowed, false otherwise</returns>
        /// <remarks>
        /// This method checks if a requested model is allowed by comparing it against the allowedModels list.
        /// It supports both exact matches and wildcard/prefix matches with an asterisk (*) at the end.
        /// For example, "gpt-4*" would match any model starting with "gpt-4".
        /// 
        /// If allowedModels is empty or null, all models are allowed (returns true).
        /// </remarks>
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
        /// Maps a VirtualKey entity to a VirtualKeyDto for client consumption.
        /// </summary>
        /// <param name="entity">The VirtualKey entity to map</param>
        /// <returns>A VirtualKeyDto containing the entity's properties</returns>
        /// <remarks>
        /// This method converts a database entity to a DTO that can be safely returned to clients.
        /// The mapping preserves all relevant properties except sensitive ones like the key hash.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if the entity parameter is null</exception>
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