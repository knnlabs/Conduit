using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public class HybridCacheStatisticsCollectorTests
    {
        private readonly Mock<ICacheStatisticsCollector> _mockLocalCollector;
        private readonly Mock<IDistributedCacheStatisticsCollector> _mockDistributedCollector;
        private readonly Mock<ILogger<HybridCacheStatisticsCollector>> _mockLogger;

        public HybridCacheStatisticsCollectorTests()
        {
            _mockLocalCollector = new Mock<ICacheStatisticsCollector>();
            _mockDistributedCollector = new Mock<IDistributedCacheStatisticsCollector>();
            _mockLogger = new Mock<ILogger<HybridCacheStatisticsCollector>>();
            
            _mockDistributedCollector.Setup(d => d.InstanceId).Returns("test-distributed-instance");
        }

        [Fact]
        public void Constructor_WithDistributedCollector_UsesDistributedMode()
        {
            // Act
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                _mockDistributedCollector.Object,
                _mockLogger.Object);

            // Assert
            Assert.True(hybrid.IsDistributed);
            Assert.Equal("test-distributed-instance", hybrid.InstanceId);
        }

        [Fact]
        public void Constructor_WithoutDistributedCollector_UsesLocalMode()
        {
            // Act
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                null,
                _mockLogger.Object);

            // Assert
            Assert.False(hybrid.IsDistributed);
            Assert.Equal("local", hybrid.InstanceId);
        }

        [Fact]
        public void Constructor_WithNeitherCollector_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                new HybridCacheStatisticsCollector(null, null, _mockLogger.Object));
        }

        [Fact]
        public async Task RecordOperationAsync_InDistributedMode_UsesDistributedCollector()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                _mockDistributedCollector.Object,
                _mockLogger.Object);

            var operation = new CacheOperation
            {
                Region = CacheRegion.ProviderResponses,
                OperationType = CacheOperationType.Hit
            };

            // Act
            await hybrid.RecordOperationAsync(operation);

            // Assert
            _mockDistributedCollector.Verify(d => d.RecordOperationAsync(operation, default), Times.Once);
            _mockLocalCollector.Verify(l => l.RecordOperationAsync(It.IsAny<CacheOperation>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RecordOperationAsync_InLocalMode_UsesLocalCollector()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                null,
                _mockLogger.Object);

            var operation = new CacheOperation
            {
                Region = CacheRegion.ProviderResponses,
                OperationType = CacheOperationType.Hit
            };

            // Act
            await hybrid.RecordOperationAsync(operation);

            // Assert
            _mockLocalCollector.Verify(l => l.RecordOperationAsync(operation, default), Times.Once);
        }

        [Fact]
        public async Task GetStatisticsAsync_InDistributedMode_ReturnsAggregatedStatistics()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                _mockDistributedCollector.Object,
                _mockLogger.Object);

            var expectedStats = new CacheStatistics { HitCount = 100, MissCount = 20 };
            _mockDistributedCollector.Setup(d => d.GetAggregatedStatisticsAsync(CacheRegion.ProviderResponses, default))
                .ReturnsAsync(expectedStats);

            // Act
            var stats = await hybrid.GetStatisticsAsync(CacheRegion.ProviderResponses);

            // Assert
            Assert.Equal(expectedStats.HitCount, stats.HitCount);
            Assert.Equal(expectedStats.MissCount, stats.MissCount);
            _mockDistributedCollector.Verify(d => d.GetAggregatedStatisticsAsync(CacheRegion.ProviderResponses, default), Times.Once);
        }

        [Fact]
        public async Task GetAggregatedStatisticsAsync_InLocalMode_ReturnsLocalStatistics()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                null,
                _mockLogger.Object);

            var expectedStats = new CacheStatistics { HitCount = 50, MissCount = 10 };
            _mockLocalCollector.Setup(l => l.GetStatisticsAsync(CacheRegion.ProviderResponses, default))
                .ReturnsAsync(expectedStats);

            // Act
            var stats = await hybrid.GetAggregatedStatisticsAsync(CacheRegion.ProviderResponses);

            // Assert
            Assert.Equal(expectedStats.HitCount, stats.HitCount);
            Assert.Equal(expectedStats.MissCount, stats.MissCount);
        }

        [Fact]
        public async Task GetPerInstanceStatisticsAsync_InDistributedMode_ReturnsDistributedStats()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                _mockDistributedCollector.Object,
                _mockLogger.Object);

            var expectedStats = new Dictionary<string, CacheStatistics>
            {
                ["instance1"] = new CacheStatistics { HitCount = 100 },
                ["instance2"] = new CacheStatistics { HitCount = 200 }
            };

            _mockDistributedCollector.Setup(d => d.GetPerInstanceStatisticsAsync(CacheRegion.ProviderResponses, default))
                .ReturnsAsync(expectedStats);

            // Act
            var stats = await hybrid.GetPerInstanceStatisticsAsync(CacheRegion.ProviderResponses);

            // Assert
            Assert.Equal(2, stats.Count);
            Assert.Equal(100, stats["instance1"].HitCount);
            Assert.Equal(200, stats["instance2"].HitCount);
        }

        [Fact]
        public async Task GetPerInstanceStatisticsAsync_InLocalMode_ReturnsSingleInstance()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                null,
                _mockLogger.Object);

            var expectedStats = new CacheStatistics { HitCount = 150 };
            _mockLocalCollector.Setup(l => l.GetStatisticsAsync(CacheRegion.ProviderResponses, default))
                .ReturnsAsync(expectedStats);

            // Act
            var stats = await hybrid.GetPerInstanceStatisticsAsync(CacheRegion.ProviderResponses);

            // Assert
            Assert.Single(stats);
            Assert.True(stats.ContainsKey("local"));
            Assert.Equal(150, stats["local"].HitCount);
        }

        [Fact]
        public async Task RegisterInstanceAsync_InDistributedMode_CallsDistributedMethod()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                _mockDistributedCollector.Object,
                _mockLogger.Object);

            // Act
            await hybrid.RegisterInstanceAsync();

            // Assert
            _mockDistributedCollector.Verify(d => d.RegisterInstanceAsync(default), Times.Once);
        }

        [Fact]
        public async Task RegisterInstanceAsync_InLocalMode_DoesNothing()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                null,
                _mockLogger.Object);

            // Act
            await hybrid.RegisterInstanceAsync();

            // Assert - should complete without errors
            Assert.True(true);
        }

        [Fact]
        public async Task GetActiveInstancesAsync_InDistributedMode_ReturnsMultipleInstances()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                _mockDistributedCollector.Object,
                _mockLogger.Object);

            var expectedInstances = new[] { "instance1", "instance2", "instance3" };
            _mockDistributedCollector.Setup(d => d.GetActiveInstancesAsync(default))
                .ReturnsAsync(expectedInstances);

            // Act
            var instances = await hybrid.GetActiveInstancesAsync();

            // Assert
            Assert.Equal(3, instances.Count());
            Assert.Contains("instance1", instances);
            Assert.Contains("instance2", instances);
            Assert.Contains("instance3", instances);
        }

        [Fact]
        public async Task GetActiveInstancesAsync_InLocalMode_ReturnsSingleLocalInstance()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                null,
                _mockLogger.Object);

            // Act
            var instances = await hybrid.GetActiveInstancesAsync();

            // Assert
            Assert.Single(instances);
            Assert.Contains("local", instances);
        }

        [Fact]
        public void EventSubscription_InDistributedMode_ForwardsEvents()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                _mockDistributedCollector.Object,
                _mockLogger.Object);

            CacheStatisticsUpdatedEventArgs? receivedArgs = null;
            hybrid.StatisticsUpdated += (sender, args) => receivedArgs = args;

            var eventArgs = new CacheStatisticsUpdatedEventArgs
            {
                Region = CacheRegion.ProviderResponses,
                Statistics = new CacheStatistics { HitCount = 100 }
            };

            // Act
            _mockDistributedCollector.Raise(d => d.StatisticsUpdated += null, eventArgs);

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal(CacheRegion.ProviderResponses, receivedArgs.Region);
            Assert.Equal(100, receivedArgs.Statistics.HitCount);
        }

        [Fact]
        public void DistributedEventSubscription_ForwardsDistributedEvents()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                _mockDistributedCollector.Object,
                _mockLogger.Object);

            DistributedCacheStatisticsEventArgs? receivedArgs = null;
            hybrid.DistributedStatisticsUpdated += (sender, args) => receivedArgs = args;

            var eventArgs = new DistributedCacheStatisticsEventArgs(
                "instance1",
                CacheRegion.ProviderResponses,
                new CacheStatistics { HitCount = 200 },
                DateTime.UtcNow);

            // Act
            _mockDistributedCollector.Raise(d => d.DistributedStatisticsUpdated += null, eventArgs);

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal("instance1", receivedArgs.InstanceId);
            Assert.Equal(CacheRegion.ProviderResponses, receivedArgs.Region);
            Assert.Equal(200, receivedArgs.Statistics.HitCount);
        }

        [Fact]
        public async Task GetLocalStatisticsAsync_InDistributedMode_ReturnsLocalInstanceStats()
        {
            // Arrange
            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                _mockDistributedCollector.Object,
                _mockLogger.Object);

            var expectedStats = new CacheStatistics { HitCount = 75 };
            _mockDistributedCollector.Setup(d => d.GetStatisticsAsync(CacheRegion.ProviderResponses, default))
                .ReturnsAsync(expectedStats);

            // Act
            var stats = await hybrid.GetLocalStatisticsAsync(CacheRegion.ProviderResponses);

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(75, stats.HitCount);
        }

        [Fact]
        public void Dispose_DisposesDistributedCollector()
        {
            // Arrange
            var mockDisposableDistributed = new Mock<IDistributedCacheStatisticsCollector>();
            mockDisposableDistributed.As<IDisposable>();
            mockDisposableDistributed.Setup(d => d.InstanceId).Returns("test");

            var hybrid = new HybridCacheStatisticsCollector(
                _mockLocalCollector.Object,
                mockDisposableDistributed.Object,
                _mockLogger.Object);

            // Act
            hybrid.Dispose();

            // Assert
            mockDisposableDistributed.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
        }
    }
}