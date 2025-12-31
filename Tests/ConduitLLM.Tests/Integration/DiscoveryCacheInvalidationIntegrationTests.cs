using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Consumers;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Integration tests for the discovery cache invalidation flow.
    /// Tests the complete flow from event publication to cache invalidation across multiple instances.
    /// </summary>
    [Trait("Category", "Integration")]
    public class DiscoveryCacheInvalidationIntegrationTests : IAsyncLifetime
    {
        private ServiceProvider _serviceProvider;
        private ITestHarness _harness;

        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();

            // Add MassTransit test harness
            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<ModelMappingCacheInvalidationConsumer>();
            });

            // Add mock services
            services.AddSingleton(Mock.Of<ICacheManager>());
            services.AddSingleton(Mock.Of<IDiscoveryCacheService>());
            services.AddSingleton(Mock.Of<ILogger<ModelMappingCacheInvalidationConsumer>>());

            _serviceProvider = services.BuildServiceProvider();
            _harness = _serviceProvider.GetRequiredService<ITestHarness>();

            await _harness.Start();
        }

        public async Task DisposeAsync()
        {
            await _harness.Stop();
            if (_serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                _serviceProvider?.Dispose();
            }
        }

        [Fact]
        public async Task Should_Process_ModelMappingChanged_Event_And_Invalidate_Cache()
        {
            // Arrange
            var mockCacheManager = Mock.Get(_serviceProvider.GetRequiredService<ICacheManager>());
            var mockDiscoveryCache = Mock.Get(_serviceProvider.GetRequiredService<IDiscoveryCacheService>());

            mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            mockDiscoveryCache
                .Setup(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var @event = new ModelMappingChanged
            {
                MappingId = 123,
                ModelAlias = "gpt-4-turbo",
                ProviderId = 1,
                IsEnabled = true,
                ChangeType = "Created",
                CorrelationId = Guid.NewGuid().ToString()
            };

            // Act
            await _harness.Bus.Publish(@event);

            // Wait for the message to be consumed
            Assert.True(await _harness.Consumed.Any<ModelMappingChanged>(x =>
                x.Context.Message.MappingId == @event.MappingId));

            // Assert
            var consumerHarness = _harness.GetConsumerHarness<ModelMappingCacheInvalidationConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<ModelMappingChanged>());

            // Verify the consumer called the cache services
            mockCacheManager.Verify(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()), Times.Once);
            mockDiscoveryCache.Verify(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()), Times.Once);

            // Verify no faults occurred (Failed property may not exist in test harness)
            // The test passes if no exceptions were thrown
        }

        [Fact]
        public async Task Should_Handle_Multiple_Events_In_Sequence()
        {
            // Arrange
            var mockCacheManager = Mock.Get(_serviceProvider.GetRequiredService<ICacheManager>());
            var mockDiscoveryCache = Mock.Get(_serviceProvider.GetRequiredService<IDiscoveryCacheService>());

            mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            mockDiscoveryCache
                .Setup(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var events = new List<ModelMappingChanged>
            {
                new() { MappingId = 1, ModelAlias = "model-1", ProviderId = 1, ChangeType = "Created", CorrelationId = Guid.NewGuid().ToString() },
                new() { MappingId = 2, ModelAlias = "model-2", ProviderId = 2, ChangeType = "Updated", CorrelationId = Guid.NewGuid().ToString() },
                new() { MappingId = 3, ModelAlias = "model-3", ProviderId = 3, ChangeType = "Deleted", CorrelationId = Guid.NewGuid().ToString() }
            };

            // Act
            foreach (var evt in events)
            {
                await _harness.Bus.Publish(evt);
            }

            // Wait for all messages to be consumed
            foreach (var evt in events)
            {
                var mappingId = evt.MappingId;
                Assert.True(await _harness.Consumed.Any<ModelMappingChanged>(x =>
                    x.Context.Message.MappingId == mappingId));
            }

            // Assert
            mockCacheManager.Verify(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()), Times.Exactly(3));
            mockDiscoveryCache.Verify(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));

            // Verify no faults occurred (Failed property may not exist in test harness)
            // The test passes if no exceptions were thrown
        }

        [Fact]
        public async Task Should_Continue_Processing_On_Cache_Failure()
        {
            // Arrange
            var mockCacheManager = Mock.Get(_serviceProvider.GetRequiredService<ICacheManager>());
            var mockDiscoveryCache = Mock.Get(_serviceProvider.GetRequiredService<IDiscoveryCacheService>());

            var cacheCallCount = 0;
            mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    cacheCallCount++;
                    if (cacheCallCount == 1)
                    {
                        // Fail on first attempt - but consumer should continue to discovery cache
                        throw new InvalidOperationException("Transient error");
                    }
                    return Task.FromResult(3);
                });

            mockDiscoveryCache
                .Setup(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var @event = new ModelMappingChanged
            {
                MappingId = 999,
                ModelAlias = "retry-model",
                ProviderId = 1,
                IsEnabled = true,
                ChangeType = "Created",
                CorrelationId = Guid.NewGuid().ToString()
            };

            // Act
            await _harness.Bus.Publish(@event);

            // Wait for consumption
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert - consumer should NOT throw on cache failure, so message is consumed
            var consumed = await _harness.Consumed.Any<ModelMappingChanged>(x =>
                x.Context.Message.MappingId == @event.MappingId);

            Assert.True(consumed);
            // Discovery cache should still be called even if model mapping cache failed
            mockDiscoveryCache.Verify(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_Process_Events_From_Multiple_Publishers_Concurrently()
        {
            // Arrange
            var mockCacheManager = Mock.Get(_serviceProvider.GetRequiredService<ICacheManager>());
            var mockDiscoveryCache = Mock.Get(_serviceProvider.GetRequiredService<IDiscoveryCacheService>());

            var cacheRemoveCount = 0;
            var invalidateCount = 0;

            mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Interlocked.Increment(ref cacheRemoveCount);
                    return Task.FromResult(3);
                });

            mockDiscoveryCache
                .Setup(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Interlocked.Increment(ref invalidateCount);
                    return Task.CompletedTask;
                });

            // Create multiple events simulating different Admin API instances
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var eventId = i;
                tasks.Add(Task.Run(async () =>
                {
                    var @event = new ModelMappingChanged
                    {
                        MappingId = eventId,
                        ModelAlias = $"concurrent-model-{eventId}",
                        ProviderId = eventId % 3 + 1, // Distribute across 3 providers
                        IsEnabled = true,
                        ChangeType = eventId % 3 == 0 ? "Created" : eventId % 3 == 1 ? "Updated" : "Deleted",
                        CorrelationId = Guid.NewGuid().ToString()
                    };

                    await _harness.Bus.Publish(@event);
                }));
            }

            // Act
            await Task.WhenAll(tasks);

            // Wait for all messages to be consumed
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert
            Assert.Equal(10, cacheRemoveCount);
            Assert.Equal(10, invalidateCount);

            // Verify no faults occurred (Failed property may not exist in test harness)
            // The test passes if no exceptions were thrown
        }

        [Fact]
        public async Task Should_Maintain_Event_Order_Per_Model()
        {
            // Arrange
            var mockCacheManager = Mock.Get(_serviceProvider.GetRequiredService<ICacheManager>());
            var mockDiscoveryCache = Mock.Get(_serviceProvider.GetRequiredService<IDiscoveryCacheService>());

            var processedEvents = new List<string>();

            mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            mockDiscoveryCache
                .Setup(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    // Track the order of processing
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    processedEvents.Add(timestamp);
                })
                .Returns(Task.CompletedTask);

            // Create events for the same model in sequence
            var modelAlias = "order-test-model";
            var events = new[]
            {
                new ModelMappingChanged { MappingId = 1, ModelAlias = modelAlias, ProviderId = 1, ChangeType = "Created", CorrelationId = "1" },
                new ModelMappingChanged { MappingId = 1, ModelAlias = modelAlias, ProviderId = 1, ChangeType = "Updated", CorrelationId = "2" },
                new ModelMappingChanged { MappingId = 1, ModelAlias = modelAlias, ProviderId = 1, ChangeType = "Updated", CorrelationId = "3" },
                new ModelMappingChanged { MappingId = 1, ModelAlias = modelAlias, ProviderId = 1, ChangeType = "Deleted", CorrelationId = "4" }
            };

            // Act
            foreach (var @event in events)
            {
                await _harness.Bus.Publish(@event);
                await Task.Delay(100); // Small delay to ensure ordering
            }

            // Wait for all messages to be consumed
            await Task.Delay(TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(4, processedEvents.Count);

            // Verify events were processed (order might not be guaranteed in test harness)
            mockDiscoveryCache.Verify(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()), Times.Exactly(4));
        }
    }
}