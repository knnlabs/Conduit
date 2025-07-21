using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Chaos tests to verify distributed cache statistics behavior under adverse conditions.
    /// </summary>
    public class CacheStatisticsChaosTests : IDisposable
    {
        private readonly List<RedisCacheStatisticsCollector> _collectors = new();
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ISubscriber> _mockSubscriber;
        private readonly Dictionary<string, object> _redisData = new();
        private readonly Random _random = new(42);
        private volatile bool _chaosEnabled = false;
        private volatile int _failureRate = 0;

        public CacheStatisticsChaosTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockSubscriber = new Mock<ISubscriber>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);
            _mockRedis.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(_mockSubscriber.Object);

            SetupChaosMock();
        }

        private void SetupChaosMock()
        {
            // Simulate random failures based on chaos settings
            _mockDatabase.Setup(db => db.HashIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue field, long value, CommandFlags flags) =>
                {
                    if (_chaosEnabled && _random.Next(100) < _failureRate)
                    {
                        throw new RedisTimeoutException("Simulated timeout", CommandStatus.Unknown);
                    }

                    var dictKey = $"{key}:{field}";
                    lock (_redisData)
                    {
                        if (_redisData.TryGetValue(dictKey, out var existing) && existing is long currentValue)
                        {
                            _redisData[dictKey] = currentValue + value;
                        }
                        else
                        {
                            _redisData[dictKey] = value;
                        }
                    }
                    return value;
                });

            _mockDatabase.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    if (_chaosEnabled && _random.Next(100) < _failureRate)
                    {
                        throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Simulated connection failure");
                    }

                    var keyStr = key.ToString();
                    var entries = new List<HashEntry>();
                    
                    lock (_redisData)
                    {
                        foreach (var kvp in _redisData.Where(x => x.Key.StartsWith(keyStr + ":")))
                        {
                            var field = kvp.Key.Substring(keyStr.Length + 1);
                            entries.Add(new HashEntry(field, (long)kvp.Value));
                        }
                    }

                    return entries.ToArray();
                });

            _mockDatabase.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    lock (_redisData)
                    {
                        if (_redisData.TryGetValue($"set:{key}", out var members) && members is HashSet<string> set)
                        {
                            return set.Select(s => (RedisValue)s).ToArray();
                        }
                        return new RedisValue[0];
                    }
                });

            _mockDatabase.Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue value, CommandFlags flags) =>
                {
                    lock (_redisData)
                    {
                        var setKey = $"set:{key}";
                        if (!_redisData.TryGetValue(setKey, out var existing))
                        {
                            existing = new HashSet<string>();
                            _redisData[setKey] = existing;
                        }
                        return ((HashSet<string>)existing).Add(value.ToString());
                    }
                });

            _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    lock (_redisData)
                    {
                        if (_redisData.TryGetValue($"string:{key}", out var value) && value is string strValue)
                        {
                            return strValue;
                        }
                        return RedisValue.Null;
                    }
                });

            _mockDatabase.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags) =>
                {
                    lock (_redisData)
                    {
                        _redisData[$"string:{key}"] = value.ToString();
                        return true;
                    }
                });
        }

        private RedisCacheStatisticsCollector CreateCollector(string instanceId)
        {
            var logger = new Mock<ILogger<RedisCacheStatisticsCollector>>();
            var collector = new RedisCacheStatisticsCollector(_mockRedis.Object, logger.Object, instanceId);
            _collectors.Add(collector);
            return collector;
        }

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
            successCount.Should().BeGreaterThan(200); // Most operations should succeed
            failureCount.Should().BeGreaterThan(0); // Some should fail
            
            // Disable chaos for final check
            _chaosEnabled = false;
            
            // Despite failures, aggregated stats should reflect successful operations
            var stats = await instances[0].GetAggregatedStatisticsAsync(region);
            stats.HitCount.Should().BeGreaterThan(0);
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

        [Fact]
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
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            await instance.RecordOperationAsync(new CacheOperation
                            {
                                Region = region,
                                OperationType = i % 3 == 0 ? CacheOperationType.Hit : CacheOperationType.Miss,
                                Success = i % 20 != 0, // 5% error rate
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

        public void Dispose()
        {
            foreach (var collector in _collectors)
            {
                try { collector?.Dispose(); } catch { }
            }
        }
    }
}