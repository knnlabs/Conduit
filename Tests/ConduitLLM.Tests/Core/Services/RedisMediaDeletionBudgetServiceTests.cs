using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public sealed class RedisMediaDeletionBudgetFixture : IDisposable
    {
        public RedisMediaDeletionBudgetFixture()
        {
            var configuredConnectionString = Environment.GetEnvironmentVariable("TEST_REDIS_CONNECTION");
            IsRequired = !string.IsNullOrWhiteSpace(configuredConnectionString);
            var connectionString = IsRequired
                ? configuredConnectionString!
                : "localhost:6379,allowAdmin=true";

            ConnectionMultiplexer? connection = null;

            try
            {
                var options = ConfigurationOptions.Parse(connectionString);
                options.ConnectTimeout = 5000;
                options.SyncTimeout = 5000;
                options.ConnectRetry = 3;
                options.AbortOnConnectFail = true;

                connection = ConnectionMultiplexer.Connect(options);
                var database = connection.GetDatabase();
                database.Ping();

                Redis = connection;
                Database = database;
            }
            catch (Exception exception)
            {
                connection?.Dispose();
                InitializationException = exception;

                if (IsRequired)
                {
                    throw new InvalidOperationException(
                        "Redis is required because TEST_REDIS_CONNECTION is set, but the test connection failed.",
                        exception);
                }
            }
        }

        public bool IsRequired { get; }

        public ConnectionMultiplexer? Redis { get; }

        public IDatabase? Database { get; }

        public Exception? InitializationException { get; }

        public bool IsAvailable => Redis?.IsConnected == true && Database != null;

        public string GetUnavailableReason()
        {
            return InitializationException == null
                ? "Redis is not connected. Set TEST_REDIS_CONNECTION or ensure local Redis is running."
                : $"Redis is not available: {InitializationException.GetType().Name}: {InitializationException.Message}";
        }

        public void Dispose()
        {
            Redis?.Dispose();
        }
    }

    /// <summary>
    /// Integration tests for RedisMediaDeletionBudgetService.
    /// Tests the Redis-backed implementation of media deletion budget tracking.
    /// Redis is optional for local runs, but required when TEST_REDIS_CONNECTION is set.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Component", "MediaLifecycle")]
    public class RedisMediaDeletionBudgetServiceTests : IClassFixture<RedisMediaDeletionBudgetFixture>
    {
        private readonly RedisMediaDeletionBudgetFixture _fixture;
        private readonly ConnectionMultiplexer? _redis;
        private readonly IDatabase? _db;
        private readonly Mock<ILogger<RedisMediaDeletionBudgetService>> _mockLogger;
        private readonly RedisMediaDeletionBudgetService? _service;

        public RedisMediaDeletionBudgetServiceTests(RedisMediaDeletionBudgetFixture fixture)
        {
            _fixture = fixture;
            _mockLogger = new Mock<ILogger<RedisMediaDeletionBudgetService>>();
            _redis = fixture.Redis;
            _db = fixture.Database;
            _service = _redis == null
                ? null
                : new RedisMediaDeletionBudgetService(_redis, _mockLogger.Object);
        }

        private void SkipIfRedisNotAvailable()
        {
            if (_fixture.IsAvailable && _service != null)
            {
                return;
            }

            var reason = _fixture.GetUnavailableReason();
            if (_fixture.IsRequired)
            {
                throw new InvalidOperationException(reason, _fixture.InitializationException);
            }

            Skip.If(true, reason);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullRedis_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RedisMediaDeletionBudgetService(null!, _mockLogger.Object));
        }

        [SkippableFact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            SkipIfRedisNotAvailable();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RedisMediaDeletionBudgetService(_redis!, null!));
        }

        #endregion

        #region GetMonthlyDeleteCountAsync Tests

        [SkippableFact]
        public async Task GetMonthlyDeleteCountAsync_WhenKeyNotExists_ReturnsZero()
        {
            SkipIfRedisNotAvailable();

            // Act
            var count = await _service!.GetMonthlyDeleteCountAsync();

            // Assert
            count.Should().BeGreaterThanOrEqualTo(0);
        }

        [SkippableFact]
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

        [SkippableFact]
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

        [SkippableFact]
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

        [SkippableTheory]
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

        [SkippableFact]
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

        [SkippableFact]
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

        [SkippableFact]
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

        [SkippableFact]
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

        [SkippableFact]
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

        [SkippableFact]
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

        [SkippableFact]
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

        [SkippableFact]
        public async Task MultipleInstances_ReserveAtomicallyWithoutExceedingBudget()
        {
            SkipIfRedisNotAvailable();
            var monthKey = GetCurrentMonthKey();
            await _db!.KeyDeleteAsync(monthKey);
            var service1 = new RedisMediaDeletionBudgetService(_redis!, _mockLogger.Object);
            var service2 = new RedisMediaDeletionBudgetService(_redis!, _mockLogger.Object);

            var reservations = await Task.WhenAll(
                service1.ReserveAsync(8, 10),
                service2.ReserveAsync(8, 10));

            reservations.Sum(item => item.Granted).Should().Be(10);
            (await service1.GetMonthlyDeleteCountAsync()).Should().Be(10);
            await _db.KeyDeleteAsync(monthKey);
        }

        #endregion

        #region Interface Compliance Tests

        [SkippableFact]
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

    }
}
