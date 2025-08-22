using System.Text;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class AudioEncryptionServiceTests
    {
        [Fact]
        public async Task GenerateKeyAsync_ReturnsNewKeyId()
        {
            // Act
            var keyId1 = await _service.GenerateKeyAsync();
            var keyId2 = await _service.GenerateKeyAsync();

            // Assert
            keyId1.Should().NotBeNullOrEmpty();
            keyId2.Should().NotBeNullOrEmpty();
            keyId1.Should().NotBe(keyId2);
            Guid.TryParse(keyId1, out _).Should().BeTrue();
            Guid.TryParse(keyId2, out _).Should().BeTrue();
        }

        [Fact]
        public async Task GenerateKeyAsync_LogsInformation()
        {
            // Act
            await _service.GenerateKeyAsync();

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generated new encryption key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithValidData_ReturnsTrue()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Test audio data");
            var encryptedData = await _service.EncryptAudioAsync(originalData);

            // Act
            var isValid = await _service.ValidateIntegrityAsync(encryptedData);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithTamperedData_ReturnsFalse()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Test audio data");
            var encryptedData = await _service.EncryptAudioAsync(originalData);
            
            // Tamper with the data
            encryptedData.EncryptedBytes[0] ^= 0xFF;

            // Act
            var isValid = await _service.ValidateIntegrityAsync(encryptedData);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithNullData_ReturnsFalse()
        {
            // Act
            var isValid = await _service.ValidateIntegrityAsync(null!);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateIntegrityAsync_LogsDebugOnFailure()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Test audio data");
            var encryptedData = await _service.EncryptAudioAsync(originalData);
            encryptedData.AuthTag[0] ^= 0xFF; // Tamper with auth tag

            // Act
            await _service.ValidateIntegrityAsync(encryptedData);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Audio integrity validation failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}