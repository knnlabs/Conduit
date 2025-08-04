using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using StackExchange.Redis;
using MassTransit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Tests.Integration
{
    [Trait("Category", "Integration")]
    [Collection("Redis")]
    public class BatchInvalidationIntegrationTests : IAsyncLifetime
    {
        private IServiceProvider _serviceProvider = null!;
        private IConnectionMultiplexer _redis = null!;
        private IBus _bus = null!;
        private IDatabase _database = null!;

        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder => builder.AddDebug());

            // Configure Redis
            var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") 
                ?? "localhost:6379,allowAdmin=true";
            
            _redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
            services.AddSingleton(_redis);
            _database = _redis.GetDatabase();

            // Configure MassTransit with in-memory transport
            services.AddMassTransit(x =>
            {
                x.AddConsumer<ConduitLLM.Http.EventHandlers.VirtualKeyCacheInvalidationHandler>();
                
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                    
                    // Configure for immediate processing in tests
                    cfg.ConcurrentMessageLimit = 1;
                });
            });

            // Add batch invalidation services
            services.Configure<BatchInvalidationOptions>(options =>
            {
                options.Enabled = true;
                options.BatchWindow = TimeSpan.FromMilliseconds(50); // Shorter window for faster tests
                options.MaxBatchSize = 50;
                options.EnableCoalescing = true;
            });

            services.AddSingleton<BatchCacheInvalidationService>();
            services.AddSingleton<IBatchCacheInvalidationService>(provider => 
                provider.GetRequiredService<BatchCacheInvalidationService>());
            services.AddHostedService(provider => 
                provider.GetRequiredService<BatchCacheInvalidationService>());

            // Add Redis services
            services.AddSingleton<IRedisBatchOperations, RedisBatchOperations>();
            services.AddSingleton<IVirtualKeyCache, RedisVirtualKeyCache>();
            services.AddSingleton<IModelCostCache, RedisModelCostCache>();

            _serviceProvider = services.BuildServiceProvider();
            _bus = _serviceProvider.GetRequiredService<IBus>();

            // Start MassTransit
            var busControl = _serviceProvider.GetRequiredService<IBusControl>();
            await busControl.StartAsync();

            // Start hosted services
            var hostedService = _serviceProvider.GetRequiredService<BatchCacheInvalidationService>();
            await hostedService.StartAsync(default);
            
            // Give services time to fully initialize
            await Task.Delay(100);
        }

        public async Task DisposeAsync()
        {
            // Stop MassTransit first
            var busControl = _serviceProvider.GetService<IBusControl>();
            if (busControl != null)
            {
                await busControl.StopAsync();
            }

            // Stop hosted services
            var hostedService = _serviceProvider.GetService<BatchCacheInvalidationService>();
            if (hostedService != null)
            {
                await hostedService.StopAsync(default);
            }

            // Clean up test data
            try
            {
                await _database.ExecuteAsync("FLUSHDB");
            }
            catch (Exception ex)
            {
                // Log but don't fail disposal
                Console.WriteLine($"Failed to flush Redis database: {ex.Message}");
            }
            
            // Dispose service provider using async disposal
            if (_serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            _redis?.Dispose();
        }

        [Fact]
        public async Task EventToInvalidation_Should_Complete_Within_SLA()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            var keyHash = "test_key_" + Guid.NewGuid();
            var cache = _serviceProvider.GetRequiredService<IVirtualKeyCache>();
            
            // Seed cache - we need to use the database fallback pattern
            // The cache doesn't have a SetVirtualKeyAsync method
            var testKey = new ConduitLLM.Configuration.Entities.VirtualKey 
            { 
                Id = 1,
                KeyHash = keyHash,
                IsEnabled = true,
                KeyName = "Test Key"
            };
            
            // Manually set in Redis for testing
            var serialized = System.Text.Json.JsonSerializer.Serialize(testKey);
            await _database.StringSetAsync($"vkey:{keyHash}", serialized);

            // Verify key exists
            var exists = await _database.KeyExistsAsync($"vkey:{keyHash}");
            Assert.True(exists, "Key should exist in cache before invalidation");

            // Act - Publish event
            await _bus.Publish(new VirtualKeyDeleted 
            { 
                KeyId = 1, 
                KeyHash = keyHash,
                KeyName = "Test Key"
            });

            // Give time for event to be consumed and queued
            await Task.Delay(50);

            // Wait for invalidation
            var maxWaitTime = TimeSpan.FromMilliseconds(300); // Increased from 200ms to account for batch window
            while (stopwatch.Elapsed < maxWaitTime)
            {
                exists = await _database.KeyExistsAsync($"vkey:{keyHash}");
                if (!exists) break;
                await Task.Delay(10);
            }

            // Assert
            stopwatch.Stop();
            Assert.False(exists, "Key should be invalidated");
            Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(300), 
                $"Invalidation took {stopwatch.ElapsedMilliseconds}ms, expected < 300ms (batch window + processing)");
        }

        [Fact]
        public async Task BulkInvalidation_Should_Handle_High_Throughput()
        {
            // Arrange
            const int eventCount = 1000;
            var cache = _serviceProvider.GetRequiredService<IVirtualKeyCache>();
            var batchService = _serviceProvider.GetRequiredService<IBatchCacheInvalidationService>();
            
            // Seed cache with test keys
            var seedTasks = Enumerable.Range(1, eventCount)
                .Select(async i =>
                {
                    var keyHash = $"bulk_key_{i}";
                    var testKey = new ConduitLLM.Configuration.Entities.VirtualKey
                    {
                        Id = i,
                        KeyHash = keyHash,
                        IsEnabled = true,
                        KeyName = $"Bulk Key {i}"
                    };
                    var serialized = System.Text.Json.JsonSerializer.Serialize(testKey);
                    await _database.StringSetAsync($"vkey:{keyHash}", serialized);
                });
            await Task.WhenAll(seedTasks);

            // Act - Publish many events
            var publishTasks = Enumerable.Range(1, eventCount)
                .Select(i => _bus.Publish(new SpendUpdated
                {
                    KeyId = i,
                    KeyHash = $"bulk_key_{i}",
                    Amount = 0.001m,
                    NewTotalSpend = i * 0.001m
                }));
            
            await Task.WhenAll(publishTasks);

            // Wait for processing
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert - Check that keys were invalidated
            var remainingKeys = 0;
            for (int i = 1; i <= eventCount; i++)
            {
                if (await _database.KeyExistsAsync($"vkey:bulk_key_{i}"))
                {
                    remainingKeys++;
                }
            }

            // Allow for some tolerance due to timing
            Assert.True(remainingKeys < eventCount * 0.01, // Less than 1% should remain
                $"Expected most keys to be invalidated, but {remainingKeys} out of {eventCount} remain");

            // Check batch statistics
            var stats = await batchService.GetStatsAsync();
            Assert.True(stats.TotalProcessed >= eventCount * 0.99, // At least 99% processed
                $"Expected {eventCount} invalidations, got {stats.TotalProcessed}");
        }

        [Fact]
        public async Task Batch_Coalescing_Should_Reduce_Redis_Operations()
        {
            // Arrange
            var keyHash = "coalesce_key_" + Guid.NewGuid();
            var cache = _serviceProvider.GetRequiredService<IVirtualKeyCache>();
            var batchService = _serviceProvider.GetRequiredService<IBatchCacheInvalidationService>();
            
            // Seed cache
            var testKey = new ConduitLLM.Configuration.Entities.VirtualKey
            {
                Id = 1,
                KeyHash = keyHash,
                IsEnabled = true,
                KeyName = "Coalesce Test Key"
            };
            var serialized = System.Text.Json.JsonSerializer.Serialize(testKey);
            await _database.StringSetAsync($"vkey:{keyHash}", serialized);

            // Act - Publish same key multiple times rapidly
            var publishTasks = Enumerable.Range(1, 10)
                .Select(_ => _bus.Publish(new SpendUpdated
                {
                    KeyId = 1,
                    KeyHash = keyHash,
                    Amount = 0.001m,
                    NewTotalSpend = 0.01m
                }));
            
            await Task.WhenAll(publishTasks);

            // Wait for batch processing
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            // Assert
            var exists = await _database.KeyExistsAsync($"vkey:{keyHash}");
            Assert.False(exists, "Key should be invalidated");

            var stats = await batchService.GetStatsAsync();
            Assert.True(stats.TotalCoalesced > 0, "Should have coalesced duplicate requests");
            Assert.True(stats.CoalescingRate > 0, "Coalescing rate should be greater than 0");
        }

        [Fact]
        public async Task Redis_Batch_Operations_Should_Be_Faster_Than_Individual()
        {
            // Arrange
            var batchOps = _serviceProvider.GetRequiredService<IRedisBatchOperations>();
            var keys = Enumerable.Range(1, 100).Select(i => $"perf_test_{i}").ToArray();
            
            // Set test data
            foreach (var key in keys)
            {
                await _database.StringSetAsync(key, "test_value");
            }

            // Act - Individual deletes
            var individualStopwatch = Stopwatch.StartNew();
            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
            }
            individualStopwatch.Stop();

            // Reset test data
            foreach (var key in keys)
            {
                await _database.StringSetAsync(key, "test_value");
            }

            // Act - Batch delete
            var batchStopwatch = Stopwatch.StartNew();
            await batchOps.BatchDeleteAsync(keys);
            batchStopwatch.Stop();

            // Assert
            Assert.True(batchStopwatch.Elapsed < individualStopwatch.Elapsed,
                $"Batch operation ({batchStopwatch.ElapsedMilliseconds}ms) should be faster than individual ({individualStopwatch.ElapsedMilliseconds}ms)");
            
            // Verify all keys were deleted
            foreach (var key in keys)
            {
                var exists = await _database.KeyExistsAsync(key);
                Assert.False(exists, $"Key {key} should be deleted");
            }
        }

        [Fact]
        public async Task Critical_Events_Should_Be_Processed_Immediately()
        {
            // Arrange
            var keyHash = "critical_key_" + Guid.NewGuid();
            var cache = _serviceProvider.GetRequiredService<IVirtualKeyCache>();
            
            // Seed cache
            var testKey = new ConduitLLM.Configuration.Entities.VirtualKey
            {
                Id = 1,
                KeyHash = keyHash,
                IsEnabled = true,
                KeyName = "Critical Test Key"
            };
            var serialized = System.Text.Json.JsonSerializer.Serialize(testKey);
            await _database.StringSetAsync($"vkey:{keyHash}", serialized);

            // Act - Publish critical event (VirtualKeyDeleted)
            var stopwatch = Stopwatch.StartNew();
            await _bus.Publish(new VirtualKeyDeleted
            {
                KeyId = 1,
                KeyHash = keyHash,
                KeyName = "Critical Test Key"
            });

            // Wait for invalidation
            var maxWaitTime = TimeSpan.FromMilliseconds(100); // Should be very fast
            var exists = true;
            while (stopwatch.Elapsed < maxWaitTime && exists)
            {
                exists = await _database.KeyExistsAsync($"vkey:{keyHash}");
                if (exists) await Task.Delay(5);
            }
            stopwatch.Stop();

            // Assert
            Assert.False(exists, "Critical key should be invalidated immediately");
            Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(50),
                $"Critical invalidation took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");
        }
    }
}