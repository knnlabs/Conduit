using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Tests for RedisWebhookDeliveryTracker
    /// </summary>
    public class RedisWebhookDeliveryTrackerTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ITransaction> _mockTransaction;
        private readonly Mock<ILogger<RedisWebhookDeliveryTracker>> _mockLogger;
        private readonly RedisWebhookDeliveryTracker _tracker;
        
        public RedisWebhookDeliveryTrackerTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockTransaction = new Mock<ITransaction>();
            _mockLogger = new Mock<ILogger<RedisWebhookDeliveryTracker>>();
            
            _mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);
            
            _mockDatabase.Setup(x => x.CreateTransaction(It.IsAny<object>()))
                .Returns(_mockTransaction.Object);
            
            _tracker = new RedisWebhookDeliveryTracker(_mockRedis.Object, _mockLogger.Object);
        }
        
        [Fact]
        public async Task IsDeliveredAsync_WhenKeyExists_ShouldReturnTrue()
        {
            // Arrange
            var deliveryKey = "task123:TaskCompleted:msg456";
            _mockDatabase.Setup(x => x.KeyExistsAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(deliveryKey)),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            
            // Act
            var result = await _tracker.IsDeliveredAsync(deliveryKey);
            
            // Assert
            Assert.True(result);
            _mockDatabase.Verify(x => x.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(deliveryKey)),
                It.IsAny<CommandFlags>()), Times.Once);
        }
        
        [Fact]
        public async Task IsDeliveredAsync_WhenKeyDoesNotExist_ShouldReturnFalse()
        {
            // Arrange
            var deliveryKey = "task123:TaskCompleted:msg456";
            _mockDatabase.Setup(x => x.KeyExistsAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);
            
            // Act
            var result = await _tracker.IsDeliveredAsync(deliveryKey);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task IsDeliveredAsync_WhenRedisThrows_ShouldReturnFalse()
        {
            // Arrange
            var deliveryKey = "task123:TaskCompleted:msg456";
            _mockDatabase.Setup(x => x.KeyExistsAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));
            
            // Act
            var result = await _tracker.IsDeliveredAsync(deliveryKey);
            
            // Assert
            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error checking webhook delivery status")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task MarkDeliveredAsync_WhenSuccessful_ShouldCommitTransaction()
        {
            // Arrange
            var deliveryKey = "task123:TaskCompleted:msg456";
            var webhookUrl = "https://example.com/webhook";
            
            _mockTransaction.Setup(x => x.ExecuteAsync(It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            
            // Act
            await _tracker.MarkDeliveredAsync(deliveryKey, webhookUrl);
            
            // Assert
            _mockTransaction.Verify(x => x.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(deliveryKey)),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                false,
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
            
            _mockTransaction.Verify(x => x.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(webhookUrl)),
                "delivered",
                1,
                It.IsAny<CommandFlags>()), Times.Once);
            
            _mockTransaction.Verify(x => x.ExecuteAsync(It.IsAny<CommandFlags>()), Times.Once);
        }
        
        [Fact]
        public async Task RecordFailureAsync_ShouldUpdateFailureStats()
        {
            // Arrange
            var deliveryKey = "task123:TaskFailed:msg456";
            var webhookUrl = "https://example.com/webhook";
            var error = "Connection timeout";
            
            _mockTransaction.Setup(x => x.ExecuteAsync(It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            
            // Act
            await _tracker.RecordFailureAsync(deliveryKey, webhookUrl, error);
            
            // Assert
            _mockTransaction.Verify(x => x.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(webhookUrl)),
                "failed",
                1,
                It.IsAny<CommandFlags>()), Times.Once);
            
            _mockTransaction.Verify(x => x.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(webhookUrl)),
                "last_error",
                error,
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
            
            _mockTransaction.Verify(x => x.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains($"webhook:failure:{deliveryKey}")),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                false,
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }
        
        [Fact]
        public async Task GetStatsAsync_ShouldReturnCorrectStats()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var hashEntries = new HashEntry[]
            {
                new HashEntry("delivered", 100),
                new HashEntry("failed", 5),
                new HashEntry("last_delivery", DateTime.UtcNow.AddMinutes(-10).ToString("O")),
                new HashEntry("last_failure", DateTime.UtcNow.AddHours(-1).ToString("O"))
            };
            
            _mockDatabase.Setup(x => x.HashGetAllAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(webhookUrl)),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(hashEntries);
            
            // Act
            var stats = await _tracker.GetStatsAsync(webhookUrl);
            
            // Assert
            Assert.Equal(100, stats.DeliveredCount);
            Assert.Equal(5, stats.FailedCount);
            Assert.NotNull(stats.LastDeliveryTime);
            Assert.NotNull(stats.LastFailureTime);
            Assert.Equal(95.24, Math.Round(stats.SuccessRate, 2));
        }
        
        [Fact]
        public async Task GetStatsAsync_WhenNoStats_ShouldReturnEmptyStats()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            _mockDatabase.Setup(x => x.HashGetAllAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(Array.Empty<HashEntry>());
            
            // Act
            var stats = await _tracker.GetStatsAsync(webhookUrl);
            
            // Assert
            Assert.Equal(0, stats.DeliveredCount);
            Assert.Equal(0, stats.FailedCount);
            Assert.Null(stats.LastDeliveryTime);
            Assert.Null(stats.LastFailureTime);
            Assert.Equal(0, stats.SuccessRate);
        }
    }
}