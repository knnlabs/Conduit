using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Admin.Models;
using ConduitLLM.Admin.Services;

namespace ConduitLLM.Admin.Tests.Services
{
    public class EphemeralMasterKeyServiceTests
    {
        private readonly Mock<IDistributedCache> _cacheMock;
        private readonly Mock<ILogger<EphemeralMasterKeyService>> _loggerMock;
        private readonly EphemeralMasterKeyService _service;

        public EphemeralMasterKeyServiceTests()
        {
            _cacheMock = new Mock<IDistributedCache>();
            _loggerMock = new Mock<ILogger<EphemeralMasterKeyService>>();
            _service = new EphemeralMasterKeyService(_cacheMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task CreateEphemeralMasterKeyAsync_ReturnsValidKey()
        {
            // Arrange
            _cacheMock.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                default))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.CreateEphemeralMasterKeyAsync();

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith("emk_", result.EphemeralMasterKey);
            Assert.True(result.ExpiresInSeconds > 0);
            Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
            Assert.True(result.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(301));

            // Verify cache was called
            _cacheMock.Verify(x => x.SetAsync(
                It.Is<string>(key => key.StartsWith("ephemeral:master:emk_")),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                default), Times.Once);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_ValidKey_ReturnsTrue()
        {
            // Arrange
            var key = "emk_valid_key";
            var keyData = new EphemeralMasterKeyData
            {
                Key = key,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(4),
                IsConsumed = false,
                IsValid = true
            };

            var json = JsonSerializer.Serialize(keyData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            _cacheMock.Setup(x => x.GetAsync($"ephemeral:master:{key}", default))
                .ReturnsAsync(bytes);

            _cacheMock.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                default))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(key);

            // Assert
            Assert.True(result);

            // Verify the key was marked as consumed
            _cacheMock.Verify(x => x.SetAsync(
                $"ephemeral:master:{key}",
                It.Is<byte[]>(data => System.Text.Encoding.UTF8.GetString(data).Contains("\"IsConsumed\":true")),
                It.IsAny<DistributedCacheEntryOptions>(),
                default), Times.Once);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_AlreadyConsumed_ReturnsFalse()
        {
            // Arrange
            var key = "emk_consumed_key";
            var keyData = new EphemeralMasterKeyData
            {
                Key = key,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(4),
                IsConsumed = true, // Already consumed
                IsValid = true
            };

            var json = JsonSerializer.Serialize(keyData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            _cacheMock.Setup(x => x.GetAsync($"ephemeral:master:{key}", default))
                .ReturnsAsync(bytes);

            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(key);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_ExpiredKey_ReturnsFalse()
        {
            // Arrange
            var key = "emk_expired_key";
            var keyData = new EphemeralMasterKeyData
            {
                Key = key,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5), // Expired
                IsConsumed = false,
                IsValid = true
            };

            var json = JsonSerializer.Serialize(keyData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            _cacheMock.Setup(x => x.GetAsync($"ephemeral:master:{key}", default))
                .ReturnsAsync(bytes);

            _cacheMock.Setup(x => x.RemoveAsync(
                It.IsAny<string>(),
                default))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(key);

            // Assert
            Assert.False(result);

            // Verify expired key was removed
            _cacheMock.Verify(x => x.RemoveAsync(
                $"ephemeral:master:{key}",
                default), Times.Once);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            var key = "emk_nonexistent";
            _cacheMock.Setup(x => x.GetAsync($"ephemeral:master:{key}", default))
                .ReturnsAsync((byte[])null!);

            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(key);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ConsumeKeyAsync_ValidKey_ReturnsTrue()
        {
            // Arrange
            var key = "emk_consume_key";
            var keyData = new EphemeralMasterKeyData
            {
                Key = key,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(4),
                IsConsumed = false,
                IsValid = true
            };

            var json = JsonSerializer.Serialize(keyData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            _cacheMock.Setup(x => x.GetAsync($"ephemeral:master:{key}", default))
                .ReturnsAsync(bytes);

            _cacheMock.Setup(x => x.RemoveAsync(
                It.IsAny<string>(),
                default))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ConsumeKeyAsync(key);

            // Assert
            Assert.True(result);

            // Verify key was deleted
            _cacheMock.Verify(x => x.RemoveAsync(
                $"ephemeral:master:{key}",
                default), Times.Once);
        }

        [Fact]
        public async Task KeyExistsAsync_ExistingKey_ReturnsTrue()
        {
            // Arrange
            var key = "emk_testkey123";
            var keyData = new EphemeralMasterKeyData
            {
                Key = key,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                IsConsumed = false,
                IsValid = true
            };

            var serializedData = JsonSerializer.Serialize(keyData);
            var serializedBytes = System.Text.Encoding.UTF8.GetBytes(serializedData);
            _cacheMock.Setup(x => x.GetAsync(
                $"ephemeral:master:{key}",
                default))
                .ReturnsAsync(serializedBytes);

            // Act
            var result = await _service.KeyExistsAsync(key);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task KeyExistsAsync_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            var key = "emk_nonexistent";
            _cacheMock.Setup(x => x.GetAsync(
                $"ephemeral:master:{key}",
                default))
                .ReturnsAsync((byte[])null!);

            // Act
            var result = await _service.KeyExistsAsync(key);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteKeyAsync_CallsRemove()
        {
            // Arrange
            var key = "emk_testkey123";
            _cacheMock.Setup(x => x.RemoveAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.DeleteKeyAsync(key);

            // Assert
            _cacheMock.Verify(x => x.RemoveAsync(
                $"ephemeral:master:{key}",
                default), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task ValidateAndConsumeKeyAsync_InvalidInput_ReturnsFalse(string invalidKey)
        {
            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(invalidKey);

            // Assert
            Assert.False(result);

            // Verify cache was never called
            _cacheMock.Verify(x => x.GetAsync(
                It.IsAny<string>(),
                default), Times.Never);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_InvalidFlag_ReturnsFalse()
        {
            // Arrange
            var key = "emk_testkey123";
            var keyData = new EphemeralMasterKeyData
            {
                Key = key,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                IsConsumed = false,
                IsValid = false // Invalid flag
            };

            var serializedData = JsonSerializer.Serialize(keyData);
            var serializedBytes = System.Text.Encoding.UTF8.GetBytes(serializedData);
            _cacheMock.Setup(x => x.GetAsync(
                $"ephemeral:master:{key}",
                default))
                .ReturnsAsync(serializedBytes);

            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(key);

            // Assert
            Assert.False(result);
        }
    }
}