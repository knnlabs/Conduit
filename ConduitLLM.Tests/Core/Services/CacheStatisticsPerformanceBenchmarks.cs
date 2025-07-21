using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
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
    /// Performance benchmarks for distributed cache statistics operations.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class CacheStatisticsPerformanceBenchmarks
    {
        private RedisCacheStatisticsCollector _collector;
        private Mock<IConnectionMultiplexer> _mockRedis;
        private Mock<IDatabase> _mockDatabase;
        private Mock<ISubscriber> _mockSubscriber;
        private CacheOperation _sampleOperation;

        [GlobalSetup]
        public void Setup()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockSubscriber = new Mock<ISubscriber>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);
            _mockRedis.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(_mockSubscriber.Object);

            // Setup minimal mocks for benchmarking
            _mockDatabase.Setup(db => db.HashIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(1L);

            _mockDatabase.Setup(db => db.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _mockDatabase.Setup(db => db.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(1);

            _mockDatabase.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(new HashEntry[] 
                { 
                    new HashEntry("HitCount", 1000),
                    new HashEntry("MissCount", 100),
                    new HashEntry("SetCount", 500)
                });

            var logger = new Mock<ILogger<RedisCacheStatisticsCollector>>();
            _collector = new RedisCacheStatisticsCollector(_mockRedis.Object, logger.Object, "benchmark-instance");

            _sampleOperation = new CacheOperation
            {
                Region = CacheRegion.ProviderResponses,
                OperationType = CacheOperationType.Hit,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            };
        }

        [Benchmark]
        public async Task RecordSingleOperation()
        {
            await _collector.RecordOperationAsync(_sampleOperation);
        }

        [Benchmark]
        public async Task RecordOperationBatch10()
        {
            var operations = Enumerable.Range(0, 10).Select(_ => _sampleOperation).ToArray();
            await _collector.RecordOperationBatchAsync(operations);
        }

        [Benchmark]
        public async Task RecordOperationBatch100()
        {
            var operations = Enumerable.Range(0, 100).Select(_ => _sampleOperation).ToArray();
            await _collector.RecordOperationBatchAsync(operations);
        }

        [Benchmark]
        public async Task GetStatistics()
        {
            await _collector.GetStatisticsAsync(CacheRegion.ProviderResponses);
        }

        [Benchmark]
        public async Task GetAggregatedStatistics()
        {
            await _collector.GetAggregatedStatisticsAsync(CacheRegion.ProviderResponses);
        }

        [Benchmark]
        public async Task GetAllStatistics()
        {
            await _collector.GetAllStatisticsAsync();
        }
    }

    /// <summary>
    /// Performance tests to verify operations meet latency requirements.
    /// </summary>
    public class CacheStatisticsPerformanceTests : IDisposable
    {
        private readonly RedisCacheStatisticsCollector _collector;
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ISubscriber> _mockSubscriber;

        public CacheStatisticsPerformanceTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockSubscriber = new Mock<ISubscriber>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);
            _mockRedis.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(_mockSubscriber.Object);

            SetupPerformanceMocks();

            var logger = new Mock<ILogger<RedisCacheStatisticsCollector>>();
            _collector = new RedisCacheStatisticsCollector(_mockRedis.Object, logger.Object, "perf-test");
        }

        private void SetupPerformanceMocks()
        {
            // Simulate realistic Redis latencies
            _mockDatabase.Setup(db => db.HashIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .Returns(async (RedisKey key, RedisValue field, long value, CommandFlags flags) =>
                {
                    await Task.Delay(TimeSpan.FromMicroseconds(100)); // 0.1ms latency
                    return value;
                });

            _mockDatabase.Setup(db => db.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Returns(async (RedisKey key, RedisValue member, double score, When when, CommandFlags flags) =>
                {
                    await Task.Delay(TimeSpan.FromMicroseconds(150)); // 0.15ms latency
                    return true;
                });

            _mockDatabase.Setup(db => db.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .Returns(async (RedisChannel channel, RedisValue message, CommandFlags flags) =>
                {
                    await Task.Delay(TimeSpan.FromMicroseconds(50)); // 0.05ms latency
                    return 1;
                });

            _mockDatabase.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(async (RedisKey key, CommandFlags flags) =>
                {
                    await Task.Delay(TimeSpan.FromMicroseconds(200)); // 0.2ms latency
                    return new HashEntry[] 
                    { 
                        new HashEntry("HitCount", 1000),
                        new HashEntry("MissCount", 100)
                    };
                });
        }

        [Fact]
        public async Task RecordOperation_MeetsLatencyRequirement_Under5ms()
        {
            // Arrange
            var operation = new CacheOperation
            {
                Region = CacheRegion.ProviderResponses,
                OperationType = CacheOperationType.Hit,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            };

            // Warm up
            await _collector.RecordOperationAsync(operation);

            // Act - Measure 1000 operations
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                await _collector.RecordOperationAsync(operation);
            }
            stopwatch.Stop();

            // Assert
            var avgLatency = stopwatch.Elapsed.TotalMilliseconds / 1000;
            avgLatency.Should().BeLessThan(5.0, "Average operation latency should be under 5ms");
        }

        [Fact]
        public async Task ConcurrentOperations_HighThroughput_MaintainsPerformance()
        {
            // Arrange
            const int concurrentTasks = 50;
            const int operationsPerTask = 100;
            
            var operation = new CacheOperation
            {
                Region = CacheRegion.VirtualKeys,
                OperationType = CacheOperationType.Get,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(5)
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[concurrentTasks];
            
            for (int i = 0; i < concurrentTasks; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < operationsPerTask; j++)
                    {
                        await _collector.RecordOperationAsync(operation);
                    }
                });
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var totalOperations = concurrentTasks * operationsPerTask;
            var throughput = totalOperations / stopwatch.Elapsed.TotalSeconds;
            var avgLatency = stopwatch.Elapsed.TotalMilliseconds / totalOperations;

            throughput.Should().BeGreaterThan(1000, "Should handle > 1000 operations per second");
            avgLatency.Should().BeLessThan(5.0, "Average latency should remain under 5ms even under load");
        }

        [Fact]
        public async Task BatchOperations_ImprovedThroughput()
        {
            // Arrange
            var operations = new List<CacheOperation>();
            for (int i = 0; i < 100; i++)
            {
                operations.Add(new CacheOperation
                {
                    Region = CacheRegion.ProviderHealth,
                    OperationType = i % 2 == 0 ? CacheOperationType.Hit : CacheOperationType.Miss,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(i % 20)
                });
            }

            // Act - Compare individual vs batch
            var individualStopwatch = Stopwatch.StartNew();
            foreach (var op in operations)
            {
                await _collector.RecordOperationAsync(op);
            }
            individualStopwatch.Stop();

            var batchStopwatch = Stopwatch.StartNew();
            await _collector.RecordOperationBatchAsync(operations);
            batchStopwatch.Stop();

            // Assert
            batchStopwatch.ElapsedMilliseconds.Should().BeLessThan(
                individualStopwatch.ElapsedMilliseconds,
                "Batch operations should be faster than individual operations");
        }

        [Fact]
        public async Task GetStatistics_LowLatency_UnderCachedConditions()
        {
            // Arrange - Pre-populate some data
            var operation = new CacheOperation
            {
                Region = CacheRegion.ModelMetadata,
                OperationType = CacheOperationType.Hit,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            };
            
            for (int i = 0; i < 100; i++)
            {
                await _collector.RecordOperationAsync(operation);
            }

            // Act - Measure read performance
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                await _collector.GetStatisticsAsync(CacheRegion.ModelMetadata);
            }
            stopwatch.Stop();

            // Assert
            var avgReadLatency = stopwatch.Elapsed.TotalMilliseconds / 1000;
            avgReadLatency.Should().BeLessThan(1.0, "Read operations should be very fast (< 1ms)");
        }

        [Fact]
        public async Task MemoryEfficiency_LargeNumberOfOperations()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);
            const int operationCount = 10000;

            // Act
            for (int i = 0; i < operationCount; i++)
            {
                await _collector.RecordOperationAsync(new CacheOperation
                {
                    Region = (CacheRegion)(i % 5),
                    OperationType = (CacheOperationType)(i % 5),
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(i % 100),
                    DataSizeBytes = i % 1024
                });
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryGrowth = finalMemory - initialMemory;

            // Assert
            var memoryPerOperation = memoryGrowth / (double)operationCount;
            // Accounting for response time samples, internal data structures, and string allocations
            // Each operation stores metadata, updates counters, and may store response time samples
            // The collector maintains up to 1000 response time samples per operation type per region
            memoryPerOperation.Should().BeLessThan(1000, "Memory usage per operation should be reasonable considering response time tracking");
        }

        [Fact]
        public async Task ExportStatistics_Performance_LargeDataset()
        {
            // Arrange - Create statistics for all regions
            foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
            {
                for (int i = 0; i < 1000; i++)
                {
                    await _collector.RecordOperationAsync(new CacheOperation
                    {
                        Region = region,
                        OperationType = (CacheOperationType)(i % 5),
                        Success = i % 10 != 0,
                        Duration = TimeSpan.FromMilliseconds(i % 100)
                    });
                }
            }

            // Act - Measure export performance
            var stopwatch = Stopwatch.StartNew();
            var prometheusExport = await _collector.ExportStatisticsAsync("prometheus");
            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Export should complete quickly even with large datasets");
            prometheusExport.Should().NotBeNullOrEmpty();
            prometheusExport.Should().Contain("cache_hits_total");
        }

        public void Dispose()
        {
            _collector?.Dispose();
        }
    }

    /// <summary>
    /// Entry point for running benchmarks.
    /// </summary>
    public class BenchmarkRunner
    {
        [Fact(Skip = "Run manually for performance benchmarking")]
        public void RunBenchmarks()
        {
            var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<CacheStatisticsPerformanceBenchmarks>();
        }
    }
}