using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Tests.Helpers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using StackExchange.Redis;

namespace ConduitLLM.Tests.Configuration.Services
{
    /// <summary>
    /// Focused unit tests for BatchSpendUpdateService configuration and initialization
    /// Separated from the main service tests to keep concerns focused
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "BatchSpendUpdateService")]
    public class BatchSpendUpdateServiceConfigurationTests : IDisposable
    {
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
        private readonly Mock<ILogger<BatchSpendUpdateService>> _mockLogger;
        private readonly Mock<IBillingAlertingService> _mockAlertingService;
        private readonly TestRedisConnectionFactory _testRedisFactory;

        public BatchSpendUpdateServiceConfigurationTests()
        {
            _mockScopeFactory = new Mock<IServiceScopeFactory>();
            _mockLogger = new Mock<ILogger<BatchSpendUpdateService>>();
            _mockAlertingService = new Mock<IBillingAlertingService>();
            
            var mockRedisConnection = new Mock<IConnectionMultiplexer>();
            var mockRedisDb = new Mock<IDatabase>();
            var mockRedisServer = new Mock<IServer>();
            
            // Setup basic Redis mocks for GetStatisticsAsync
            var endPoint = new System.Net.DnsEndPoint("localhost", 6379);
            mockRedisConnection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockRedisDb.Object);
            mockRedisConnection.Setup(x => x.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
                .Returns(mockRedisServer.Object);
            mockRedisConnection.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
                .Returns(new[] { endPoint });
            
            // Setup server.Keys to return empty for statistics
            mockRedisServer.Setup(x => x.Keys(
                It.IsAny<int>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<int>(), 
                It.IsAny<long>(), 
                It.IsAny<int>(), 
                It.IsAny<CommandFlags>()))
                .Returns(Array.Empty<RedisKey>());
            
            _testRedisFactory = new TestRedisConnectionFactory(mockRedisConnection.Object);
        }

        [Fact]
        public void Constructor_WithValidConfiguration_ShouldSucceed()
        {
            // Arrange
            var options = Microsoft.Extensions.Options.Options.Create(new BatchSpendingOptions
            {
                FlushIntervalSeconds = 60,
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 3600,
                RedisTtlHours = 24
            });

            // Act & Assert - Should not throw
            using var service = new BatchSpendUpdateService(
                _mockScopeFactory.Object,
                _testRedisFactory,
                options,
                _mockLogger.Object,
                _mockAlertingService.Object);

            // IsHealthy will be false until the service starts (ExecuteAsync is called)
            // For unit tests, we just verify construction succeeded
            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_WithInvalidConfiguration_ShouldThrowException()
        {
            // Arrange
            var options = Microsoft.Extensions.Options.Options.Create(new BatchSpendingOptions
            {
                FlushIntervalSeconds = 0, // Invalid - below minimum
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 3600,
                RedisTtlHours = 24
            });

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new BatchSpendUpdateService(
                    _mockScopeFactory.Object,
                    _testRedisFactory,
                    options,
                    _mockLogger.Object,
                    _mockAlertingService.Object));

            Assert.Contains("Invalid BatchSpending configuration", exception.Message);
        }

        [Fact]
        public void Constructor_WithDangerousConfiguration_ShouldThrowException()
        {
            // Arrange - Configuration that could cause transaction loss
            var options = Microsoft.Extensions.Options.Options.Create(new BatchSpendingOptions
            {
                FlushIntervalSeconds = 86400, // 24 hours - same as Redis TTL
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 86400,
                RedisTtlHours = 24
            });

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new BatchSpendUpdateService(
                    _mockScopeFactory.Object,
                    _testRedisFactory,
                    options,
                    _mockLogger.Object,
                    _mockAlertingService.Object));

            Assert.Contains("prevent transaction loss", exception.Message);
        }

        [Theory]
        [InlineData(1, 1)]      // 1 second for fast testing
        [InlineData(30, 30)]    // Default production
        [InlineData(300, 300)]  // 5 minutes
        [InlineData(3600, 3600)] // 1 hour
        public void Constructor_WithVariousValidIntervals_ShouldApplyCorrectConfiguration(
            int configuredInterval, int expectedInterval)
        {
            // Arrange
            var options = Microsoft.Extensions.Options.Options.Create(new BatchSpendingOptions
            {
                FlushIntervalSeconds = configuredInterval,
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 3600,
                RedisTtlHours = 24
            });

            // Act
            using var service = new BatchSpendUpdateService(
                _mockScopeFactory.Object,
                _testRedisFactory,
                options,
                _mockLogger.Object,
                _mockAlertingService.Object);

            // Assert - Verify the service was created successfully
            Assert.NotNull(service);
            
            // Verify that the logger was called with configuration information
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"flush interval: {TimeSpan.FromSeconds(expectedInterval)}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void Constructor_WithClampedConfiguration_ShouldFailValidation()
        {
            // Arrange - Configuration that will fail validation (2 hours > 1 hour max)
            var options = Microsoft.Extensions.Options.Options.Create(new BatchSpendingOptions
            {
                FlushIntervalSeconds = 7200, // 2 hours - exceeds max
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 3600, // 1 hour max
                RedisTtlHours = 24
            });

            // Act & Assert - Should throw validation exception
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new BatchSpendUpdateService(
                    _mockScopeFactory.Object,
                    _testRedisFactory,
                    options,
                    _mockLogger.Object,
                    _mockAlertingService.Object));

            Assert.Contains("cannot be greater than MaximumIntervalSeconds", exception.Message);
        }

        [Fact]
        public async Task GetStatisticsAsync_ShouldIncludeConfigurationValues()
        {
            // Arrange
            var options = Microsoft.Extensions.Options.Options.Create(new BatchSpendingOptions
            {
                FlushIntervalSeconds = 120,
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 3600,
                RedisTtlHours = 48
            });

            using var service = new BatchSpendUpdateService(
                _mockScopeFactory.Object,
                _testRedisFactory,
                options,
                _mockLogger.Object,
                _mockAlertingService.Object);

            // Act
            var statistics = await service.GetStatisticsAsync();

            // Assert
            Assert.Contains("ConfiguredFlushInterval", statistics.Keys);
            Assert.Contains("MinimumInterval", statistics.Keys);
            Assert.Contains("MaximumInterval", statistics.Keys);
            Assert.Contains("RedisTtlHours", statistics.Keys);
            
            Assert.Equal(120, statistics["ConfiguredFlushInterval"]);
            Assert.Equal(1, statistics["MinimumInterval"]);
            Assert.Equal(3600, statistics["MaximumInterval"]);
            Assert.Equal(48.0, statistics["RedisTtlHours"]);
        }

        [Fact]
        public void Constructor_WithDevelopmentConfiguration_ShouldEnableFastTesting()
        {
            // Arrange - Typical development configuration
            var options = Microsoft.Extensions.Options.Options.Create(new BatchSpendingOptions
            {
                FlushIntervalSeconds = 1, // 1 second for fast testing
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 3600,
                RedisTtlHours = 24
            });

            // Act
            using var service = new BatchSpendUpdateService(
                _mockScopeFactory.Object,
                _testRedisFactory,
                options,
                _mockLogger.Object,
                _mockAlertingService.Object);

            // Assert - Service created successfully
            Assert.NotNull(service);
            
            // Verify that 1-second interval is configured
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("flush interval: 00:00:01")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        public void Dispose()
        {
            // Clean up any resources if needed
        }
    }
}