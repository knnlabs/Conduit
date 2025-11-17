using System.Security.Cryptography;
using System.Text;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Static utility methods for virtual key operations
    /// </summary>
    public static class VirtualKeyUtilities
    {
        /// <summary>
        /// Hashes a key using SHA256
        /// </summary>
        /// <param name="key">The key to hash</param>
        /// <returns>Hexadecimal string representation of the hash</returns>
        public static string HashKey(string key)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(key);
            var hash = sha256.ComputeHash(bytes);
            
            // Convert to hex string to match Admin API format
            var builder = new StringBuilder();
            foreach (byte b in hash)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }

        /// <summary>
        /// Generates a secure random key
        /// </summary>
        /// <returns>A 32-character secure random string</returns>
        public static string GenerateSecureKey()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32]; // 256 bits
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "")
                .Substring(0, 32); // Take first 32 characters for consistency
        }

        /// <summary>
        /// Checks if a requested model is allowed based on the allowed models list
        /// </summary>
        /// <param name="requestedModel">The model being requested</param>
        /// <param name="allowedModels">Comma-separated list of allowed models, supports wildcards</param>
        /// <returns>True if the model is allowed, false otherwise</returns>
        public static bool IsModelAllowed(string requestedModel, string allowedModels)
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
        /// Maps VirtualKey entity to VirtualKeyDto
        /// </summary>
        /// <param name="virtualKey">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        public static VirtualKeyDto MapToDto(VirtualKey virtualKey)
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
            };
        }
    }
}