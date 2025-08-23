using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ConduitLLM.Http.Models;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for managing ephemeral API keys for direct browser-to-API communication
    /// </summary>
    public interface IEphemeralKeyService
    {
        /// <summary>
        /// Creates an ephemeral key for a virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID to associate with the ephemeral key</param>
        /// <param name="virtualKey">The actual virtual key value to store (will be encrypted)</param>
        /// <param name="metadata">Optional metadata about the request</param>
        /// <returns>The ephemeral key response with token and expiration</returns>
        Task<EphemeralKeyResponse> CreateEphemeralKeyAsync(int virtualKeyId, string virtualKey, EphemeralKeyMetadata? metadata = null);

        /// <summary>
        /// Validates and consumes an ephemeral key
        /// </summary>
        /// <param name="key">The ephemeral key to validate</param>
        /// <returns>The virtual key ID if valid, null otherwise</returns>
        Task<int?> ValidateAndConsumeKeyAsync(string key);

        /// <summary>
        /// Marks an ephemeral key as consumed without deleting it (for streaming)
        /// </summary>
        /// <param name="key">The ephemeral key to mark as consumed</param>
        /// <returns>The virtual key ID if valid, null otherwise</returns>
        Task<int?> ConsumeKeyAsync(string key);

        /// <summary>
        /// Deletes an ephemeral key after use
        /// </summary>
        /// <param name="key">The ephemeral key to delete</param>
        Task DeleteKeyAsync(string key);

        /// <summary>
        /// Checks if a key exists and is valid
        /// </summary>
        /// <param name="key">The ephemeral key to check</param>
        /// <returns>True if the key exists and is valid</returns>
        Task<bool> KeyExistsAsync(string key);

        /// <summary>
        /// Retrieves the virtual key associated with an ephemeral key
        /// </summary>
        /// <param name="key">The ephemeral key</param>
        /// <returns>The virtual key if found and valid, null otherwise</returns>
        Task<string?> GetVirtualKeyAsync(string key);
    }

    public class EphemeralKeyService : IEphemeralKeyService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<EphemeralKeyService> _logger;
        private const string KeyPrefix = "ephemeral:";
        private const int TTLSeconds = 300; // 5 minutes
        
        // Use a static key for encryption - in production this should come from configuration
        // This is just for data protection at rest in Redis
        // AES-256 requires exactly 32 bytes (256 bits)
        // This base64 string decodes to exactly 32 bytes: "ThisIsA32ByteKeyForAES256Encrypt"
        private static readonly byte[] EncryptionKey = Convert.FromBase64String("VGhpc0lzQTMyQnl0ZUtleUZvckFFUzI1NkVuY3J5cHQ=");

        public EphemeralKeyService(
            IDistributedCache cache,
            ILogger<EphemeralKeyService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EphemeralKeyResponse> CreateEphemeralKeyAsync(int virtualKeyId, string virtualKey, EphemeralKeyMetadata? metadata = null)
        {
            // Generate a cryptographically secure token
            var key = GenerateSecureToken();
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(TTLSeconds);

            // Encrypt the virtual key for storage
            var encryptedVirtualKey = EncryptString(virtualKey);
            
            var keyData = new EphemeralKeyData
            {
                Key = key,
                VirtualKeyId = virtualKeyId,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = expiresAt,
                IsConsumed = false,
                Metadata = metadata,
                EncryptedVirtualKey = encryptedVirtualKey
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

            _logger.LogInformation("Created ephemeral key for virtual key {VirtualKeyId}, expires at {ExpiresAt}", 
                virtualKeyId, expiresAt);

            return new EphemeralKeyResponse
            {
                EphemeralKey = key,
                ExpiresAt = expiresAt,
                ExpiresInSeconds = TTLSeconds
            };
        }

        public async Task<int?> ValidateAndConsumeKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogDebug("Ephemeral key validation failed: empty key");
                return null;
            }

            var cacheKey = $"{KeyPrefix}{key}";
            var serializedData = await _cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(serializedData))
            {
                _logger.LogWarning("Ephemeral key not found: {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            var keyData = JsonSerializer.Deserialize<EphemeralKeyData>(serializedData);
            if (keyData == null)
            {
                _logger.LogError("Failed to deserialize ephemeral key data for key: {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            // Check if already consumed
            if (keyData.IsConsumed)
            {
                _logger.LogWarning("Ephemeral key already used: {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            // Check expiration
            if (keyData.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Ephemeral key expired: {Key}, expired at {ExpiresAt}", 
                    SanitizeKeyForLogging(key), keyData.ExpiresAt);
                // Clean up expired key
                await _cache.RemoveAsync(cacheKey);
                return null;
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

            _logger.LogInformation("Consumed ephemeral key for virtual key {VirtualKeyId}", keyData.VirtualKeyId);

            return keyData.VirtualKeyId;
        }

        public async Task<int?> ConsumeKeyAsync(string key)
        {
            // Similar to ValidateAndConsumeKeyAsync but doesn't delete
            // Used for streaming where we need to maintain the connection
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var cacheKey = $"{KeyPrefix}{key}";
            var serializedData = await _cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(serializedData))
            {
                _logger.LogWarning("Ephemeral key not found for consumption: {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            var keyData = JsonSerializer.Deserialize<EphemeralKeyData>(serializedData);
            if (keyData == null)
            {
                return null;
            }

            if (keyData.IsConsumed)
            {
                _logger.LogWarning("Attempted to consume already-used ephemeral key: {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            if (keyData.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Attempted to consume expired ephemeral key: {Key}", SanitizeKeyForLogging(key));
                await _cache.RemoveAsync(cacheKey);
                return null;
            }

            // For streaming, immediately delete the key after successful validation
            // The connection itself is now authenticated
            await _cache.RemoveAsync(cacheKey);
            
            _logger.LogInformation("Consumed and deleted ephemeral key for streaming, virtual key {VirtualKeyId}", 
                keyData.VirtualKeyId);

            return keyData.VirtualKeyId;
        }

        public async Task DeleteKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var cacheKey = $"{KeyPrefix}{key}";
            await _cache.RemoveAsync(cacheKey);
            
            _logger.LogDebug("Deleted ephemeral key: {Key}", SanitizeKeyForLogging(key));
        }

        public async Task<bool> KeyExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
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
            return $"ek_{token}";
        }

        private static string SanitizeKeyForLogging(string key)
        {
            // Only show first 10 characters of the key for security
            if (key.Length <= 10)
                return key;
                
            return $"{key.Substring(0, 10)}...";
        }

        public async Task<string?> GetVirtualKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogDebug("GetVirtualKeyAsync: empty key");
                return null;
            }

            var cacheKey = $"{KeyPrefix}{key}";
            var serializedData = await _cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(serializedData))
            {
                _logger.LogWarning("GetVirtualKeyAsync: Ephemeral key not found: {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            var keyData = JsonSerializer.Deserialize<EphemeralKeyData>(serializedData);
            if (keyData == null || string.IsNullOrEmpty(keyData.EncryptedVirtualKey))
            {
                _logger.LogError("GetVirtualKeyAsync: No encrypted virtual key found for ephemeral key: {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            // Check expiration
            if (keyData.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("GetVirtualKeyAsync: Ephemeral key expired: {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            // Decrypt and return the virtual key
            try
            {
                return DecryptString(keyData.EncryptedVirtualKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt virtual key for ephemeral key: {Key}", SanitizeKeyForLogging(key));
                return null;
            }
        }

        private static string EncryptString(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = EncryptionKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Combine IV and cipher text
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
            Array.Copy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        private static string DecryptString(string cipherText)
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = EncryptionKey;

            // Extract IV from the beginning
            var iv = new byte[aes.IV.Length];
            var cipher = new byte[fullCipher.Length - aes.IV.Length];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}