using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for audio encryption and decryption services.
    /// </summary>
    public interface IAudioEncryptionService
    {
        /// <summary>
        /// Encrypts audio data.
        /// </summary>
        /// <param name="audioData">The audio data to encrypt.</param>
        /// <param name="metadata">Optional metadata to include.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Encrypted audio data.</returns>
        Task<EncryptedAudioData> EncryptAudioAsync(
            byte[] audioData,
            AudioEncryptionMetadata? metadata = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts audio data.
        /// </summary>
        /// <param name="encryptedData">The encrypted audio data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Decrypted audio data.</returns>
        Task<byte[]> DecryptAudioAsync(
            EncryptedAudioData encryptedData,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a new encryption key.
        /// </summary>
        /// <returns>A new encryption key.</returns>
        Task<string> GenerateKeyAsync();

        /// <summary>
        /// Validates encrypted audio data integrity.
        /// </summary>
        /// <param name="encryptedData">The encrypted data to validate.</param>
        /// <returns>True if data is valid and unmodified.</returns>
        Task<bool> ValidateIntegrityAsync(EncryptedAudioData encryptedData);
    }

    /// <summary>
    /// Represents encrypted audio data.
    /// </summary>
    public class EncryptedAudioData
    {
        /// <summary>
        /// Gets or sets the encrypted audio bytes.
        /// </summary>
        public byte[] EncryptedBytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the initialization vector.
        /// </summary>
        public byte[] IV { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the key identifier.
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the encryption algorithm used.
        /// </summary>
        public string Algorithm { get; set; } = "AES-256-GCM";

        /// <summary>
        /// Gets or sets the authentication tag.
        /// </summary>
        public byte[] AuthTag { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the encrypted metadata.
        /// </summary>
        public string? EncryptedMetadata { get; set; }

        /// <summary>
        /// Gets or sets when the data was encrypted.
        /// </summary>
        public DateTime EncryptedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Metadata for audio encryption.
    /// </summary>
    public class AudioEncryptionMetadata
    {
        /// <summary>
        /// Gets or sets the audio format.
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original size.
        /// </summary>
        public long OriginalSize { get; set; }

        /// <summary>
        /// Gets or sets the duration in seconds.
        /// </summary>
        public double? DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the virtual key.
        /// </summary>
        public string? VirtualKey { get; set; }

        /// <summary>
        /// Gets or sets custom properties.
        /// </summary>
        public Dictionary<string, string> CustomProperties { get; set; } = new();
    }
}
