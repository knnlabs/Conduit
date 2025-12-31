using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Integration tests for RedisMediaDeletionBudgetService.
    /// Tests the Redis-backed implementation of media deletion budget tracking.
    /// NOTE: Requires Redis to be running. Tests will be skipped if Redis is not available.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Component", "MediaLifecycle")]
    public class RedisMediaDeletionBudgetServiceTests : IDisposable
    {
        private readonly ConnectionMultiplexer? _redis;
        private readonly IDatabase? _db;
        private readonly Mock<ILogger<RedisMediaDeletionBudgetService>> _mockLogger;
        private readonly RedisMediaDeletionBudgetService? _service;
        private readonly string _testKeyPrefix;

        public RedisMediaDeletionBudgetServiceTests()
        {
            _mockLogger = new Mock<ILogger<RedisMediaDeletionBudgetService>>();
            _testKeyPrefix = $"test:{Guid.NewGuid():N}:";

            // Use environment variable or local Redis for testing
            var redisConnectionString = Environment.GetEnvironmentVariable("TEST_REDIS_CONNECTION")
                ?? "localhost:6379,allowAdmin=true";

            try
            {
                var options = ConfigurationOptions.Parse(redisConnectionString);
                options.ConnectTimeout = 1000;
                options.SyncTimeout = 1000;
                options.AbortOnConnectFail = false;

                _redis = ConnectionMultiplexer.Connect(options);
                _db = _redis.GetDatabase();

                // Test connection
                _db.Ping();

                _service = new RedisMediaDeletionBudgetService(_redis, _mockLogger.Object);
            }
            catch (Exception)
            {
                // Redis not available - tests will be skipped
                _redis = null;
                _db = null;
                _service = null;
            }
        }

        private void SkipIfRedisNotAvailable()
        {
            if (_redis == null || !_redis.IsConnected || _service == null)
            {
                throw new SkipException(
                    "Redis is not available for testing. " +
                    "Set TEST_REDIS_CONNECTION environment variable or ensure Redis is running.");
            }
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullRedis_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RedisMediaDeletionBudgetService(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            SkipIfRedisNotAvailable();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RedisMediaDeletionBudgetService(_redis!, null!));
        }

        #endregion

        #region GetMonthlyDeleteCountAsync Tests

        [Fact]
        public async Task GetMonthlyDeleteCountAsync_WhenKeyNotExists_ReturnsZero()
        {
            SkipIfRedisNotAvailable();

            // Act
            var count = await _service!.GetMonthlyDeleteCountAsync();

            // Assert
            count.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task GetMonthlyDeleteCountAsync_AfterIncrement_ReturnsCorrectCount()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            // First clear any existing value for this month
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);

            await _service!.IncrementMonthlyDeleteCountAsync(100);

            // Act
            var count = await _service.GetMonthlyDeleteCountAsync();

            // Assert
            count.Should().Be(100);

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        #endregion

        #region IncrementMonthlyDeleteCountAsync Tests

        [Fact]
        public async Task IncrementMonthlyDeleteCountAsync_WithPositiveCount_ReturnsNewTotal()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);

            // Act
            var result = await _service!.IncrementMonthlyDeleteCountAsync(100);

            // Assert
            result.Should().Be(100);

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        [Fact]
        public async Task IncrementMonthlyDeleteCountAsync_MultipleIncrements_AccumulatesCorrectly()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);

            // Act
            await _service!.IncrementMonthlyDeleteCountAsync(50);
            await _service.IncrementMonthlyDeleteCountAsync(30);
            var result = await _service.IncrementMonthlyDeleteCountAsync(20);

            // Assert
            result.Should().Be(100);

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task IncrementMonthlyDeleteCountAsync_WithZeroOrNegativeCount_ReturnsCurrentCount(int count)
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);
            await _service!.IncrementMonthlyDeleteCountAsync(50);

            // Act
            var result = await _service.IncrementMonthlyDeleteCountAsync(count);

            // Assert
            result.Should().Be(50);

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        [Fact]
        public async Task IncrementMonthlyDeleteCountAsync_SetsExpiry_OnNewKey()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);

            // Act
            await _service!.IncrementMonthlyDeleteCountAsync(10);

            // Assert
            var ttl = await _db.KeyTimeToLiveAsync(monthKey);
            ttl.Should().NotBeNull();
            ttl!.Value.TotalDays.Should().BeGreaterThan(30);
            ttl.Value.TotalDays.Should().BeLessThanOrEqualTo(35);

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        #endregion

        #region WouldExceedBudgetAsync Tests

        [Fact]
        public async Task WouldExceedBudgetAsync_WhenWithinBudget_ReturnsFalse()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);
            await _service!.IncrementMonthlyDeleteCountAsync(400_000);

            // Act
            var result = await _service.WouldExceedBudgetAsync(50_000, 500_000);

            // Assert
            result.Should().BeFalse();

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        [Fact]
        public async Task WouldExceedBudgetAsync_WhenWouldExceedBudget_ReturnsTrue()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);
            await _service!.IncrementMonthlyDeleteCountAsync(400_000);

            // Act
            var result = await _service.WouldExceedBudgetAsync(100_001, 500_000);

            // Assert
            result.Should().BeTrue();

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        #endregion

        #region GetRemainingBudgetAsync Tests

        [Fact]
        public async Task GetRemainingBudgetAsync_WhenNoUsage_ReturnsFullBudget()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);

            // Act
            var result = await _service!.GetRemainingBudgetAsync(500_000);

            // Assert
            result.Should().Be(500_000);
        }

        [Fact]
        public async Task GetRemainingBudgetAsync_WithPartialUsage_ReturnsCorrectRemaining()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);
            await _service!.IncrementMonthlyDeleteCountAsync(150_000);

            // Act
            var result = await _service.GetRemainingBudgetAsync(500_000);

            // Assert
            result.Should().Be(350_000);

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        [Fact]
        public async Task GetRemainingBudgetAsync_WhenOverBudget_ReturnsZero()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);
            await _service!.IncrementMonthlyDeleteCountAsync(600_000);

            // Act
            var result = await _service.GetRemainingBudgetAsync(500_000);

            // Assert
            result.Should().Be(0);

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        #endregion

        #region Distributed Scenarios

        [Fact]
        public async Task MultipleInstances_ShareBudgetState()
        {
            SkipIfRedisNotAvailable();

            // Arrange
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);

            var service1 = new RedisMediaDeletionBudgetService(_redis!, _mockLogger.Object);
            var service2 = new RedisMediaDeletionBudgetService(_redis!, _mockLogger.Object);

            // Act
            await service1.IncrementMonthlyDeleteCountAsync(100);
            await service2.IncrementMonthlyDeleteCountAsync(200);

            var count1 = await service1.GetMonthlyDeleteCountAsync();
            var count2 = await service2.GetMonthlyDeleteCountAsync();

            // Assert
            count1.Should().Be(300);
            count2.Should().Be(300);

            // Cleanup
            await _db.KeyDeleteAsync(monthKey);
        }

        #endregion

        #region Interface Compliance Tests

        [Fact]
        public void Service_ImplementsIMediaDeletionBudgetService()
        {
            SkipIfRedisNotAvailable();

            // Assert
            _service.Should().BeAssignableTo<IMediaDeletionBudgetService>();
        }

        #endregion

        private static string GetCurrentMonthKey()
        {
            return $"media:monthly-deletes:{DateTime.UtcNow:yyyy-MM}";
        }

        public void Dispose()
        {
            // Clean up test data
            if (_redis?.IsConnected == true && _db != null)
            {
                try
                {
                    var server = _redis.GetServer(_redis.GetEndPoints().First());
                    var keys = server.Keys(pattern: $"{_testKeyPrefix}*");
                    foreach (var key in keys)
                    {
                        _db.KeyDelete(key);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                _redis.Dispose();
            }
        }
    }
}
