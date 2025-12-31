using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Unit tests for CheckConnectionLimitAsync method in RedisSignalRRateLimitService
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "RedisSignalRRateLimitService")]
    [Trait("Feature", "ConnectionLimiting")]
    public class RedisSignalRRateLimitServiceConnectionLimitTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ILogger<RedisSignalRRateLimitService>> _mockLogger;
        private readonly RedisSignalRRateLimitService _service;

        public RedisSignalRRateLimitServiceConnectionLimitTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockLogger = new Mock<ILogger<RedisSignalRRateLimitService>>();

            _mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);

            _service = new RedisSignalRRateLimitService(_mockRedis.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_WithNullHash_ReturnsAllowed()
        {
            // Arrange
            string? nullHash = null;
            const int maxConnections = 100;

            // Act
            var result = await _service.CheckConnectionLimitAsync(nullHash!, maxConnections);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(maxConnections, result.MaxConnections);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_WithEmptyHash_ReturnsAllowed()
        {
            // Arrange
            const string emptyHash = "";
            const int maxConnections = 100;

            // Act
            var result = await _service.CheckConnectionLimitAsync(emptyHash, maxConnections);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(maxConnections, result.MaxConnections);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_UnderLimit_ReturnsAllowed()
        {
            // Arrange
            const string virtualKeyHash = "test-vk-hash";
            const int maxConnections = 100;
            const int currentConnections = 50;

            _mockDatabase
                .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), "count", CommandFlags.None))
                .ReturnsAsync((RedisValue)currentConnections);

            // Act
            var result = await _service.CheckConnectionLimitAsync(virtualKeyHash, maxConnections);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(currentConnections, result.CurrentConnections);
            Assert.Equal(maxConnections, result.MaxConnections);
            Assert.Equal(string.Empty, result.DenialReason);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_AtLimit_ReturnsNotAllowed()
        {
            // Arrange
            const string virtualKeyHash = "test-vk-hash";
            const int maxConnections = 100;
            const int currentConnections = 100; // At limit

            _mockDatabase
                .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), "count", CommandFlags.None))
                .ReturnsAsync((RedisValue)currentConnections);

            // Act
            var result = await _service.CheckConnectionLimitAsync(virtualKeyHash, maxConnections);

            // Assert
            Assert.False(result.IsAllowed);
            Assert.Equal(currentConnections, result.CurrentConnections);
            Assert.Equal(maxConnections, result.MaxConnections);
            Assert.NotEmpty(result.DenialReason);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_OverLimit_ReturnsNotAllowed()
        {
            // Arrange
            const string virtualKeyHash = "test-vk-hash";
            const int maxConnections = 100;
            const int currentConnections = 150; // Over limit

            _mockDatabase
                .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), "count", CommandFlags.None))
                .ReturnsAsync((RedisValue)currentConnections);

            // Act
            var result = await _service.CheckConnectionLimitAsync(virtualKeyHash, maxConnections);

            // Assert
            Assert.False(result.IsAllowed);
            Assert.Equal(currentConnections, result.CurrentConnections);
            Assert.Equal(maxConnections, result.MaxConnections);
        }

        [Theory]
        [InlineData(0, 100, true)]
        [InlineData(50, 100, true)]
        [InlineData(99, 100, true)]
        [InlineData(100, 100, false)]
        [InlineData(101, 100, false)]
        [InlineData(200, 100, false)]
        public async Task CheckConnectionLimitAsync_VariousConnectionCounts_ReturnsExpectedResult(
            int currentConnections, int maxConnections, bool expectedIsAllowed)
        {
            // Arrange
            const string virtualKeyHash = "test-vk-hash";

            _mockDatabase
                .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), "count", CommandFlags.None))
                .ReturnsAsync((RedisValue)currentConnections);

            // Act
            var result = await _service.CheckConnectionLimitAsync(virtualKeyHash, maxConnections);

            // Assert
            Assert.Equal(expectedIsAllowed, result.IsAllowed);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_ReturnsCorrectCurrentConnections()
        {
            // Arrange
            const string virtualKeyHash = "test-vk-hash";
            const int maxConnections = 100;
            const int currentConnections = 75;

            _mockDatabase
                .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), "count", CommandFlags.None))
                .ReturnsAsync((RedisValue)currentConnections);

            // Act
            var result = await _service.CheckConnectionLimitAsync(virtualKeyHash, maxConnections);

            // Assert
            Assert.Equal(currentConnections, result.CurrentConnections);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_ReturnsCorrectMaxConnections()
        {
            // Arrange
            const string virtualKeyHash = "test-vk-hash";
            const int maxConnections = 250;

            _mockDatabase
                .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), "count", CommandFlags.None))
                .ReturnsAsync((RedisValue)50);

            // Act
            var result = await _service.CheckConnectionLimitAsync(virtualKeyHash, maxConnections);

            // Assert
            Assert.Equal(maxConnections, result.MaxConnections);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_WhenLimitReached_DenialReasonContainsConnectionCounts()
        {
            // Arrange
            const string virtualKeyHash = "test-vk-hash";
            const int maxConnections = 100;
            const int currentConnections = 100;

            _mockDatabase
                .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), "count", CommandFlags.None))
                .ReturnsAsync((RedisValue)currentConnections);

            // Act
            var result = await _service.CheckConnectionLimitAsync(virtualKeyHash, maxConnections);

            // Assert
            Assert.Contains("100", result.DenialReason);
            Assert.Contains("Connection limit exceeded", result.DenialReason);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_WhenNoExistingConnections_ReturnsAllowed()
        {
            // Arrange
            const string virtualKeyHash = "test-vk-hash";
            const int maxConnections = 100;

            // When the hash key doesn't exist, HashGetAsync returns RedisValue.Null
            _mockDatabase
                .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), "count", CommandFlags.None))
                .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _service.CheckConnectionLimitAsync(virtualKeyHash, maxConnections);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(0, result.CurrentConnections);
        }

        [Fact]
        public async Task CheckConnectionLimitAsync_WithDifferentMaxLimits_ReturnsCorrectResults()
        {
            // Arrange - Test with different max limits
            const string virtualKeyHash = "test-vk-hash";
            const int currentConnections = 50;

            _mockDatabase
                .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), "count", CommandFlags.None))
                .ReturnsAsync((RedisValue)currentConnections);

            // Act & Assert - With max 100, should be allowed
            var result100 = await _service.CheckConnectionLimitAsync(virtualKeyHash, 100);
            Assert.True(result100.IsAllowed);

            // Act & Assert - With max 50, should not be allowed
            var result50 = await _service.CheckConnectionLimitAsync(virtualKeyHash, 50);
            Assert.False(result50.IsAllowed);

            // Act & Assert - With max 25, should not be allowed
            var result25 = await _service.CheckConnectionLimitAsync(virtualKeyHash, 25);
            Assert.False(result25.IsAllowed);
        }
    }
}
