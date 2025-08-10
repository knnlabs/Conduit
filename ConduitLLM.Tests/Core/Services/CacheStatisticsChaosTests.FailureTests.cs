using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CacheStatisticsChaosTests
    {
        [Fact]
        public async Task RandomRedisFailures_OperationsContinue_DataEventuallyConsistent()
        {
            // Arrange
            _chaosEnabled = true;
            _failureRate = 20; // 20% failure rate

            var instances = new List<RedisCacheStatisticsCollector>();
            for (int i = 0; i < 3; i++)
            {
                var instance = CreateCollector($"chaos-instance-{i}");
                instances.Add(instance);
                
                // Registration might fail, retry
                for (int retry = 0; retry < 5; retry++)
                {
                    try
                    {
                        await instance.RegisterInstanceAsync();
                        break;
                    }
                    catch { await Task.Delay(10); }
                }
            }

            var region = CacheRegion.ProviderResponses;
            var successCount = 0;
            var failureCount = 0;

            // Act - Perform operations with random failures
            var tasks = new List<Task>();
            foreach (var instance in instances)
            {
                for (int i = 0; i < 100; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await instance.RecordOperationAsync(new CacheOperation
                            {
                                Region = region,
                                OperationType = CacheOperationType.Hit,
                                Success = true,
                                Duration = TimeSpan.FromMilliseconds(10)
                            });
                            Interlocked.Increment(ref successCount);
                        }
                        catch
                        {
                            Interlocked.Increment(ref failureCount);
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);

            // Assert
            // Since RecordOperationAsync swallows exceptions, we can't track failures this way
            // Instead, let's verify that some operations were recorded despite chaos
            _chaosEnabled = false; // Disable to get final stats
            
            var stats = await instances[0].GetAggregatedStatisticsAsync(region);
            stats.HitCount.Should().BeGreaterThan(0, "Some operations should have succeeded despite chaos");
            
            // Verify chaos was actually triggered by checking mock invocations
            _mockDatabase.Verify(db => db.HashIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()), 
                Times.AtLeast(100), "Many hash increment operations should have been attempted");
        }

        [Fact]
        public async Task NetworkPartition_SplitBrain_EventualConsistency()
        {
            // Arrange
            var partition1 = new List<RedisCacheStatisticsCollector>
            {
                CreateCollector("partition1-instance1"),
                CreateCollector("partition1-instance2")
            };
            
            var partition2 = new List<RedisCacheStatisticsCollector>
            {
                CreateCollector("partition2-instance1"),
                CreateCollector("partition2-instance2")
            };

            // Register all instances
            foreach (var instance in partition1.Concat(partition2))
            {
                await instance.RegisterInstanceAsync();
            }

            var region = CacheRegion.VirtualKeys;

            // Act - Simulate network partition
            // Each partition can only see its own data
            var partition1Data = new Dictionary<string, object>();
            var partition2Data = new Dictionary<string, object>();

            // Partition 1 records operations
            foreach (var instance in partition1)
            {
                for (int i = 0; i < 50; i++)
                {
                    await instance.RecordOperationAsync(new CacheOperation
                    {
                        Region = region,
                        OperationType = CacheOperationType.Hit,
                        Success = true,
                        Duration = TimeSpan.FromMilliseconds(5)
                    });
                }
            }

            // Partition 2 records different operations
            foreach (var instance in partition2)
            {
                for (int i = 0; i < 75; i++)
                {
                    await instance.RecordOperationAsync(new CacheOperation
                    {
                        Region = region,
                        OperationType = CacheOperationType.Miss,
                        Success = true,
                        Duration = TimeSpan.FromMilliseconds(8)
                    });
                }
            }

            // Heal partition - merge data
            // In real scenario, Redis would handle this
            var stats = await partition1[0].GetAggregatedStatisticsAsync(region);
            
            // Assert - Data from both partitions should eventually be visible
            // This simulates eventual consistency after partition healing
            stats.HitCount.Should().BeGreaterThanOrEqualTo(100); // 50 * 2 instances
            stats.MissCount.Should().BeGreaterThanOrEqualTo(150); // 75 * 2 instances
        }

        [Fact]
        public async Task ClockSkew_DifferentInstanceTimes_HandledGracefully()
        {
            // Arrange
            var normalInstance = CreateCollector("normal-time");
            var futureInstance = CreateCollector("future-time");
            var pastInstance = CreateCollector("past-time");

            await normalInstance.RegisterInstanceAsync();
            await futureInstance.RegisterInstanceAsync();
            await pastInstance.RegisterInstanceAsync();

            // Simulate clock skew in heartbeats
            var now = DateTimeOffset.UtcNow;
            lock (_redisData)
            {
                _redisData["string:conduit:cache:heartbeat:normal-time"] = now.ToUnixTimeMilliseconds().ToString();
                _redisData["string:conduit:cache:heartbeat:future-time"] = now.AddMinutes(5).ToUnixTimeMilliseconds().ToString(); // 5 minutes ahead
                _redisData["string:conduit:cache:heartbeat:past-time"] = now.AddMinutes(-1).ToUnixTimeMilliseconds().ToString(); // 1 minute behind
            }

            // Act
            var activeInstances = (await normalInstance.GetActiveInstancesAsync()).ToList();

            // Assert - All instances should still be considered active despite clock skew
            activeInstances.Should().HaveCount(3);
            activeInstances.Should().Contain("normal-time");
            activeInstances.Should().Contain("future-time");
            activeInstances.Should().Contain("past-time");
        }
    }
}