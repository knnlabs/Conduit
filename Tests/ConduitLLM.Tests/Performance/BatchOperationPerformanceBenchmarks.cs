using System.Diagnostics;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services.BatchOperations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using CoreVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Tests.Performance
{
    /// <summary>
    /// Performance benchmarks comparing V1 (legacy) and V2 (base class) batch operations.
    /// These tests measure throughput, latency, and resource usage.
    /// </summary>
    [Trait("Category", "Performance")]
    [Trait("Component", "Performance")]
    [Trait("Phase", "2")]
    public class BatchOperationPerformanceBenchmarks : TestBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<CoreVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ISpendNotificationService> _mockSpendNotificationService;
        private readonly Mock<IBatchOperationIdempotencyService> _mockIdempotencyService;

        public BatchOperationPerformanceBenchmarks(ITestOutputHelper output) : base(output)
        {
            _mockVirtualKeyService = new Mock<CoreVirtualKeyService>();
            _mockSpendNotificationService = new Mock<ISpendNotificationService>();
            _mockIdempotencyService = new Mock<IBatchOperationIdempotencyService>();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddDebug());
            services.AddScoped<IBatchOperationService, ConduitLLM.Core.Services.BatchOperationService>();
            services.AddSingleton<ITaskHub>(_ => new Mock<ITaskHub>().Object);
            services.AddSingleton<CoreVirtualKeyService>(_ => _mockVirtualKeyService.Object);
            services.AddSingleton<ISpendNotificationService>(_ => _mockSpendNotificationService.Object);
            services.AddSingleton<IBatchOperationIdempotencyService>(_ => _mockIdempotencyService.Object);
            services.AddScoped<BatchSpendUpdateOperation>();
            services.AddScoped<BatchSpendUpdateOperationV2>();

            _serviceProvider = services.BuildServiceProvider();

            // Setup mocks for fast processing
            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConduitLLM.Configuration.Entities.VirtualKey { Id = 1, KeyName = "Test" });

            _mockVirtualKeyService.Setup(s => s.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                .Returns(Task.FromResult(true));

            _mockSpendNotificationService.Setup(s => s.NotifySpendUpdatedAsync(
                    It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _mockIdempotencyService.Setup(s => s.IsOperationProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(500)]
        public async Task Benchmark_V1VsV2_SmallToMediumBatches(int itemCount)
        {
            // Arrange
            var items = GenerateTestItems(itemCount);
            var v1Operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperation>();
            var v2Operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();

            // Warm up
            await v1Operation.ExecuteAsync(GenerateTestItems(5), virtualKeyId: 1);
            await v2Operation.ExecuteAsync(GenerateTestItems(5), virtualKeyId: 1);

            // Act - V1
            var v1Stopwatch = Stopwatch.StartNew();
            var v1Result = await v1Operation.ExecuteAsync(items, virtualKeyId: 1);
            v1Stopwatch.Stop();

            // Act - V2
            var v2Stopwatch = Stopwatch.StartNew();
            var v2Result = await v2Operation.ExecuteAsync(items, virtualKeyId: 1);
            v2Stopwatch.Stop();

            // Assert - Both should succeed
            Assert.Equal(BatchOperationStatusEnum.Completed, v1Result.Status);
            Assert.Equal(BatchOperationStatusEnum.Completed, v2Result.Status);
            Assert.Equal(itemCount, v1Result.SuccessCount);
            Assert.Equal(itemCount, v2Result.SuccessCount);

            // Performance metrics
            var v1Throughput = itemCount / v1Result.Duration.TotalSeconds;
            var v2Throughput = itemCount / v2Result.Duration.TotalSeconds;
            var throughputDiff = ((v2Throughput - v1Throughput) / v1Throughput) * 100;

            Output.WriteLine($"=== Batch Size: {itemCount} Items ===");
            Output.WriteLine($"V1 Duration: {v1Result.Duration.TotalMilliseconds:F2}ms ({v1Throughput:F1} items/sec)");
            Output.WriteLine($"V2 Duration: {v2Result.Duration.TotalMilliseconds:F2}ms ({v2Throughput:F1} items/sec)");
            Output.WriteLine($"Throughput Difference: {throughputDiff:F1}%");
            Output.WriteLine($"V2 Overhead: {(v2Result.Duration - v1Result.Duration).TotalMilliseconds:F2}ms");
            Output.WriteLine("");

            // V2 should be reasonably close to V1 performance
            // Allow up to 50% overhead due to additional features (idempotency, retry logic)
            var maxAcceptableOverhead = v1Result.Duration.TotalMilliseconds * 0.5;
            var actualOverhead = (v2Result.Duration - v1Result.Duration).TotalMilliseconds;

            Assert.True(actualOverhead < maxAcceptableOverhead,
                $"V2 overhead ({actualOverhead:F2}ms) exceeds acceptable threshold ({maxAcceptableOverhead:F2}ms)");
        }

        [Fact]
        public async Task Benchmark_V2_IdempotencyOverhead()
        {
            // Arrange
            var items = GenerateTestItems(100);
            var v2Operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();

            // Act - Without idempotency token
            var withoutTokenStopwatch = Stopwatch.StartNew();
            var withoutTokenResult = await v2Operation.ExecuteAsync(items, virtualKeyId: 1);
            withoutTokenStopwatch.Stop();

            // Act - With idempotency token
            var withTokenStopwatch = Stopwatch.StartNew();
            var withTokenResult = await v2Operation.ExecuteAsync(items, virtualKeyId: 1, idempotencyToken: "test-token");
            withTokenStopwatch.Stop();

            // Assert
            Assert.Equal(BatchOperationStatusEnum.Completed, withoutTokenResult.Status);
            Assert.Equal(BatchOperationStatusEnum.Completed, withTokenResult.Status);

            var overhead = (withTokenResult.Duration - withoutTokenResult.Duration).TotalMilliseconds;

            Output.WriteLine($"=== Idempotency Overhead ===");
            Output.WriteLine($"Without Token: {withoutTokenResult.Duration.TotalMilliseconds:F2}ms");
            Output.WriteLine($"With Token: {withTokenResult.Duration.TotalMilliseconds:F2}ms");
            Output.WriteLine($"Overhead: {overhead:F2}ms ({(overhead / withoutTokenResult.Duration.TotalMilliseconds * 100):F1}%)");
            Output.WriteLine("");

            // Idempotency overhead should be minimal (< 20% for 100 items)
            var maxAcceptableOverhead = withoutTokenResult.Duration.TotalMilliseconds * 0.2;
            Assert.True(overhead < maxAcceptableOverhead,
                $"Idempotency overhead ({overhead:F2}ms) exceeds threshold ({maxAcceptableOverhead:F2}ms)");
        }

        [Fact]
        public async Task Benchmark_V2_RetryOverhead_WithFailures()
        {
            // Arrange
            var items = GenerateTestItems(50);
            var v2Operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();

            var attemptCounts = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();

            // Simulate 20% transient failure rate
            _mockVirtualKeyService.Setup(s => s.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                .Returns<int, decimal>((id, amount) =>
                {
                    var count = attemptCounts.AddOrUpdate(id, 1, (_, c) => c + 1);

                    // Fail first attempt for 20% of items
                    if (id % 5 == 0 && count == 1)
                    {
                        throw new TimeoutException("Simulated transient error");
                    }

                    return Task.FromResult(true);
                });

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await v2Operation.ExecuteAsync(items, virtualKeyId: 1);
            stopwatch.Stop();

            // Assert
            Assert.Equal(BatchOperationStatusEnum.Completed, result.Status);
            Assert.Equal(50, result.SuccessCount);
            Assert.Equal(0, result.FailedCount); // All should succeed after retry

            var retriedItems = attemptCounts.Count(kvp => kvp.Value > 1);
            var expectedRetries = items.Count / 5; // 20% failure rate

            Output.WriteLine($"=== Retry Performance ===");
            Output.WriteLine($"Total Items: {items.Count}");
            Output.WriteLine($"Retried Items: {retriedItems} (expected ~{expectedRetries})");
            Output.WriteLine($"Total Duration: {result.Duration.TotalMilliseconds:F2}ms");
            Output.WriteLine($"Throughput: {result.ItemsPerSecond:F1} items/sec");
            Output.WriteLine("");

            // Verify retries happened
            Assert.True(retriedItems >= expectedRetries - 2 && retriedItems <= expectedRetries + 2,
                $"Expected ~{expectedRetries} retries, got {retriedItems}");
        }

        [Theory]
        [InlineData(100, 1)]
        [InlineData(100, 5)]
        [InlineData(100, 10)]
        [InlineData(100, 20)]
        public async Task Benchmark_V2_ParallelismImpact(int itemCount, int parallelism)
        {
            // Arrange
            var items = GenerateTestItems(itemCount);

            // Create a custom V2 operation with specific parallelism
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<IBatchOperationService, ConduitLLM.Core.Services.BatchOperationService>();
            services.AddSingleton<ITaskHub>(_ => new Mock<ITaskHub>().Object);
            services.AddSingleton<CoreVirtualKeyService>(_ => _mockVirtualKeyService.Object);
            services.AddSingleton<ISpendNotificationService>(_ => _mockSpendNotificationService.Object);
            services.AddSingleton<IBatchOperationIdempotencyService>(_ => _mockIdempotencyService.Object);
            services.AddScoped<TestBatchOperationWithParallelism>(sp =>
                new TestBatchOperationWithParallelism(
                    sp.GetRequiredService<ILogger<TestBatchOperationWithParallelism>>(),
                    sp.GetRequiredService<IBatchOperationService>(),
                    _mockVirtualKeyService.Object,
                    _mockSpendNotificationService.Object,
                    _mockIdempotencyService.Object,
                    parallelism));

            var provider = services.BuildServiceProvider();
            var operation = provider.GetRequiredService<TestBatchOperationWithParallelism>();

            // Simulate some processing time
            _mockVirtualKeyService.Setup(s => s.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                .Returns(async () =>
                {
                    await Task.Delay(10); // 10ms per item
                    return true;
                });

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await operation.ExecuteAsync(items, virtualKeyId: 1);
            stopwatch.Stop();

            // Assert
            Output.WriteLine($"=== Parallelism: {parallelism} ===");
            Output.WriteLine($"Duration: {result.Duration.TotalMilliseconds:F2}ms");
            Output.WriteLine($"Throughput: {result.ItemsPerSecond:F1} items/sec");
            Output.WriteLine($"Expected Min Duration: {(itemCount * 10.0 / parallelism):F2}ms");
            Output.WriteLine("");

            // Verify parallelism is working (duration should be inversely proportional to parallelism)
            var expectedMinDuration = (itemCount * 10.0 / parallelism) * 0.8; // 80% of theoretical
            Assert.True(result.Duration.TotalMilliseconds >= expectedMinDuration,
                $"Duration {result.Duration.TotalMilliseconds:F2}ms is suspiciously fast for parallelism {parallelism}");

            (provider as IDisposable)?.Dispose();
        }

        [Fact]
        public async Task Benchmark_MemoryUsage_LargeBatch()
        {
            // Arrange
            var items = GenerateTestItems(1000);
            var v2Operation = _serviceProvider.GetRequiredService<BatchSpendUpdateOperationV2>();

            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var beforeMemory = GC.GetTotalMemory(false);

            // Act
            var result = await v2Operation.ExecuteAsync(items, virtualKeyId: 1);

            // Force GC after operation
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var afterMemory = GC.GetTotalMemory(false);
            var memoryUsed = (afterMemory - beforeMemory) / 1024.0 / 1024.0; // Convert to MB

            // Assert
            Output.WriteLine($"=== Memory Usage (1000 items) ===");
            Output.WriteLine($"Memory Before: {beforeMemory / 1024.0 / 1024.0:F2} MB");
            Output.WriteLine($"Memory After: {afterMemory / 1024.0 / 1024.0:F2} MB");
            Output.WriteLine($"Memory Used: {memoryUsed:F2} MB");
            Output.WriteLine($"Per Item: {(memoryUsed * 1024 / items.Count):F2} KB");
            Output.WriteLine("");

            // Memory usage should be reasonable (< 50MB for 1000 items)
            Assert.True(Math.Abs(memoryUsed) < 50,
                $"Memory usage ({memoryUsed:F2} MB) exceeds acceptable threshold (50 MB)");
        }

        private static List<SpendUpdateItem> GenerateTestItems(int count)
        {
            return Enumerable.Range(1, count).Select(i => new SpendUpdateItem
            {
                VirtualKeyId = i,
                Amount = i * 0.5m,
                Model = "gpt-4",
                Provider = "OpenAI",
                RequestMetadata = new Dictionary<string, object>
                {
                    ["requestId"] = $"req-{i}",
                    ["timestamp"] = DateTime.UtcNow
                }
            }).ToList();
        }

        // Test helper class to control parallelism
        private class TestBatchOperationWithParallelism : BatchSpendUpdateOperationV2
        {
            private readonly int _parallelism;

            public TestBatchOperationWithParallelism(
                ILogger<TestBatchOperationWithParallelism> logger,
                IBatchOperationService batchOperationService,
                IVirtualKeyService virtualKeyService,
                ISpendNotificationService spendNotificationService,
                IBatchOperationIdempotencyService? idempotencyService,
                int parallelism)
                : base(logger, batchOperationService, virtualKeyService, spendNotificationService, idempotencyService)
            {
                _parallelism = parallelism;
            }

            protected override BatchOperationOptions ConfigureBatchOptions(int virtualKeyId)
            {
                var options = base.ConfigureBatchOptions(virtualKeyId);
                options.MaxDegreeOfParallelism = _parallelism;
                return options;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                (_serviceProvider as IDisposable)?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
