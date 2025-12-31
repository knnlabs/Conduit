using FluentAssertions;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Aggregation tests for distributed cache statistics
    /// </summary>
    public partial class DistributedCacheStatisticsIntegrationTests
    {
        [Fact]
        public async Task MultipleInstances_RecordOperations_AggregatesCorrectly()
        {
            // Arrange
            var instance1 = CreateCollector("instance-1");
            var instance2 = CreateCollector("instance-2");
            var instance3 = CreateCollector("instance-3");

            await instance1.RegisterInstanceAsync();
            await instance2.RegisterInstanceAsync();
            await instance3.RegisterInstanceAsync();

            var region = CacheRegion.ProviderResponses;

            // Act - Each instance records different operations
            var tasks = new List<Task>();

            // Instance 1: 100 hits, 20 misses
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(instance1.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Hit,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(10 + i % 5)
                }));
            }
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(instance1.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Miss,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(5)
                }));
            }

            // Instance 2: 150 hits, 50 misses
            for (int i = 0; i < 150; i++)
            {
                tasks.Add(instance2.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Hit,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(8 + i % 3)
                }));
            }
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(instance2.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Miss,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(4)
                }));
            }

            // Instance 3: 80 hits, 40 misses, 5 errors
            for (int i = 0; i < 80; i++)
            {
                tasks.Add(instance3.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Hit,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(15 + i % 10)
                }));
            }
            for (int i = 0; i < 40; i++)
            {
                tasks.Add(instance3.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Miss,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(6)
                }));
            }
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(instance3.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Set,
                    Success = false,
                    Duration = TimeSpan.FromMilliseconds(20)
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Verify aggregated statistics
            var aggregatedStats = await instance1.GetAggregatedStatisticsAsync(region);

            aggregatedStats.HitCount.Should().Be(330); // 100 + 150 + 80
            aggregatedStats.MissCount.Should().Be(110); // 20 + 50 + 40
            aggregatedStats.ErrorCount.Should().Be(5);
            aggregatedStats.HitRate.Should().BeApproximately(75.0, 0.1); // 330 / (330 + 110) * 100

            // Verify per-instance statistics
            var perInstanceStats = await instance1.GetPerInstanceStatisticsAsync(region);
            perInstanceStats.Should().HaveCount(3);
            perInstanceStats["instance-1"].HitCount.Should().Be(100);
            perInstanceStats["instance-2"].HitCount.Should().Be(150);
            perInstanceStats["instance-3"].HitCount.Should().Be(80);
        }

        [Fact]
        public async Task DataSizeTacking_AcrossInstances_AggregatesCorrectly()
        {
            // Arrange
            var instance1 = CreateCollector("instance-1");
            var instance2 = CreateCollector("instance-2");

            await instance1.RegisterInstanceAsync();
            await instance2.RegisterInstanceAsync();

            var region = CacheRegion.VirtualKeys;

            // Act
            await instance1.RecordOperationAsync(new CacheOperation
            {
                Region = region,
                OperationType = CacheOperationType.Set,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10),
                DataSizeBytes = 1024 * 1024 // 1MB
            });

            await instance2.RecordOperationAsync(new CacheOperation
            {
                Region = region,
                OperationType = CacheOperationType.Set,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10),
                DataSizeBytes = 2 * 1024 * 1024 // 2MB
            });

            // Assert
            var stats = await instance1.GetAggregatedStatisticsAsync(region);
            stats.MemoryUsageBytes.Should().Be(3 * 1024 * 1024); // 3MB total
        }
    }
}