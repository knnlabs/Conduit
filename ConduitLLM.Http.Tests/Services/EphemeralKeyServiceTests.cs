using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.Services;
using ConduitLLM.Http.Models;

namespace ConduitLLM.Http.Tests.Services
{
    public class EphemeralKeyServiceTests
    {
        private readonly Mock<IDistributedCache> _mockCache;
        private readonly Mock<ILogger<EphemeralKeyService>> _mockLogger;
        private readonly EphemeralKeyService _service;

        public EphemeralKeyServiceTests()
        {
            _mockCache = new Mock<IDistributedCache>();
            _mockLogger = new Mock<ILogger<EphemeralKeyService>>();
            _service = new EphemeralKeyService(_mockCache.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CreateEphemeralKeyAsync_Should_Return_Valid_Response()
        {
            // Arrange
            var virtualKeyId = 123;
            var metadata = new EphemeralKeyMetadata
            {
                SourceIP = "127.0.0.1",
                UserAgent = "TestAgent",
                Purpose = "unit-test",
                RequestId = "test-request-id"
            };

            // Act
            var result = await _service.CreateEphemeralKeyAsync(virtualKeyId, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.EphemeralKey);
            Assert.StartsWith("ek_", result.EphemeralKey);
            Assert.Equal(300, result.ExpiresInSeconds);
            Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
            Assert.True(result.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(301));

            // Verify cache was called
            _mockCache.Verify(x => x.SetAsync(
                It.Is<string>(key => key.StartsWith("ephemeral:ek_")),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                default), Times.Once);
        }

        [Fact]
        public async Task CreateEphemeralKeyAsync_Should_Store_Correct_Data_In_Cache()
        {
            // Arrange
            var virtualKeyId = 456;
            string? storedKey = null;
            byte[]? storedData = null;

            _mockCache.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                default))
                .Callback<string, byte[], DistributedCacheEntryOptions, System.Threading.CancellationToken>(
                    (key, data, options, ct) =>
                    {
                        storedKey = key;
                        storedData = data;
                    })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.CreateEphemeralKeyAsync(virtualKeyId, null);

            // Assert
            Assert.NotNull(storedKey);
            Assert.NotNull(storedData);
            Assert.StartsWith("ephemeral:", storedKey);

            // Deserialize and verify stored data
            var json = System.Text.Encoding.UTF8.GetString(storedData);
            var storedKeyData = JsonSerializer.Deserialize<EphemeralKeyData>(json);

            Assert.NotNull(storedKeyData);
            Assert.Equal(result.EphemeralKey, storedKeyData.Key);
            Assert.Equal(virtualKeyId, storedKeyData.VirtualKeyId);
            Assert.False(storedKeyData.IsConsumed);
            Assert.Equal(result.ExpiresAt, storedKeyData.ExpiresAt);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_Should_Return_Null_For_Empty_Key()
        {
            // Act
            var result = await _service.ValidateAndConsumeKeyAsync("");

            // Assert
            Assert.Null(result);
            _mockCache.Verify(x => x.GetAsync(It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_Should_Return_Null_For_NonExistent_Key()
        {
            // Arrange
            var ephemeralKey = "ek_nonexistent";
            _mockCache.Setup(x => x.GetAsync($"ephemeral:{ephemeralKey}", default))
                .ReturnsAsync((byte[])null!);

            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(ephemeralKey);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_Should_Return_VirtualKeyId_For_Valid_Key()
        {
            // Arrange
            var ephemeralKey = "ek_valid_key";
            var virtualKeyId = 789;
            var keyData = new EphemeralKeyData
            {
                Key = ephemeralKey,
                VirtualKeyId = virtualKeyId,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(4),
                IsConsumed = false,
                Metadata = null
            };

            var json = JsonSerializer.Serialize(keyData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            _mockCache.Setup(x => x.GetAsync($"ephemeral:{ephemeralKey}", default))
                .ReturnsAsync(bytes);

            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(ephemeralKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKeyId, result.Value);

            // Verify the key was marked as consumed
            _mockCache.Verify(x => x.SetAsync(
                $"ephemeral:{ephemeralKey}",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                default), Times.Once);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_Should_Return_Null_For_Already_Consumed_Key()
        {
            // Arrange
            var ephemeralKey = "ek_consumed_key";
            var keyData = new EphemeralKeyData
            {
                Key = ephemeralKey,
                VirtualKeyId = 123,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(4),
                IsConsumed = true, // Already consumed
                Metadata = null
            };

            var json = JsonSerializer.Serialize(keyData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            _mockCache.Setup(x => x.GetAsync($"ephemeral:{ephemeralKey}", default))
                .ReturnsAsync(bytes);

            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(ephemeralKey);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ValidateAndConsumeKeyAsync_Should_Return_Null_For_Expired_Key()
        {
            // Arrange
            var ephemeralKey = "ek_expired_key";
            var keyData = new EphemeralKeyData
            {
                Key = ephemeralKey,
                VirtualKeyId = 123,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5), // Expired
                IsConsumed = false,
                Metadata = null
            };

            var json = JsonSerializer.Serialize(keyData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            _mockCache.Setup(x => x.GetAsync($"ephemeral:{ephemeralKey}", default))
                .ReturnsAsync(bytes);

            // Act
            var result = await _service.ValidateAndConsumeKeyAsync(ephemeralKey);

            // Assert
            Assert.Null(result);

            // Verify the expired key was removed
            _mockCache.Verify(x => x.RemoveAsync($"ephemeral:{ephemeralKey}", default), Times.Once);
        }

        [Fact]
        public async Task ConsumeKeyAsync_Should_Delete_Key_After_Validation()
        {
            // Arrange
            var ephemeralKey = "ek_streaming_key";
            var virtualKeyId = 999;
            var keyData = new EphemeralKeyData
            {
                Key = ephemeralKey,
                VirtualKeyId = virtualKeyId,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(4),
                IsConsumed = false,
                Metadata = null
            };

            var json = JsonSerializer.Serialize(keyData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            _mockCache.Setup(x => x.GetAsync($"ephemeral:{ephemeralKey}", default))
                .ReturnsAsync(bytes);

            // Act
            var result = await _service.ConsumeKeyAsync(ephemeralKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKeyId, result.Value);

            // Verify the key was deleted immediately
            _mockCache.Verify(x => x.RemoveAsync($"ephemeral:{ephemeralKey}", default), Times.Once);
        }

        [Fact]
        public async Task DeleteKeyAsync_Should_Remove_Key_From_Cache()
        {
            // Arrange
            var ephemeralKey = "ek_to_delete";

            // Act
            await _service.DeleteKeyAsync(ephemeralKey);

            // Assert
            _mockCache.Verify(x => x.RemoveAsync($"ephemeral:{ephemeralKey}", default), Times.Once);
        }

        [Fact]
        public async Task DeleteKeyAsync_Should_Handle_Empty_Key()
        {
            // Act
            await _service.DeleteKeyAsync("");

            // Assert
            _mockCache.Verify(x => x.RemoveAsync(It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task KeyExistsAsync_Should_Return_True_For_Existing_Key()
        {
            // Arrange
            var ephemeralKey = "ek_existing";
            _mockCache.Setup(x => x.GetAsync($"ephemeral:{ephemeralKey}", default))
                .ReturnsAsync(new byte[] { 1, 2, 3 });

            // Act
            var result = await _service.KeyExistsAsync(ephemeralKey);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task KeyExistsAsync_Should_Return_False_For_NonExistent_Key()
        {
            // Arrange
            var ephemeralKey = "ek_nonexistent";
            _mockCache.Setup(x => x.GetAsync($"ephemeral:{ephemeralKey}", default))
                .ReturnsAsync((byte[])null!);

            // Act
            var result = await _service.KeyExistsAsync(ephemeralKey);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task KeyExistsAsync_Should_Return_False_For_Empty_Key()
        {
            // Act
            var result = await _service.KeyExistsAsync("");

            // Assert
            Assert.False(result);
            _mockCache.Verify(x => x.GetAsync(It.IsAny<string>(), default), Times.Never);
        }
    }
}