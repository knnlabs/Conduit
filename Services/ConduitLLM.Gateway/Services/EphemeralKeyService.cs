using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Models;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service for managing reusable, short-lived API keys for direct browser-to-API communication.
    /// Keys remain valid until their cache TTL expires or they are explicitly deleted.
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

        /// <summary>
        /// Gets the virtual key ID for an ephemeral key without consuming it
        /// </summary>
        /// <param name="key">The ephemeral key</param>
        /// <returns>The virtual key ID if valid, null otherwise</returns>
        Task<int?> GetVirtualKeyIdAsync(string key);

        /// <summary>
        /// Gets the full ephemeral key data without consuming it
        /// </summary>
        /// <param name="key">The ephemeral key</param>
        /// <returns>The key data if found, null otherwise</returns>
        Task<EphemeralKeyData?> GetKeyDataAsync(string key);
    }

    /// <summary>
    /// Implementation of the ephemeral key service for Gateway API authentication
    /// </summary>
    public class EphemeralKeyService : EphemeralKeyServiceBase<EphemeralKeyData>, IEphemeralKeyService
    {
        private const int DefaultTTLSeconds = 900; // 15 minutes - longer for video generation which can take several minutes

        // Use a static key for encryption - in production this should come from configuration
        // This is just for data protection at rest in Redis
        // AES-256 requires exactly 32 bytes (256 bits)
        // This base64 string decodes to exactly 32 bytes: "ThisIsA32ByteKeyForAES256Encrypt"
        private static readonly byte[] EncryptionKey = Convert.FromBase64String("VGhpc0lzQTMyQnl0ZUtleUZvckFFUzI1NkVuY3J5cHQ=");

        /// <inheritdoc />
        protected override string KeyPrefix => CacheKeys.Ephemeral.Prefix;

        /// <inheritdoc />
        protected override string TokenPrefix => CacheKeys.Ephemeral.TokenPrefix;

        /// <inheritdoc />
        protected override int TTLSeconds => DefaultTTLSeconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="EphemeralKeyService"/> class.
        /// </summary>
        /// <param name="cache">The distributed cache</param>
        /// <param name="logger">The logger</param>
        public EphemeralKeyService(
            IDistributedCache cache,
            ILogger<EphemeralKeyService> logger)
            : base(cache, logger)
        {
        }

        /// <inheritdoc />
        public async Task<EphemeralKeyResponse> CreateEphemeralKeyAsync(int virtualKeyId, string virtualKey, EphemeralKeyMetadata? metadata = null)
        {
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
                Metadata = metadata,
                EncryptedVirtualKey = encryptedVirtualKey
            };

            await StoreKeyDataAsync(key, keyData);

            Logger.LogInformation("Created ephemeral key for virtual key {VirtualKeyId}, expires at {ExpiresAt}",
                virtualKeyId, expiresAt);

            return new EphemeralKeyResponse
            {
                EphemeralKey = key,
                ExpiresAt = expiresAt,
                ExpiresInSeconds = TTLSeconds
            };
        }

        /// <inheritdoc />
        public async Task<string?> GetVirtualKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Logger.LogDebug("GetVirtualKeyAsync: empty key");
                return null;
            }

            var keyData = await GetKeyDataFromCacheAsync(key);
            if (keyData == null || string.IsNullOrEmpty(keyData.EncryptedVirtualKey))
            {
                Logger.LogWarning("GetVirtualKeyAsync: Ephemeral key not found or no encrypted virtual key: {Key}",
                    SanitizeKeyForLogging(key));
                return null;
            }

            // Check expiration
            if (keyData.ExpiresAt < DateTimeOffset.UtcNow)
            {
                Logger.LogWarning("GetVirtualKeyAsync: Ephemeral key expired: {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            // Decrypt and return the virtual key
            try
            {
                return DecryptString(keyData.EncryptedVirtualKey);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to decrypt virtual key for ephemeral key: {Key}", SanitizeKeyForLogging(key));
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<int?> GetVirtualKeyIdAsync(string key)
        {
            var keyData = await GetKeyDataAsync(key);
            return keyData?.VirtualKeyId;
        }

        /// <inheritdoc />
        public async Task<EphemeralKeyData?> GetKeyDataAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return await GetKeyDataFromCacheAsync(key);
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
