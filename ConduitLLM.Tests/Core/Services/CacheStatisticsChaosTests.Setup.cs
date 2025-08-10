using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Chaos tests to verify distributed cache statistics behavior under adverse conditions.
    /// </summary>
    public partial class CacheStatisticsChaosTests : IDisposable
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

            _mockDatabase.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue value, CommandFlags flags) =>
                {
                    lock (_redisData)
                    {
                        var setKey = $"set:{key}";
                        if (_redisData.TryGetValue(setKey, out var existing) && existing is HashSet<string> set)
                        {
                            return set.Remove(value.ToString());
                        }
                        return false;
                    }
                });

            _mockDatabase.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    lock (_redisData)
                    {
                        var keyStr = key.ToString();
                        var strKey = $"string:{keyStr}";
                        return _redisData.Remove(strKey);
                    }
                });

            // Add chaos support for SortedSetAddAsync - without When parameter (matching actual usage)
            _mockDatabase.Setup(db => db.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue member, double score, CommandFlags flags) =>
                {
                    if (_chaosEnabled && _random.Next(100) < _failureRate)
                    {
                        throw new RedisTimeoutException("Simulated timeout", CommandStatus.Unknown);
                    }
                    
                    lock (_redisData)
                    {
                        var sortedSetKey = $"sortedset:{key}";
                        if (!_redisData.TryGetValue(sortedSetKey, out var existing))
                        {
                            existing = new SortedSet<(double score, string member)>(Comparer<(double, string)>.Create((a, b) => 
                            {
                                var scoreComp = a.Item1.CompareTo(b.Item1);
                                return scoreComp != 0 ? scoreComp : string.Compare(a.Item2, b.Item2, StringComparison.Ordinal);
                            }));
                            _redisData[sortedSetKey] = existing;
                        }
                        var sortedSet = (SortedSet<(double, string)>)existing;
                        return sortedSet.Add((score, member.ToString()));
                    }
                });
                
            // Also support the overload with When parameter
            _mockDatabase.Setup(db => db.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue member, double score, When when, CommandFlags flags) =>
                {
                    if (_chaosEnabled && _random.Next(100) < _failureRate)
                    {
                        throw new RedisTimeoutException("Simulated timeout", CommandStatus.Unknown);
                    }
                    
                    lock (_redisData)
                    {
                        var sortedSetKey = $"sortedset:{key}";
                        if (!_redisData.TryGetValue(sortedSetKey, out var existing))
                        {
                            existing = new SortedSet<(double score, string member)>(Comparer<(double, string)>.Create((a, b) => 
                            {
                                var scoreComp = a.Item1.CompareTo(b.Item1);
                                return scoreComp != 0 ? scoreComp : string.Compare(a.Item2, b.Item2, StringComparison.Ordinal);
                            }));
                            _redisData[sortedSetKey] = existing;
                        }
                        var sortedSet = (SortedSet<(double, string)>)existing;
                        return sortedSet.Add((score, member.ToString()));
                    }
                });

            // Add chaos support for SortedSetRemoveRangeByRankAsync
            _mockDatabase.Setup(db => db.SortedSetRemoveRangeByRankAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, long start, long stop, CommandFlags flags) =>
                {
                    if (_chaosEnabled && _random.Next(100) < _failureRate)
                    {
                        throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Simulated connection failure");
                    }
                    return 0;
                });

            // Add chaos support for PublishAsync
            _mockDatabase.Setup(db => db.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisChannel channel, RedisValue message, CommandFlags flags) =>
                {
                    if (_chaosEnabled && _random.Next(100) < _failureRate)
                    {
                        throw new RedisTimeoutException("Simulated publish timeout", CommandStatus.Unknown);
                    }
                    return 1;
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

            // Add mock for hash set operations (alerts)
            _mockDatabase.Setup(db => db.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
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
                try { collector?.Dispose(); } catch { }
            }
        }
    }
}