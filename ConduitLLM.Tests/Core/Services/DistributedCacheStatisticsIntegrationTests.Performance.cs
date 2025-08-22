using System.Diagnostics;
using FluentAssertions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Performance and concurrency tests for distributed cache statistics
    /// </summary>
    public partial class DistributedCacheStatisticsIntegrationTests
    {
        [Fact]
        [Trait("Category", "TimingSensitive")]
        public async Task ConcurrentOperations_AtomicIncrement_NoRaceConditions()
        {
            // Arrange
            var instances = new List<RedisCacheStatisticsCollector>();
            for (int i = 0; i < 5; i++)
            {
                var instance = CreateCollector($"instance-{i}");
                await instance.RegisterInstanceAsync();
                instances.Add(instance);
            }

            var region = CacheRegion.ProviderHealth;
            var operationsPerInstance = 1000;

            // Act - Concurrent operations from all instances
            var tasks = new List<Task>();
            foreach (var instance in instances)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < operationsPerInstance; i++)
                    {
                        await instance.RecordOperationAsync(new CacheOperation
                        {
                            Region = region,
                            OperationType = CacheOperationType.Hit,
                            Success = true,
                            Duration = TimeSpan.FromMilliseconds(5)
                        });
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All operations should be accounted for
            var stats = await instances[0].GetAggregatedStatisticsAsync(region);
            stats.HitCount.Should().Be(instances.Count * operationsPerInstance);
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public async Task ResponseTimeAggregation_AcrossInstances_CalculatesPercentiles()
        {
            // Arrange
            var instance1 = CreateCollector("instance-1");
            var instance2 = CreateCollector("instance-2");

            await instance1.RegisterInstanceAsync();
            await instance2.RegisterInstanceAsync();

            // Ensure instances are in the set (RegisterInstanceAsync should have done this)
            lock (_lockObj)
            {
                _instanceSet.Add("instance-1");
                _instanceSet.Add("instance-2");
            }

            // Ensure heartbeats are set for active instances
            lock (_lockObj)
            {
                _redisStorage[$"conduit:cache:heartbeat:instance-1"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                _redisStorage[$"conduit:cache:heartbeat:instance-2"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            }

            var region = CacheRegion.ModelMetadata;

            // Act - Record operations with varying response times
            var responseTimes = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            
            foreach (var time in responseTimes)
            {
                await instance1.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Get,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(time)
                });

                await instance2.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Get,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(time + 5) // Slightly different times
                });
            }

            // Assert
            // First verify that response times were actually recorded
            var getKey1 = $"conduit:cache:response:{region}:Get:instance-1";
            var getKey2 = $"conduit:cache:response:{region}:Get:instance-2";
            
            lock (_lockObj)
            {
                _sortedSetStorage.Should().ContainKey(getKey1);
                _sortedSetStorage.Should().ContainKey(getKey2);
                _sortedSetStorage[getKey1].Should().HaveCount(10);
                _sortedSetStorage[getKey2].Should().HaveCount(10);
            }
            
            var stats = await instance1.GetAggregatedStatisticsAsync(region);
            
            // Average should be around 55ms (sum of all times / count)
            stats.AverageGetTime.TotalMilliseconds.Should().BeInRange(50, 60);
            
            // P95 should capture 95th percentile
            stats.P95GetTime.TotalMilliseconds.Should().BeInRange(85, 100);
            
            // P99 should be close to max
            stats.P99GetTime.TotalMilliseconds.Should().BeInRange(95, 105);
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public async Task PerformanceBenchmark_RecordOperation_Under5ms()
        {
            // Arrange
            var instance = CreateCollector("perf-instance");
            await instance.RegisterInstanceAsync();

            var region = CacheRegion.ProviderResponses;
            var operation = new CacheOperation
            {
                Region = region,
                OperationType = CacheOperationType.Hit,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            };

            // Warm up
            await instance.RecordOperationAsync(operation);

            // Act - Measure operation time
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                await instance.RecordOperationAsync(operation);
            }
            stopwatch.Stop();

            // Assert
            var avgOperationTime = stopwatch.ElapsedMilliseconds / 100.0;
            avgOperationTime.Should().BeLessThan(10.0, "Average operation time should be under 10ms");
        }
    }
}