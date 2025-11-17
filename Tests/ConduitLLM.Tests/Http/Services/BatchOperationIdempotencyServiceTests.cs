using System.Text.Json;
using ConduitLLM.Http.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Services
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class BatchOperationIdempotencyServiceTests : TestBase
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ILogger<BatchOperationIdempotencyService>> _mockLogger;
        private readonly BatchOperationIdempotencyService _service;

        public BatchOperationIdempotencyServiceTests(ITestOutputHelper output) : base(output)
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockLogger = new Mock<ILogger<BatchOperationIdempotencyService>>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);

            _service = new BatchOperationIdempotencyService(_mockRedis.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task IsOperationProcessedAsync_WhenTokenExists_ShouldReturnTrue()
        {
            // Arrange
            var token = "test-token-123";
            _mockDatabase.Setup(db => db.KeyExistsAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(token)),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsOperationProcessedAsync(token);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsOperationProcessedAsync_WhenTokenDoesNotExist_ShouldReturnFalse()
        {
            // Arrange
            var token = "test-token-123";
            _mockDatabase.Setup(db => db.KeyExistsAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(token)),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.IsOperationProcessedAsync(token);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsOperationProcessedAsync_WhenRedisThrows_ShouldReturnFalse()
        {
            // Arrange
            var token = "test-token-123";
            _mockDatabase.Setup(db => db.KeyExistsAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

            // Act
            var result = await _service.IsOperationProcessedAsync(token);

            // Assert - fail open
            Assert.False(result);
        }

        [Fact]
        public async Task StoreOperationResultAsync_ShouldSerializeAndStoreResult()
        {
            // Arrange
            var token = "test-token-123";
            var result = new { OperationId = "op-456", Success = true, Count = 10 };
            var capturedValue = string.Empty;
            var capturedExpiry = TimeSpan.Zero;

            _mockDatabase.Setup(db => db.StringSetAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(token)),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()))
                .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                    (k, v, e, ka, w, f) =>
                    {
                        capturedValue = v.ToString();
                        capturedExpiry = e ?? TimeSpan.Zero;
                    })
                .ReturnsAsync(true);

            // Act
            await _service.StoreOperationResultAsync(token, result, TimeSpan.FromHours(1));

            // Assert
            Assert.NotEmpty(capturedValue);
            Assert.Equal(TimeSpan.FromHours(1), capturedExpiry);

            var deserializedResult = JsonSerializer.Deserialize<Dictionary<string, object>>(capturedValue);
            Assert.NotNull(deserializedResult);
        }

        [Fact]
        public async Task GetOperationResultAsync_WhenCacheHit_ShouldReturnDeserializedResult()
        {
            // Arrange
            var token = "test-token-123";
            var originalResult = new TestOperationResult
            {
                OperationId = "op-456",
                Success = true,
                Count = 10
            };
            var serialized = JsonSerializer.Serialize(originalResult);

            _mockDatabase.Setup(db => db.StringGetAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(token)),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(serialized));

            // Act
            var result = await _service.GetOperationResultAsync<TestOperationResult>(token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(originalResult.OperationId, result.OperationId);
            Assert.Equal(originalResult.Success, result.Success);
            Assert.Equal(originalResult.Count, result.Count);
        }

        [Fact]
        public async Task GetOperationResultAsync_WhenCacheMiss_ShouldReturnNull()
        {
            // Arrange
            var token = "test-token-123";
            _mockDatabase.Setup(db => db.StringGetAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(token)),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _service.GetOperationResultAsync<TestOperationResult>(token);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GenerateToken_WithSameInputs_ShouldProduceSameToken()
        {
            // Arrange
            var operationType = "test_operation";
            var param1 = new { Id = 123, Name = "Test" };
            var param2 = "string-param";

            // Act
            var token1 = _service.GenerateToken(operationType, param1, param2);
            var token2 = _service.GenerateToken(operationType, param1, param2);

            // Assert
            Assert.Equal(token1, token2);
        }

        [Fact]
        public void GenerateToken_WithDifferentInputs_ShouldProduceDifferentTokens()
        {
            // Arrange
            var operationType = "test_operation";
            var param1a = new { Id = 123, Name = "Test" };
            var param1b = new { Id = 456, Name = "Different" };

            // Act
            var token1 = _service.GenerateToken(operationType, param1a);
            var token2 = _service.GenerateToken(operationType, param1b);

            // Assert
            Assert.NotEqual(token1, token2);
        }

        [Fact]
        public void GenerateToken_ShouldProduceUrlSafeToken()
        {
            // Arrange
            var operationType = "test_operation";
            var param = new { Id = 123, Name = "Test" };

            // Act
            var token = _service.GenerateToken(operationType, param);

            // Assert
            Assert.DoesNotContain("+", token);
            Assert.DoesNotContain("/", token);
            Assert.DoesNotContain("=", token);
        }

        [Fact]
        public async Task InvalidateTokenAsync_ShouldDeleteKey()
        {
            // Arrange
            var token = "test-token-123";
            _mockDatabase.Setup(db => db.KeyDeleteAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(token)),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _service.InvalidateTokenAsync(token);

            // Assert
            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(token)),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task IsOperationProcessedAsync_WithInvalidToken_ShouldThrow(string? invalidToken)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _service.IsOperationProcessedAsync(invalidToken!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GenerateToken_WithInvalidOperationType_ShouldThrow(string? invalidType)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(
                () => _service.GenerateToken(invalidType!));
        }

        // Helper class for testing
        private class TestOperationResult
        {
            public string OperationId { get; set; } = string.Empty;
            public bool Success { get; set; }
            public int Count { get; set; }
        }
    }
}
