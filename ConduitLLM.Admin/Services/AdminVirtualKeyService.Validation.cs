using System.Security.Cryptography;
using System.Text;

using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing virtual keys through the Admin API - Validation functionality
    /// </summary>
    public partial class AdminVirtualKeyService
    {
        /// <inheritdoc />
        public async Task<VirtualKeyValidationResult> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            _logger.LogInformation("Validating virtual key and checking if model {Model} is allowed", (requestedModel ?? "any").Replace(Environment.NewLine, ""));

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

            // Check group balance
            var group = await _groupRepository.GetByKeyIdAsync(virtualKey.Id);
            if (group != null && group.Balance <= 0)
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
            // Budget info is now at group level, not included in validation result

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
                VirtualKeyGroupId = key.VirtualKeyGroupId,
                IsEnabled = key.IsEnabled,
                ExpiresAt = key.ExpiresAt,
                RateLimitRpm = key.RateLimitRpm,
                RateLimitRpd = key.RateLimitRpd
            };
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
        /// Checks if a requested model is allowed based on the AllowedModels string
        /// </summary>
        /// <param name="requestedModel">The model being requested</param>
        /// <param name="allowedModels">Comma-separated string of allowed models</param>
        /// <returns>True if the model is allowed, false otherwise</returns>
        private static bool IsModelAllowed(string requestedModel, string allowedModels)
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