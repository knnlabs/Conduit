using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for AudioEncryptionService to ensure secure encryption/decryption of audio data.
    /// </summary>
    public class AudioEncryptionServiceTests
    {
        private readonly Mock<ILogger<AudioEncryptionService>> _mockLogger;
        private readonly AudioEncryptionService _service;

        public AudioEncryptionServiceTests()
        {
            _mockLogger = new Mock<ILogger<AudioEncryptionService>>();
            _service = new AudioEncryptionService(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AudioEncryptionService(null));
        }

        [Fact]
        public async Task EncryptAudioAsync_WithValidData_ReturnsEncryptedResult()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("test audio content");

            // Act
            var result = await _service.EncryptAudioAsync(audioData);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.EncryptedBytes);
            Assert.Equal(audioData.Length, result.EncryptedBytes.Length);
            Assert.NotNull(result.IV);
            Assert.Equal(12, result.IV.Length); // AES-GCM nonce is 12 bytes
            Assert.NotNull(result.AuthTag);
            Assert.Equal(16, result.AuthTag.Length); // AES-GCM tag is 16 bytes
            Assert.Equal("default", result.KeyId);
            Assert.Equal("AES-256-GCM", result.Algorithm);
            Assert.True(result.EncryptedAt <= DateTime.UtcNow);
            Assert.True(result.EncryptedAt > DateTime.UtcNow.AddMinutes(-1));
            
            // Encrypted data should be different from original
            Assert.NotEqual(audioData, result.EncryptedBytes);
        }

        [Fact]
        public async Task EncryptAudioAsync_WithMetadata_IncludesEncryptedMetadata()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("test audio content");
            var metadata = new AudioEncryptionMetadata
            {
                Format = "wav",
                OriginalSize = 1024000,
                DurationSeconds = 30.0,
                VirtualKey = "key-123",
                CustomProperties = new Dictionary<string, string>
                {
                    { "sampleRate", "44100" }
                }
            };

            // Act
            var result = await _service.EncryptAudioAsync(audioData, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.EncryptedMetadata);
            
            // Verify metadata is properly encoded
            var metadataJson = JsonSerializer.Serialize(metadata);
            var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(metadataJson));
            Assert.Equal(expectedBase64, result.EncryptedMetadata);
        }

        [Fact]
        public async Task EncryptAudioAsync_WithNullAudioData_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.EncryptAudioAsync(null));
        }

        [Fact]
        public async Task EncryptAudioAsync_WithEmptyAudioData_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.EncryptAudioAsync(new byte[0]));
        }

        [Fact]
        public async Task DecryptAudioAsync_WithValidEncryptedData_ReturnsOriginalData()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("test audio content for decryption");
            var encryptedData = await _service.EncryptAudioAsync(originalData);

            // Act
            var decryptedData = await _service.DecryptAudioAsync(encryptedData);

            // Assert
            Assert.NotNull(decryptedData);
            Assert.Equal(originalData, decryptedData);
        }

        [Fact]
        public async Task DecryptAudioAsync_WithMetadata_SuccessfullyDecrypts()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("test audio with metadata");
            var metadata = new AudioEncryptionMetadata
            {
                Format = "mp3",
                OriginalSize = 2048000,
                DurationSeconds = 120.0
            };
            var encryptedData = await _service.EncryptAudioAsync(originalData, metadata);

            // Act
            var decryptedData = await _service.DecryptAudioAsync(encryptedData);

            // Assert
            Assert.NotNull(decryptedData);
            Assert.Equal(originalData, decryptedData);
        }

        [Fact]
        public async Task DecryptAudioAsync_WithNullEncryptedData_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.DecryptAudioAsync(null));
        }

        [Fact]
        public async Task DecryptAudioAsync_WithTamperedData_ThrowsInvalidOperationException()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("test audio content");
            var encryptedData = await _service.EncryptAudioAsync(originalData);
            
            // Tamper with the encrypted data
            encryptedData.EncryptedBytes[0] = (byte)(encryptedData.EncryptedBytes[0] ^ 0xFF);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.DecryptAudioAsync(encryptedData));
            
            Assert.Contains("decryption failed", exception.Message.ToLower());
        }

        [Fact]
        public async Task DecryptAudioAsync_WithTamperedAuthTag_ThrowsInvalidOperationException()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("test audio content");
            var encryptedData = await _service.EncryptAudioAsync(originalData);
            
            // Tamper with the auth tag
            encryptedData.AuthTag[0] = (byte)(encryptedData.AuthTag[0] ^ 0xFF);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.DecryptAudioAsync(encryptedData));
            
            Assert.Contains("decryption failed", exception.Message.ToLower());
        }

        [Fact]
        public async Task DecryptAudioAsync_WithInvalidKeyId_ThrowsInvalidOperationException()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("test audio content");
            var encryptedData = await _service.EncryptAudioAsync(originalData);
            
            // Use invalid key ID
            encryptedData.KeyId = "invalid-key-id";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.DecryptAudioAsync(encryptedData));
            
            Assert.Contains("Key not found", exception.Message);
        }

        [Fact]
        public async Task GenerateKeyAsync_ReturnsValidKeyId()
        {
            // Act
            var keyId = await _service.GenerateKeyAsync();

            // Assert
            Assert.NotNull(keyId);
            Assert.NotEmpty(keyId);
            
            // Should be a valid GUID format
            Assert.True(Guid.TryParse(keyId, out _));
        }

        [Fact]
        public async Task GenerateKeyAsync_CreatesMultipleUniqueKeys()
        {
            // Act
            var keyId1 = await _service.GenerateKeyAsync();
            var keyId2 = await _service.GenerateKeyAsync();

            // Assert
            Assert.NotEqual(keyId1, keyId2);
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithValidData_ReturnsTrue()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("integrity test data");
            var encryptedData = await _service.EncryptAudioAsync(originalData);

            // Act
            var isValid = await _service.ValidateIntegrityAsync(encryptedData);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithTamperedData_ReturnsFalse()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("integrity test data");
            var encryptedData = await _service.EncryptAudioAsync(originalData);
            
            // Tamper with the data
            encryptedData.EncryptedBytes[5] = (byte)(encryptedData.EncryptedBytes[5] ^ 0xFF);

            // Act
            var isValid = await _service.ValidateIntegrityAsync(encryptedData);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithNullData_ReturnsFalse()
        {
            // Act
            var isValid = await _service.ValidateIntegrityAsync(null);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task EncryptDecryptRoundTrip_PreservesDataIntegrity()
        {
            // Arrange
            var testSizes = new[] { 1, 100, 1024, 10000, 100000 }; // Various sizes

            foreach (var size in testSizes)
            {
                var originalData = new byte[size];
                new Random().NextBytes(originalData);

                // Act
                var encryptedData = await _service.EncryptAudioAsync(originalData);
                var decryptedData = await _service.DecryptAudioAsync(encryptedData);

                // Assert
                Assert.Equal(originalData, decryptedData);
            }
        }

        [Fact]
        public async Task EncryptDecryptRoundTrip_WithMetadata_PreservesDataIntegrity()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("test data with complex metadata");
            var metadata = new AudioEncryptionMetadata
            {
                Format = "flac",
                OriginalSize = 10240000,
                DurationSeconds = 5400.0, // 1.5 hours
                VirtualKey = "complex-key-id-with-special-chars-!@#$%",
                CustomProperties = new Dictionary<string, string>
                {
                    { "channels", "2" },
                    { "bitDepth", "24" },
                    { "genre", "classical" },
                    { "sampleRate", "96000" }
                }
            };

            // Act
            var encryptedData = await _service.EncryptAudioAsync(originalData, metadata);
            var decryptedData = await _service.DecryptAudioAsync(encryptedData);

            // Assert
            Assert.Equal(originalData, decryptedData);
            Assert.True(await _service.ValidateIntegrityAsync(encryptedData));
        }

        [Fact]
        public async Task EncryptAudioAsync_MultipleCallsWithSameData_ProducesDifferentCiphertext()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("same input data");

            // Act
            var encrypted1 = await _service.EncryptAudioAsync(audioData);
            var encrypted2 = await _service.EncryptAudioAsync(audioData);

            // Assert
            // Should produce different ciphertext due to random IV/nonce
            Assert.NotEqual(encrypted1.EncryptedBytes, encrypted2.EncryptedBytes);
            Assert.NotEqual(encrypted1.IV, encrypted2.IV);
            Assert.NotEqual(encrypted1.AuthTag, encrypted2.AuthTag);
            
            // But both should decrypt to same original data
            var decrypted1 = await _service.DecryptAudioAsync(encrypted1);
            var decrypted2 = await _service.DecryptAudioAsync(encrypted2);
            Assert.Equal(decrypted1, decrypted2);
            Assert.Equal(audioData, decrypted1);
        }

        [Fact]
        public async Task EncryptAudioAsync_LogsInformation()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("test audio for logging");

            // Act
            await _service.EncryptAudioAsync(audioData);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Encrypted audio data")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task DecryptAudioAsync_LogsInformation()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("test audio for logging");
            var encryptedData = await _service.EncryptAudioAsync(audioData);

            // Act
            await _service.DecryptAudioAsync(encryptedData);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Decrypted audio data")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateKeyAsync_LogsInformation()
        {
            // Act
            await _service.GenerateKeyAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Generated new encryption key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EncryptAudioAsync_WithCancellationToken_SupportsCorectSignature()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("test cancellation");
            var cts = new CancellationTokenSource();

            // Act - should complete before cancellation
            var result = await _service.EncryptAudioAsync(audioData, null, cts.Token);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task DecryptAudioAsync_WithCancellationToken_SupportsCorrectSignature()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("test cancellation");
            var encryptedData = await _service.EncryptAudioAsync(audioData);
            var cts = new CancellationTokenSource();

            // Act - should complete before cancellation
            var result = await _service.DecryptAudioAsync(encryptedData, cts.Token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(audioData, result);
        }
    }
}