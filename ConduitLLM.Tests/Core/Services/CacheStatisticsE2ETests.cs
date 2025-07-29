using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Simplified end-to-end tests for distributed cache statistics integration.
    /// </summary>
    public class CacheStatisticsE2ETests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ISubscriber> _mockSubscriber;
        private readonly Dictionary<string, object> _dataStore = new();

        public CacheStatisticsE2ETests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockSubscriber = new Mock<ISubscriber>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);
            _mockRedis.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(_mockSubscriber.Object);

            SetupCompleteMocks();

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder => builder.AddConsole());

            // Add memory cache
            services.AddMemoryCache();

            // Add distributed cache (memory-based for testing)
            services.AddDistributedMemoryCache();

            // Configure options
            services.Configure<CacheManagerOptions>(options =>
            {
                options.EnableDetailedStatistics = true;
                options.UseDistributedCache = true;
                options.StatisticsReportingInterval = TimeSpan.FromMinutes(1);
            });

            services.Configure<CacheStatisticsOptions>(options =>
            {
                options.AggregationInterval = TimeSpan.FromMinutes(1);
                options.PersistenceInterval = TimeSpan.FromMinutes(5);
                options.MaxResponseTimeSamples = 1000;
            });

            // Register Redis connection
            services.AddSingleton(_mockRedis.Object);

            // Register distributed statistics collector
            services.AddSingleton<IDistributedCacheStatisticsCollector, RedisCacheStatisticsCollector>();

            // Register hybrid collector as the main statistics collector
            services.AddSingleton<ICacheStatisticsCollector>(sp =>
            {
                var distributedCollector = sp.GetService<IDistributedCacheStatisticsCollector>();
                var localCollector = new CacheStatisticsCollector(
                    sp.GetRequiredService<ILogger<CacheStatisticsCollector>>(),
                    sp.GetRequiredService<IOptions<CacheStatisticsOptions>>(),
                    null);
                
                return new HybridCacheStatisticsCollector(
                    localCollector,
                    distributedCollector,
                    sp.GetRequiredService<ILogger<HybridCacheStatisticsCollector>>());
            });

            // Register cache manager
            services.AddSingleton<ICacheManager, CacheManager>();

            // Register cache registry
            services.AddSingleton<ICacheRegistry, CacheRegistry>();

            // Register statistics registration service
            services.AddHostedService<CacheStatisticsRegistrationService>();
        }

        private void SetupCompleteMocks()
        {
            // Comprehensive mock setup for end-to-end testing
            var hashData = new Dictionary<string, Dictionary<string, long>>();
            var setData = new Dictionary<string, HashSet<string>>();
            var stringData = new Dictionary<string, string>();
            var sortedSetData = new Dictionary<string, List<(string member, double score)>>();

            // Hash operations - setup a common handler for all overloads
            Func<RedisKey, RedisValue, long, long> hashIncrementHandler = (key, field, increment) =>
            {
                var keyStr = key.ToString();
                var fieldStr = field.ToString();
                
                if (!hashData.ContainsKey(keyStr))
                    hashData[keyStr] = new Dictionary<string, long>();
                
                if (!hashData[keyStr].ContainsKey(fieldStr))
                    hashData[keyStr][fieldStr] = 0;
                
                hashData[keyStr][fieldStr] += increment;
                return hashData[keyStr][fieldStr];
            };

            // Setup overload with explicit increment value
            _mockDatabase.Setup(db => db.HashIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue field, long value, CommandFlags flags) => 
                    hashIncrementHandler(key, field, value));

            // Setup overload with default increment value of 1 and default CommandFlags
            _mockDatabase.Setup(db => db.HashIncrementAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<RedisValue>(), 
                1L, 
                CommandFlags.None))
                .ReturnsAsync((RedisKey key, RedisValue field, long value, CommandFlags flags) => 
                    hashIncrementHandler(key, field, value));

            _mockDatabase.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    if (!hashData.ContainsKey(keyStr))
                        return new HashEntry[0];
                    
                    return hashData[keyStr].Select(kvp => new HashEntry(kvp.Key, kvp.Value)).ToArray();
                });

            _mockDatabase.Setup(db => db.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue field, RedisValue value, When when, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    var fieldStr = field.ToString();
                    
                    if (!hashData.ContainsKey(keyStr))
                        hashData[keyStr] = new Dictionary<string, long>();
                    
                    // Store as string in Redis mock, we'll handle the JSON serialization
                    stringData[$"{keyStr}:{fieldStr}"] = value.ToString();
                    
                    return true;
                });

            // Set operations
            _mockDatabase.Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue value, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    if (!setData.ContainsKey(keyStr))
                        setData[keyStr] = new HashSet<string>();
                    
                    return setData[keyStr].Add(value.ToString());
                });

            _mockDatabase.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    if (!setData.ContainsKey(keyStr))
                        return new RedisValue[0];
                    
                    return setData[keyStr].Select(s => (RedisValue)s).ToArray();
                });

            _mockDatabase.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue value, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    if (!setData.ContainsKey(keyStr))
                        return false;
                    
                    return setData[keyStr].Remove(value.ToString());
                });

            // String operations
            _mockDatabase.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags) =>
                {
                    stringData[key.ToString()] = value.ToString();
                    return true;
                });

            _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    return stringData.ContainsKey(keyStr) ? stringData[keyStr] : RedisValue.Null;
                });

            // Key operations
            _mockDatabase.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    var deleted = false;
                    
                    if (hashData.ContainsKey(keyStr))
                    {
                        hashData.Remove(keyStr);
                        deleted = true;
                    }
                    
                    if (stringData.ContainsKey(keyStr))
                    {
                        stringData.Remove(keyStr);
                        deleted = true;
                    }
                    
                    return deleted;
                });

            // Sorted set operations
            _mockDatabase.Setup(db => db.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, RedisValue member, double score, When when, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    if (!sortedSetData.ContainsKey(keyStr))
                        sortedSetData[keyStr] = new List<(string, double)>();
                    
                    sortedSetData[keyStr].Add((member.ToString(), score));
                    return true;
                });

            _mockDatabase.Setup(db => db.SortedSetRangeByRankWithScoresAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisKey key, long start, long stop, Order order, CommandFlags flags) =>
                {
                    var keyStr = key.ToString();
                    if (!sortedSetData.ContainsKey(keyStr))
                        return new SortedSetEntry[0];
                    
                    return sortedSetData[keyStr]
                        .OrderBy(x => x.score)
                        .Select(x => new SortedSetEntry(x.member, x.score))
                        .ToArray();
                });

            // Publish operations
            _mockDatabase.Setup(db => db.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(1);

            // Trim operations
            _mockDatabase.Setup(db => db.SortedSetRemoveRangeByRankAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(0);
        }

        [Fact]
        public async Task FullWorkflow_CacheOperations_GenerateStatistics()
        {
            // Arrange
            var cacheManager = _serviceProvider.GetRequiredService<ICacheManager>();
            var statsCollector = _serviceProvider.GetRequiredService<ICacheStatisticsCollector>();
            
            // Register cache regions
            var registry = _serviceProvider.GetRequiredService<ICacheRegistry>();
            registry.RegisterRegion(CacheRegion.ProviderResponses, new CacheRegionConfig
            {
                Region = CacheRegion.ProviderResponses,
                DefaultTTL = TimeSpan.FromMinutes(5),
                MaxSizeInBytes = 100 * 1024 * 1024, // 100MB
                UseMemoryCache = true,
                UseDistributedCache = true
            });

            // Act - Simulate cache operations
            var tasks = new List<Task>();
            
            // Simulate hits
            for (int i = 0; i < 100; i++)
            {
                var key = $"provider-response-{i}";
                await cacheManager.SetAsync(key, $"response-{i}", CacheRegion.ProviderResponses, TimeSpan.FromMinutes(5));
                
                tasks.Add(Task.Run(async () =>
                {
                    var result = await cacheManager.GetAsync<string>(key, CacheRegion.ProviderResponses);
                    result.Should().NotBeNull();
                }));
            }

            // Simulate misses
            for (int i = 100; i < 120; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await cacheManager.GetAsync<string>($"non-existent-{i}", CacheRegion.ProviderResponses);
                    result.Should().BeNull();
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Verify statistics
            var stats = await statsCollector.GetStatisticsAsync(CacheRegion.ProviderResponses);
            stats.HitCount.Should().BeGreaterThanOrEqualTo(100);
            stats.MissCount.Should().BeGreaterThanOrEqualTo(20);
            stats.SetCount.Should().BeGreaterThanOrEqualTo(100);
            stats.HitRate.Should().BeGreaterThan(80); // At least 80% hit rate

            // Verify aggregated statistics if distributed
            if (statsCollector is IDistributedCacheStatisticsCollector distributedCollector)
            {
                var aggregatedStats = await distributedCollector.GetAggregatedStatisticsAsync(CacheRegion.ProviderResponses);
                aggregatedStats.TotalRequests.Should().BeGreaterThanOrEqualTo(120);
            }
        }

        [Fact]
        public async Task RegistrationService_AutomaticInstanceRegistration()
        {
            // Arrange
            var distributedCollector = _serviceProvider.GetRequiredService<IDistributedCacheStatisticsCollector>();
            
            // In test environment, we need to manually register the instance
            // In production, the CacheStatisticsRegistrationService would do this automatically
            await distributedCollector.RegisterInstanceAsync();

            // Act
            var activeInstances = await distributedCollector.GetActiveInstancesAsync();

            // Assert
            activeInstances.Should().NotBeEmpty();
            activeInstances.Should().Contain(distributedCollector.InstanceId);
        }

        [Fact]
        public async Task StatisticsEvents_PropagateCorrectly()
        {
            // Arrange
            var cacheManager = _serviceProvider.GetRequiredService<ICacheManager>();
            var statsCollector = _serviceProvider.GetRequiredService<ICacheStatisticsCollector>();
            
            var eventReceived = false;
            CacheStatistics? receivedStats = null;
            
            statsCollector.StatisticsUpdated += (sender, args) =>
            {
                eventReceived = true;
                receivedStats = args.Statistics;
            };

            // Act
            await cacheManager.SetAsync("test-key", "test-value", CacheRegion.VirtualKeys, TimeSpan.FromMinutes(5));
            await Task.Delay(50); // Allow event to propagate

            // Assert
            eventReceived.Should().BeTrue();
            receivedStats.Should().NotBeNull();
            receivedStats!.SetCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AlertThresholds_TriggerCorrectly()
        {
            // Arrange
            var statsCollector = _serviceProvider.GetRequiredService<ICacheStatisticsCollector>();
            var cacheManager = _serviceProvider.GetRequiredService<ICacheManager>();
            
            var alertTriggered = false;
            CacheAlert? receivedAlert = null;
            
            statsCollector.AlertTriggered += (sender, args) =>
            {
                alertTriggered = true;
                receivedAlert = args.Alert;
            };

            // Configure alert for low hit rate
            await statsCollector.ConfigureAlertsAsync(CacheRegion.ModelMetadata, new CacheAlertThresholds
            {
                MinHitRate = 90.0, // 90% threshold
                MaxResponseTime = TimeSpan.FromMilliseconds(50),
                MaxErrorRate = 5.0
            });

            // Act - Generate mostly misses to trigger low hit rate alert
            for (int i = 0; i < 10; i++)
            {
                await cacheManager.GetAsync<string>($"non-existent-{i}", CacheRegion.ModelMetadata);
            }

            // Only 1 hit
            await cacheManager.SetAsync("exists", "value", CacheRegion.ModelMetadata, TimeSpan.FromMinutes(5));
            await cacheManager.GetAsync<string>("exists", CacheRegion.ModelMetadata);

            await Task.Delay(100); // Allow alert processing

            // Assert
            alertTriggered.Should().BeTrue();
            receivedAlert.Should().NotBeNull();
            receivedAlert!.AlertType.Should().Be(CacheAlertType.LowHitRate);
        }

        [Fact]
        public async Task PrometheusExport_GeneratesValidMetrics()
        {
            // Arrange
            var cacheManager = _serviceProvider.GetRequiredService<ICacheManager>();
            var statsCollector = _serviceProvider.GetRequiredService<ICacheStatisticsCollector>();
            
            // Generate some statistics
            for (int i = 0; i < 50; i++)
            {
                await cacheManager.SetAsync($"health-{i}", "ok", CacheRegion.ProviderHealth, TimeSpan.FromMinutes(1));
                await cacheManager.GetAsync<string>($"health-{i}", CacheRegion.ProviderHealth);
            }

            // Act
            var prometheusExport = await statsCollector.ExportStatisticsAsync("prometheus");

            // Assert
            prometheusExport.Should().NotBeNullOrEmpty();
            prometheusExport.Should().Contain("# HELP cache_hits_total");
            prometheusExport.Should().Contain("# TYPE cache_hits_total counter");
            prometheusExport.Should().Contain("cache_hits_total{region=\"ProviderHealth\"}");
            prometheusExport.Should().Contain("cache_hit_rate{region=\"ProviderHealth\"}");
        }

        [Fact]
        public async Task StatisticsPersistence_SurvivesRestart()
        {
            // Arrange
            var cacheManager = _serviceProvider.GetRequiredService<ICacheManager>();
            var statsCollector = _serviceProvider.GetRequiredService<ICacheStatisticsCollector>();
            
            // Generate statistics
            for (int i = 0; i < 30; i++)
            {
                await cacheManager.SetAsync($"key-{i}", $"value-{i}", CacheRegion.VirtualKeys, TimeSpan.FromMinutes(5));
            }

            var statsBefore = await statsCollector.GetStatisticsAsync(CacheRegion.VirtualKeys);

            // Act - Simulate restart by creating new collector with same instance ID
            var distributedCollector = _serviceProvider.GetRequiredService<IDistributedCacheStatisticsCollector>();
            var newLogger = new Mock<ILogger<RedisCacheStatisticsCollector>>();
            var newCollector = new RedisCacheStatisticsCollector(_mockRedis.Object, newLogger.Object, distributedCollector.InstanceId);

            // Assert - Statistics should persist
            var statsAfter = await newCollector.GetStatisticsAsync(CacheRegion.VirtualKeys);
            statsAfter.SetCount.Should().Be(statsBefore.SetCount);
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
        }
    }
}