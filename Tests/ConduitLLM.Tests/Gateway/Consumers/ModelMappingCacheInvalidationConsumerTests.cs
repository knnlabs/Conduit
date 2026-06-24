using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Consumers;
using ConduitLLM.Tests.Messaging;

namespace ConduitLLM.Tests.Http.Consumers
{
    [Trait("Category", "Unit")]
    public class ModelMappingCacheInvalidationConsumerTests
    {
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly Mock<IDiscoveryCacheService> _mockDiscoveryCacheService;
        private readonly Mock<ILogger<ModelMappingCacheInvalidationConsumer>> _mockLogger;
        private readonly ModelMappingCacheInvalidationConsumer _consumer;

        public ModelMappingCacheInvalidationConsumerTests()
        {
            _mockCacheManager = new Mock<ICacheManager>();
            _mockDiscoveryCacheService = new Mock<IDiscoveryCacheService>();
            _mockLogger = new Mock<ILogger<ModelMappingCacheInvalidationConsumer>>();

            _consumer = new ModelMappingCacheInvalidationConsumer(
                _mockCacheManager.Object,
                _mockDiscoveryCacheService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task Consume_Should_Invalidate_ModelMapping_And_Discovery_Cache_On_Created()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 123,
                ModelAlias = "gpt-4-turbo",
                ProviderId = 1,
                IsEnabled = true,
                ChangeType = "Created",
                CorrelationId = Guid.NewGuid().ToString()
            };

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Act
            await _consumer.HandleAsync(@event, new TestEventContext());

            // Assert - Model mapping cache invalidation
            _mockCacheManager.Verify(
                x => x.RemoveManyAsync(
                    It.Is<IEnumerable<string>>(keys =>
                        keys.Any(k => k.Contains("model:mapping:gpt-4-turbo")) &&
                        keys.Any(k => k.Contains("model:mapping:id:123")) &&
                        keys.Any(k => k.Contains("model:mapping:all"))),
                    CacheRegion.ModelMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Assert - Discovery cache invalidation
            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Invalidate_Both_Caches_On_Updated()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 456,
                ModelAlias = "claude-3-opus",
                ProviderId = 2,
                IsEnabled = false,
                ChangeType = "Updated",
                CorrelationId = Guid.NewGuid().ToString()
            };

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Act
            await _consumer.HandleAsync(@event, new TestEventContext());

            // Assert
            _mockCacheManager.Verify(
                x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()),
                Times.Once);

            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Invalidate_Both_Caches_On_Deleted()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 789,
                ModelAlias = "gemini-pro",
                ProviderId = 3,
                IsEnabled = true,
                ChangeType = "Deleted",
                CorrelationId = Guid.NewGuid().ToString()
            };

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Act
            await _consumer.HandleAsync(@event, new TestEventContext());

            // Assert
            _mockCacheManager.Verify(
                x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()),
                Times.Once);

            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Continue_With_Discovery_Cache_When_ModelMapping_Cache_Fails()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 111,
                ModelAlias = "error-model",
                ProviderId = 5,
                IsEnabled = true,
                ChangeType = "Updated",
                CorrelationId = Guid.NewGuid().ToString()
            };

            // Simulate model mapping cache failure
            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis connection failed"));

            // Act - should NOT throw
            await _consumer.HandleAsync(@event, new TestEventContext());

            // Assert - Discovery cache should still be called despite model mapping cache failure
            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate model mapping cache")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Not_Throw_When_Discovery_Cache_Fails()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 222,
                ModelAlias = "cache-error-model",
                ProviderId = 6,
                IsEnabled = true,
                ChangeType = "Created",
                CorrelationId = Guid.NewGuid().ToString()
            };

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Simulate discovery cache failure
            _mockDiscoveryCacheService
                .Setup(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis connection failed"));

            // Act - should NOT throw
            await _consumer.HandleAsync(@event, new TestEventContext());

            // Assert - Model mapping cache should have been called before the discovery failure
            _mockCacheManager.Verify(
                x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate discovery cache")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Handle_Null_ModelAlias()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 444,
                ModelAlias = null,
                ProviderId = 8,
                IsEnabled = true,
                ChangeType = "Deleted",
                CorrelationId = Guid.NewGuid().ToString()
            };

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            await _consumer.HandleAsync(@event, new TestEventContext());

            // Assert - Should only include ID-based key and all mappings key (not alias key)
            _mockCacheManager.Verify(
                x => x.RemoveManyAsync(
                    It.Is<IEnumerable<string>>(keys =>
                        keys.Any(k => k.Contains("model:mapping:id:444")) &&
                        keys.Any(k => k.Contains("model:mapping:all")) &&
                        !keys.Any(k => k.StartsWith("model:mapping:") && !k.Contains("id:") && !k.Contains("all"))),
                    CacheRegion.ModelMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData("Created")]
        [InlineData("Updated")]
        [InlineData("Deleted")]
        public async Task Consume_Should_Handle_All_Change_Types(string changeType)
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 333,
                ModelAlias = $"model-{changeType.ToLower()}",
                ProviderId = 7,
                IsEnabled = true,
                ChangeType = changeType,
                CorrelationId = Guid.NewGuid().ToString()
            };

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Act
            await _consumer.HandleAsync(@event, new TestEventContext());

            // Assert
            _mockCacheManager.Verify(
                x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()),
                Times.Once);

            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_CacheManager()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ModelMappingCacheInvalidationConsumer(
                    null!,
                    _mockDiscoveryCacheService.Object,
                    _mockLogger.Object));
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_DiscoveryCacheService()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ModelMappingCacheInvalidationConsumer(
                    _mockCacheManager.Object,
                    null!,
                    _mockLogger.Object));
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Logger()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ModelMappingCacheInvalidationConsumer(
                    _mockCacheManager.Object,
                    _mockDiscoveryCacheService.Object,
                    null!));
        }
    }
}
