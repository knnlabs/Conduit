using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ConduitLLM.Admin.Models;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing ephemeral master keys for Admin API authentication
    /// </summary>
    public interface IEphemeralMasterKeyService
    {
        /// <summary>
        /// Creates an ephemeral master key
        /// </summary>
        /// <returns>The ephemeral master key response with token and expiration</returns>
        Task<EphemeralMasterKeyResponse> CreateEphemeralMasterKeyAsync();

        /// <summary>
        /// Validates and consumes an ephemeral master key
        /// </summary>
        /// <param name="key">The ephemeral master key to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        Task<bool> ValidateAndConsumeKeyAsync(string key);

        /// <summary>
        /// Marks an ephemeral master key as consumed without deleting it (for streaming)
        /// </summary>
        /// <param name="key">The ephemeral master key to mark as consumed</param>
        /// <returns>True if valid, false otherwise</returns>
        Task<bool> ConsumeKeyAsync(string key);

        /// <summary>
        /// Deletes an ephemeral master key after use
        /// </summary>
        /// <param name="key">The ephemeral master key to delete</param>
        Task DeleteKeyAsync(string key);

        /// <summary>
        /// Checks if a key exists and is valid
        /// </summary>
        /// <param name="key">The ephemeral master key to check</param>
        /// <returns>True if the key exists and is valid</returns>
        Task<bool> KeyExistsAsync(string key);
    }

    /// <summary>
    /// Implementation of the ephemeral master key service for Admin API authentication
    /// </summary>
    public class EphemeralMasterKeyService : IEphemeralMasterKeyService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<EphemeralMasterKeyService> _logger;
        private const string KeyPrefix = "ephemeral:master:";
        private const int TTLSeconds = 300; // 5 minutes

        /// <summary>
        /// Initializes a new instance of the <see cref="EphemeralMasterKeyService"/> class.
        /// </summary>
        /// <param name="cache">The distributed cache</param>
        /// <param name="logger">The logger</param>
        public EphemeralMasterKeyService(
            IDistributedCache cache,
            ILogger<EphemeralMasterKeyService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<EphemeralMasterKeyResponse> CreateEphemeralMasterKeyAsync()
        {
            // Generate a cryptographically secure token
            var key = GenerateSecureToken();
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(TTLSeconds);

            var keyData = new EphemeralMasterKeyData
            {
                Key = key,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = expiresAt,
                IsConsumed = false,
                IsValid = true
            };

            // Store in Redis with TTL
            var cacheKey = $"{KeyPrefix}{key}";
            var serializedData = JsonSerializer.Serialize(keyData);

            await _cache.SetStringAsync(
                cacheKey,
                serializedData,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(TTLSeconds)
                });

            _logger.LogInformation("Created ephemeral master key, expires at {ExpiresAt}", expiresAt);

            return new EphemeralMasterKeyResponse
            {
                EphemeralMasterKey = key,
                ExpiresAt = expiresAt,
                ExpiresInSeconds = TTLSeconds
            };
        }

        /// <inheritdoc />
        public async Task<bool> ValidateAndConsumeKeyAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogDebug("Ephemeral master key validation failed: empty or whitespace key");
                return false;
            }

            var cacheKey = $"{KeyPrefix}{key}";
            var serializedData = await _cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(serializedData))
            {
                _logger.LogWarning("Ephemeral master key not found: {Key}", SanitizeKeyForLogging(key));
                return false;
            }

            var keyData = JsonSerializer.Deserialize<EphemeralMasterKeyData>(serializedData);
            if (keyData == null)
            {
                _logger.LogError("Failed to deserialize ephemeral master key data for key: {Key}", SanitizeKeyForLogging(key));
                return false;
            }

            // Check if already consumed
            if (keyData.IsConsumed)
            {
                _logger.LogWarning("Ephemeral master key already used: {Key}", SanitizeKeyForLogging(key));
                return false;
            }

            // Check expiration
            if (keyData.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Ephemeral master key expired: {Key}, expired at {ExpiresAt}", 
                    SanitizeKeyForLogging(key), keyData.ExpiresAt);
                // Clean up expired key
                await _cache.RemoveAsync(cacheKey);
                return false;
            }

            // Check validity flag
            if (!keyData.IsValid)
            {
                _logger.LogWarning("Ephemeral master key is not valid: {Key}", SanitizeKeyForLogging(key));
                return false;
            }

            // Mark as consumed but keep in cache for cleanup
            keyData.IsConsumed = true;
            serializedData = JsonSerializer.Serialize(keyData);
            
            // Update with short TTL for cleanup tracking
            await _cache.SetStringAsync(
                cacheKey,
                serializedData,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) // Keep for 30s for cleanup
                });

            _logger.LogInformation("Consumed ephemeral master key");

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> ConsumeKeyAsync(string key)
        {
            // Similar to ValidateAndConsumeKeyAsync but doesn't delete
            // Used for streaming where we need to maintain the connection
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var cacheKey = $"{KeyPrefix}{key}";
            var serializedData = await _cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(serializedData))
            {
                _logger.LogWarning("Ephemeral master key not found for consumption: {Key}", SanitizeKeyForLogging(key));
                return false;
            }

            var keyData = JsonSerializer.Deserialize<EphemeralMasterKeyData>(serializedData);
            if (keyData == null)
            {
                return false;
            }

            if (keyData.IsConsumed)
            {
                _logger.LogWarning("Attempted to consume already-used ephemeral master key: {Key}", SanitizeKeyForLogging(key));
                return false;
            }

            if (keyData.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Attempted to consume expired ephemeral master key: {Key}", SanitizeKeyForLogging(key));
                await _cache.RemoveAsync(cacheKey);
                return false;
            }

            if (!keyData.IsValid)
            {
                _logger.LogWarning("Attempted to consume invalid ephemeral master key: {Key}", SanitizeKeyForLogging(key));
                return false;
            }

            // For streaming, immediately delete the key after successful validation
            // The connection itself is now authenticated
            await _cache.RemoveAsync(cacheKey);
            
            _logger.LogInformation("Consumed and deleted ephemeral master key for streaming");

            return true;
        }

        /// <inheritdoc />
        public async Task DeleteKeyAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var cacheKey = $"{KeyPrefix}{key}";
            await _cache.RemoveAsync(cacheKey);
            
            _logger.LogDebug("Deleted ephemeral master key: {Key}", SanitizeKeyForLogging(key));
        }

        /// <inheritdoc />
        public async Task<bool> KeyExistsAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var cacheKey = $"{KeyPrefix}{key}";
            var data = await _cache.GetStringAsync(cacheKey);
            return !string.IsNullOrEmpty(data);
        }

        private static string GenerateSecureToken()
        {
            const int tokenLength = 32; // 256 bits
            var randomBytes = new byte[tokenLength];
            
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            // Convert to URL-safe base64
            var token = Convert.ToBase64String(randomBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            // Add prefix
            return $"emk_{token}";
        }

        private static string SanitizeKeyForLogging(string key)
        {
            // Only show first 10 characters of the key for security
            if (key.Length <= 10)
                return key;
                
            return $"{key.Substring(0, 10)}...";
        }
    }
}