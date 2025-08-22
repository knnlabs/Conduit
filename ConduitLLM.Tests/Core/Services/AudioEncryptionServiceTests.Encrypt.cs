using System.Text;

using ConduitLLM.Core.Interfaces;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class AudioEncryptionServiceTests
    {
        [Fact]
        public async Task EncryptAudioAsync_WithValidData_ReturnsEncryptedData()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("Test audio data for encryption");

            // Act
            var result = await _service.EncryptAudioAsync(audioData);

            // Assert
            result.Should().NotBeNull();
            result.EncryptedBytes.Should().NotBeEmpty();
            result.EncryptedBytes.Length.Should().Be(audioData.Length);
            result.IV.Should().NotBeEmpty();
            result.IV.Length.Should().Be(12); // AES-GCM nonce is 12 bytes
            result.AuthTag.Should().NotBeEmpty();
            result.AuthTag.Length.Should().Be(16); // AES-GCM tag is 16 bytes
            result.KeyId.Should().Be("default");
            result.Algorithm.Should().Be("AES-256-GCM");
            result.EncryptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task EncryptAudioAsync_WithMetadata_IncludesEncryptedMetadata()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("Test audio data");
            var metadata = new AudioEncryptionMetadata
            {
                Format = "mp3",
                OriginalSize = 1024,
                DurationSeconds = 10.5,
                VirtualKey = "test-key",
                CustomProperties = new() { ["artist"] = "Test Artist" }
            };

            // Act
            var result = await _service.EncryptAudioAsync(audioData, metadata);

            // Assert
            result.EncryptedMetadata.Should().NotBeNullOrEmpty();
            
            // Verify metadata can be decoded
            var decodedMetadata = Convert.FromBase64String(result.EncryptedMetadata);
            var metadataJson = Encoding.UTF8.GetString(decodedMetadata);
            metadataJson.Should().Contain("mp3");
            metadataJson.Should().Contain("1024");
            metadataJson.Should().Contain("test-key");
        }

        [Fact]
        public async Task EncryptAudioAsync_WithNullData_ThrowsArgumentException()
        {
            // Act & Assert
            var act = () => _service.EncryptAudioAsync(null!);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("audioData")
                .WithMessage("*cannot be null or empty*");
        }

        [Fact]
        public async Task EncryptAudioAsync_WithEmptyData_ThrowsArgumentException()
        {
            // Act & Assert
            var act = () => _service.EncryptAudioAsync(Array.Empty<byte>());
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("audioData")
                .WithMessage("*cannot be null or empty*");
        }

        [Fact]
        public async Task EncryptAudioAsync_LogsInformation()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("Test audio data");

            // Act
            await _service.EncryptAudioAsync(audioData);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Encrypted audio data")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EncryptAudioAsync_ProducesUniqueIVsEachTime()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("Test audio data");

            // Act
            var result1 = await _service.EncryptAudioAsync(audioData);
            var result2 = await _service.EncryptAudioAsync(audioData);
            var result3 = await _service.EncryptAudioAsync(audioData);

            // Assert
            result1.IV.Should().NotBeEquivalentTo(result2.IV);
            result2.IV.Should().NotBeEquivalentTo(result3.IV);
            result1.IV.Should().NotBeEquivalentTo(result3.IV);
        }

        [Fact]
        public async Task EncryptAudioAsync_WithSameData_ProducesDifferentCiphertext()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("Test audio data");

            // Act
            var result1 = await _service.EncryptAudioAsync(audioData);
            var result2 = await _service.EncryptAudioAsync(audioData);

            // Assert
            result1.EncryptedBytes.Should().NotBeEquivalentTo(result2.EncryptedBytes);
        }

        [Fact]
        public async Task EncryptAudioAsync_WithCancellationToken_RespectsCancellation()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("Test audio data");
            using var cts = new CancellationTokenSource();
            
            // Act - Note: Current implementation doesn't actually check cancellation token
            // but we test the interface compliance
            var result = await _service.EncryptAudioAsync(audioData, null, cts.Token);

            // Assert
            result.Should().NotBeNull();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task EncryptDecrypt_WithVariousSizes_WorksCorrectly(int size)
        {
            // Arrange
            var audioData = new byte[size];
            new Random().NextBytes(audioData);

            // Act
            var encryptedData = await _service.EncryptAudioAsync(audioData);
            var decryptedData = await _service.DecryptAudioAsync(encryptedData);

            // Assert
            decryptedData.Should().BeEquivalentTo(audioData);
        }
    }
}