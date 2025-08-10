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
    /// Setup and helper methods for distributed cache statistics integration tests.
    /// </summary>
    public partial class DistributedCacheStatisticsIntegrationTests : IDisposable
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

        public void Dispose()
        {
            foreach (var collector in _collectors)
            {
                collector?.Dispose();
            }
        }
    }
}