using FluentAssertions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CacheStatisticsChaosTests
    {
        [Fact]
        [Trait("Category", "TimingSensitive")]
        public async Task MassiveLoad_ThousandsOfOperations_SystemStable()
        {
            // Arrange
            const int instanceCount = 10;
            const int operationsPerInstance = 1000;
            
            var instances = new List<RedisCacheStatisticsCollector>();
            for (int i = 0; i < instanceCount; i++)
            {
                var instance = CreateCollector($"load-instance-{i}");
                await instance.RegisterInstanceAsync();
                instances.Add(instance);
            }

            var region = CacheRegion.ProviderHealth;
            var completedOperations = 0;

            // Act - Massive concurrent load
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(50); // Limit concurrency
            
            foreach (var instance in instances)
            {
                for (int i = 0; i < operationsPerInstance; i++)
                {
                    var operationIndex = i; // Capture loop variable
                    var currentInstance = instance; // Capture instance
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            await currentInstance.RecordOperationAsync(new CacheOperation
                            {
                                Region = region,
                                OperationType = operationIndex % 3 == 0 ? CacheOperationType.Hit : CacheOperationType.Miss,
                                Success = operationIndex % 20 != 0, // 5% error rate
                                Duration = TimeSpan.FromMilliseconds(_random.Next(1, 50))
                            });
                            Interlocked.Increment(ref completedOperations);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);

            // Assert
            completedOperations.Should().Be(instanceCount * operationsPerInstance);
            
            var stats = await instances[0].GetAggregatedStatisticsAsync(region);
            stats.TotalRequests.Should().Be(instanceCount * operationsPerInstance);
            var expectedErrors = (long)(instanceCount * operationsPerInstance * 0.05);
            stats.ErrorCount.Should().BeCloseTo(expectedErrors, 50); // ~5% errors
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public async Task RapidInstanceChurn_FrequentJoinLeave_StatsRemainAccurate()
        {
            // Arrange
            var stableInstance = CreateCollector("stable-instance");
            await stableInstance.RegisterInstanceAsync();
            
            var region = CacheRegion.ModelMetadata;
            var churnInstances = new List<RedisCacheStatisticsCollector>();

            // Act - Rapid instance churn
            for (int wave = 0; wave < 5; wave++)
            {
                // Create and register new instances
                var waveInstances = new List<RedisCacheStatisticsCollector>();
                for (int i = 0; i < 3; i++)
                {
                    var instance = CreateCollector($"churn-wave{wave}-instance{i}");
                    await instance.RegisterInstanceAsync();
                    waveInstances.Add(instance);
                    churnInstances.Add(instance);
                }

                // Perform operations
                foreach (var instance in waveInstances.Concat(new[] { stableInstance }))
                {
                    await instance.RecordOperationAsync(new CacheOperation
                    {
                        Region = region,
                        OperationType = CacheOperationType.Set,
                        Success = true,
                        Duration = TimeSpan.FromMilliseconds(10),
                        DataSizeBytes = 1024
                    });
                }

                // Unregister wave instances (simulate departure)
                foreach (var instance in waveInstances)
                {
                    await instance.UnregisterInstanceAsync();
                }
            }

            // Assert - Stats should account for all operations
            var finalStats = await stableInstance.GetAggregatedStatisticsAsync(region);
            finalStats.SetCount.Should().Be(20); // 5 waves * 4 instances per wave (3 churn + 1 stable)
            
            var activeInstances = (await stableInstance.GetActiveInstancesAsync()).ToList();
            activeInstances.Should().HaveCount(1); // Only stable instance remains
            activeInstances.Should().Contain("stable-instance");
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public async Task MemoryPressure_LargeDataVolumes_GracefulHandling()
        {
            // Arrange
            var instance = CreateCollector("memory-test");
            await instance.RegisterInstanceAsync();
            
            var region = CacheRegion.ProviderResponses;
            var largeDataSize = 10 * 1024 * 1024; // 10MB per operation

            // Act - Record operations with large data sizes
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(instance.RecordOperationAsync(new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Set,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(50),
                    DataSizeBytes = largeDataSize
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var stats = await instance.GetStatisticsAsync(region);
            stats.MemoryUsageBytes.Should().Be(100L * largeDataSize); // 1GB total
            stats.SetCount.Should().Be(100);
        }
    }
}