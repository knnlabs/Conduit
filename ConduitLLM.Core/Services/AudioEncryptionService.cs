using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implementation of audio encryption service using AES-256-GCM.
    /// </summary>
    public class AudioEncryptionService : IAudioEncryptionService
    {
        private readonly ILogger<AudioEncryptionService> _logger;
        private readonly ConcurrentDictionary<string, byte[]> _keyStore = new(); // In production, use secure key management

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioEncryptionService"/> class.
        /// </summary>
        public AudioEncryptionService(ILogger<AudioEncryptionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<EncryptedAudioData> EncryptAudioAsync(
            byte[] audioData,
            AudioEncryptionMetadata? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));
            }

            try
            {
                using var aesGcm = new AesGcm(await GetOrCreateKeyAsync(), 16); // 16 byte tag size

                // Generate nonce/IV
                var nonce = new byte[12]; // AES-GCM nonce is 12 bytes
                RandomNumberGenerator.Fill(nonce);

                // Prepare ciphertext and tag
                var ciphertext = new byte[audioData.Length];
                var tag = new byte[16]; // AES-GCM tag is 16 bytes

                // Encrypt metadata if provided
                byte[]? associatedData = null;
                string? encryptedMetadata = null;
                if (metadata != null)
                {
                    var metadataJson = JsonSerializer.Serialize(metadata);
                    associatedData = Encoding.UTF8.GetBytes(metadataJson);
                    encryptedMetadata = Convert.ToBase64String(associatedData);
                }

                // Perform encryption
                aesGcm.Encrypt(nonce, audioData, ciphertext, tag, associatedData);

                var result = new EncryptedAudioData
                {
                    EncryptedBytes = ciphertext,
                    IV = nonce,
                    AuthTag = tag,
                    KeyId = "default", // In production, use proper key rotation
                    Algorithm = "AES-256-GCM",
                    EncryptedMetadata = encryptedMetadata,
                    EncryptedAt = DateTime.UtcNow
                };

                _logger.LogDebug(
                    "Encrypted audio data: {Size} bytes -> {EncryptedSize} bytes",
                    audioData.Length,
                    ciphertext.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt audio data");
                throw new InvalidOperationException("Audio encryption failed", ex);
            }
        }

        /// <inheritdoc />
        public async Task<byte[]> DecryptAudioAsync(
            EncryptedAudioData encryptedData,
            CancellationToken cancellationToken = default)
        {
            if (encryptedData == null)
            {
                throw new ArgumentNullException(nameof(encryptedData));
            }

            try
            {
                var key = await GetKeyAsync(encryptedData.KeyId);
                if (key == null)
                {
                    throw new InvalidOperationException($"Key not found: {encryptedData.KeyId}");
                }

                using var aesGcm = new AesGcm(key, 16); // 16 byte tag size

                // Prepare plaintext buffer
                var plaintext = new byte[encryptedData.EncryptedBytes.Length];

                // Prepare associated data if metadata exists
                byte[]? associatedData = null;
                if (!string.IsNullOrEmpty(encryptedData.EncryptedMetadata))
                {
                    associatedData = Convert.FromBase64String(encryptedData.EncryptedMetadata);
                }

                // Perform decryption
                aesGcm.Decrypt(
                    encryptedData.IV,
                    encryptedData.EncryptedBytes,
                    encryptedData.AuthTag,
                    plaintext,
                    associatedData);

                _logger.LogDebug(
                    "Decrypted audio data: {EncryptedSize} bytes -> {Size} bytes",
                    encryptedData.EncryptedBytes.Length,
                    plaintext.Length);

                return plaintext;
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to decrypt audio data - authentication failed");
                throw new InvalidOperationException("Audio decryption failed - data may be tampered", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt audio data");
                throw new InvalidOperationException("Audio decryption failed", ex);
            }
        }

        /// <inheritdoc />
        public Task<string> GenerateKeyAsync()
        {
            var key = new byte[32]; // 256 bits
            RandomNumberGenerator.Fill(key);

            var keyId = Guid.NewGuid().ToString();
            _keyStore.TryAdd(keyId, key);

            _logger.LogInformation("Generated new encryption key: {KeyId}", keyId);

            return Task.FromResult(keyId);
        }

        /// <inheritdoc />
        public async Task<bool> ValidateIntegrityAsync(EncryptedAudioData encryptedData)
        {
            if (encryptedData == null)
            {
                return false;
            }

            try
            {
                // Attempt to decrypt - if it fails, integrity is compromised
                var decrypted = await DecryptAudioAsync(encryptedData);
                return decrypted != null && decrypted.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Audio integrity validation failed - decryption unsuccessful");
                return false;
            }
        }

        private Task<byte[]> GetOrCreateKeyAsync()
        {
            // In production, this would retrieve from secure key management
            var key = _keyStore.GetOrAdd("default", keyId =>
            {
                var newKey = new byte[32];
                RandomNumberGenerator.Fill(newKey);
                return newKey;
            });

            return Task.FromResult(key);
        }

        private Task<byte[]?> GetKeyAsync(string keyId)
        {
            _keyStore.TryGetValue(keyId, out var key);
            return Task.FromResult(key);
        }
    }
}
