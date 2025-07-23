using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Integration tests for distributed cache statistics collection across multiple instances.
    /// </summary>
    public class DistributedCacheStatisticsIntegrationTests : IDisposable
    {
        private readonly List<RedisCacheStatisticsCollector> _collectors = new();
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ISubscriber> _mockSubscriber;
        private readonly Dictionary<string, RedisValue> _redisStorage = new();
        private readonly Dictionary<string, HashEntry[]> _hashStorage = new();
        private readonly HashSet<string> _instanceSet = new();
        private readonly object _lockObj = new object(); // For thread-safe operations
        private readonly Dictionary<string, List<(RedisValue member, double score)>> _sortedSetStorage = new();

        public DistributedCacheStatisticsIntegrationTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockSubscriber = new Mock<ISubscriber>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);
            _mockRedis.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(_mockSubscriber.Object);

            // Setup Redis mock to simulate distributed storage
            SetupRedisMock();
        }

        private void SetupRedisMock()
        {
            // Hash operations
            _mockDatabase.Setup(db => db.HashIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue field, long value, CommandFlags flags) =>
                {
                    lock (_lockObj) // Ensure thread-safe operations
                    {
                        var keyStr = key.ToString();
                        if (!_hashStorage.ContainsKey(keyStr))
                            _hashStorage[keyStr] = new HashEntry[0];

                        var entries = _hashStorage[keyStr].ToList();
                        var existingEntry = entries.FirstOrDefault(e => e.Name == field);
                        
                        if (existingEntry.Name.HasValue)
                        {
                            entries.Remove(existingEntry);
                            var currentValue = existingEntry.Value.TryParse(out long current) ? current : 0;
                            entries.Add(new HashEntry(field, currentValue + value));
                        }
                        else
                        {
                            entries.Add(new HashEntry(field, value));
                        }

                        _hashStorage[keyStr] = entries.ToArray();
                        return value;
                    }
                });

            _mockDatabase.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        var keyStr = key.ToString();
                        return _hashStorage.ContainsKey(keyStr) ? _hashStorage[keyStr] : new HashEntry[0];
                    }
                });

            // Set operations for instance tracking
            _mockDatabase.Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue value, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        _instanceSet.Add(value.ToString());
                        return true;
                    }
                });

            _mockDatabase.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue value, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        return _instanceSet.Remove(value.ToString());
                    }
                });

            _mockDatabase.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        return _instanceSet.Select(i => (RedisValue)i).ToArray();
                    }
                });

            // String operations for heartbeat
            _mockDatabase.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        _redisStorage[key.ToString()] = value;
                        return true;
                    }
                });

            _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        var keyStr = key.ToString();
                        return _redisStorage.ContainsKey(keyStr) ? _redisStorage[keyStr] : RedisValue.Null;
                    }
                });

            // Sorted set operations for response times - matching actual usage without When parameter
            _mockDatabase.Setup(db => db.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue member, double score, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        var keyStr = key.ToString();
                        if (!_sortedSetStorage.ContainsKey(keyStr))
                            _sortedSetStorage[keyStr] = new List<(RedisValue, double)>();
                        
                        _sortedSetStorage[keyStr].Add((member, score));
                        return true;
                    }
                });
                
            // Also setup the overload with SortedSetWhen parameter (some implementations may use this)
            _mockDatabase.Setup(db => db.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue member, double score, SortedSetWhen when, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        var keyStr = key.ToString();
                        if (!_sortedSetStorage.ContainsKey(keyStr))
                            _sortedSetStorage[keyStr] = new List<(RedisValue, double)>();
                        
                        _sortedSetStorage[keyStr].Add((member, score));
                        return true;
                    }
                });
            
            // Also setup the overload with When parameter for other tests
            _mockDatabase.Setup(db => db.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue member, double score, When when, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        var keyStr = key.ToString();
                        if (!_sortedSetStorage.ContainsKey(keyStr))
                            _sortedSetStorage[keyStr] = new List<(RedisValue, double)>();
                        
                        _sortedSetStorage[keyStr].Add((member, score));
                        return true;
                    }
                });

            _mockDatabase.Setup(db => db.SortedSetRangeByRankWithScoresAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, long start, long stop, Order order, CommandFlags flags) =>
                {
                    lock (_lockObj)
                    {
                        var keyStr = key.ToString();
                        if (!_sortedSetStorage.ContainsKey(keyStr))
                            return new SortedSetEntry[0];

                        var entries = _sortedSetStorage[keyStr]
                            .Select(e => new SortedSetEntry(e.member, e.score))
                            .OrderBy(e => e.Score)
                            .ToArray();

                        return entries;
                    }
                });
            
            // Sorted set remove operations
            _mockDatabase.Setup(db => db.SortedSetRemoveRangeByRankAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, long start, long stop, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    if (!_sortedSetStorage.ContainsKey(keyStr))
                        return 0;
                    
                    var list = _sortedSetStorage[keyStr];
                    var orderedList = list.OrderBy(e => e.score).ToList();
                    var removeCount = 0;
                    
                    // Handle negative indices (from end)
                    if (stop < 0)
                    {
                        stop = orderedList.Count + stop;
                    }
                    
                    // Remove elements in range
                    for (long i = start; i <= stop && i < orderedList.Count; i++)
                    {
                        if (i >= 0)
                        {
                            removeCount++;
                        }
                    }
                    
                    // Keep only elements outside the range
                    if (start == 0 && stop < 0)
                    {
                        // Keep only the last N elements
                        var keepCount = -stop - 1;
                        if (keepCount > 0 && keepCount < orderedList.Count)
                        {
                            _sortedSetStorage[keyStr] = orderedList.Skip(orderedList.Count - (int)keepCount).ToList();
                        }
                    }
                    
                    return removeCount;
                });
            
            // Publish operations for stats updates
            _mockDatabase.Setup(db => db.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisChannel channel, RedisValue message, CommandFlags flags) =>
                {
                    // Just return 1 to indicate success
                    return 1;
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
        public async Task InstanceFailure_RemainingInstances_ContinueWorking()
        {
            // Arrange
            var instance1 = CreateCollector("instance-1");
            var instance2 = CreateCollector("instance-2");
            var instance3 = CreateCollector("instance-3");

            await instance1.RegisterInstanceAsync();
            await instance2.RegisterInstanceAsync();
            await instance3.RegisterInstanceAsync();

            var region = CacheRegion.VirtualKeys;

            // Act - Record some operations on all instances
            var operation = new CacheOperation
            {
                Region = region,
                OperationType = CacheOperationType.Hit,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            };

            await instance1.RecordOperationAsync(operation);
            await instance2.RecordOperationAsync(operation);
            await instance3.RecordOperationAsync(operation);

            // Simulate instance2 failure
            await instance2.UnregisterInstanceAsync();
            instance2.Dispose();

            // Continue operations on remaining instances
            await instance1.RecordOperationAsync(operation);
            await instance3.RecordOperationAsync(operation);

            // Assert
            var activeInstances = (await instance1.GetActiveInstancesAsync()).ToList();
            activeInstances.Should().HaveCount(2);
            activeInstances.Should().Contain("instance-1");
            activeInstances.Should().Contain("instance-3");
            activeInstances.Should().NotContain("instance-2");

            var stats = await instance1.GetAggregatedStatisticsAsync(region);
            stats.HitCount.Should().Be(5); // 3 initial + 2 after failure
        }

        [Fact]
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
        public async Task InstanceRegistration_Heartbeat_TracksActiveInstances()
        {
            // Arrange
            var instance1 = CreateCollector("instance-1");
            var instance2 = CreateCollector("instance-2");
            
            // Act - Register only instance1 and instance2
            await instance1.RegisterInstanceAsync();
            await instance2.RegisterInstanceAsync();
            
            // Manually add instance-3 to the set without registering (so no heartbeat timer)
            lock (_lockObj)
            {
                _instanceSet.Add("instance-3");
                // Set heartbeat times - instance-3 will have an expired heartbeat
                _redisStorage[$"conduit:cache:heartbeat:instance-1"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                _redisStorage[$"conduit:cache:heartbeat:instance-2"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds().ToString();
                _redisStorage[$"conduit:cache:heartbeat:instance-3"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds().ToString(); // Expired
            }

            // Assert
            var activeInstances = (await instance1.GetActiveInstancesAsync()).ToList();
            activeInstances.Should().HaveCount(2);
            activeInstances.Should().Contain("instance-1");
            activeInstances.Should().Contain("instance-2");
            activeInstances.Should().NotContain("instance-3"); // Expired heartbeat
            
            // Clean up
            instance1.Dispose();
            instance2.Dispose();
        }

        [Fact]
        public async Task RedisConnectionFailure_GracefulDegradation()
        {
            // Arrange
            var instance = CreateCollector("instance-1");
            await instance.RegisterInstanceAsync();

            var region = CacheRegion.ProviderResponses;

            // Act - Simulate Redis failure during operation
            var callCount = 0;
            _mockDatabase.Setup(db => db.HashIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .Returns((RedisKey key, RedisValue field, long value, CommandFlags flags) =>
                {
                    callCount++;
                    if (callCount > 3)
                        throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed");
                    
                    return Task.FromResult(1L);
                });

            // Record multiple operations to trigger failure
            var exceptions = new List<Exception>();
            for (int i = 0; i < 5; i++)
            {
                var operation = new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Hit,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(10),
                    DataSizeBytes = 100 // Add data size to trigger more HashIncrement calls
                };

                try
                {
                    await instance.RecordOperationAsync(operation);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            // Assert - Some operations should fail but not crash the system
            // Each operation with DataSizeBytes makes 4 HashIncrement calls (2 for Hit, 2 for DataBytes)
            // We make 5 operations, so we expect at least some calls before failure
            callCount.Should().BeGreaterThan(3);
            // RedisCacheStatisticsCollector handles errors gracefully, so no exceptions should bubble up
            exceptions.Should().BeEmpty("Redis failures should be handled gracefully without throwing exceptions");
        }

        [Fact]
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
            avgOperationTime.Should().BeLessThan(5.0, "Average operation time should be under 5ms");
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

        public void Dispose()
        {
            foreach (var collector in _collectors)
            {
                collector?.Dispose();
            }
        }
    }
}