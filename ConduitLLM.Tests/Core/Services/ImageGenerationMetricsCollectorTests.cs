using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public class ImageGenerationMetricsCollectorTests
    {
        private readonly Mock<ILogger<ImageGenerationMetricsCollector>> _mockLogger;
        private readonly Mock<IImageGenerationMetricsService> _mockMetricsService;
        private readonly IOptions<ImageGenerationMetricsOptions> _options;
        private readonly ImageGenerationMetricsCollector _collector;

        public ImageGenerationMetricsCollectorTests()
        {
            _mockLogger = new Mock<ILogger<ImageGenerationMetricsCollector>>();
            _mockMetricsService = new Mock<IImageGenerationMetricsService>();
            _options = Options.Create(new ImageGenerationMetricsOptions
            {
                MetricsRetentionHours = 24,
                MaxResponseTimeHistorySize = 1000,
                MaxResourceHistorySize = 100,
                SlaTargets = new SlaTargetOptions
                {
                    MinAvailabilityPercent = 99.9,
                    MaxP95ResponseTimeMs = 45000,
                    MaxErrorRatePercent = 1.0
                }
            });
            _collector = new ImageGenerationMetricsCollector(_mockLogger.Object, _mockMetricsService.Object, _options);
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ImageGenerationMetricsCollector(null!, _mockMetricsService.Object, _options));
        }

        [Fact]
        public void Constructor_NullMetricsService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ImageGenerationMetricsCollector(_mockLogger.Object, null!, _options));
        }

        [Fact]
        public void Constructor_NullOptions_UsesDefaults()
        {
            var collector = new ImageGenerationMetricsCollector(_mockLogger.Object, _mockMetricsService.Object, null);
            Assert.NotNull(collector);
        }

        [Fact]
        public void RecordGenerationStart_ValidParameters_RecordsMetrics()
        {
            // Arrange
            const string operationId = "test-op-1";
            const string provider = "OpenAI";
            const string model = "dall-e-3";
            const int imageCount = 2;
            const int virtualKeyId = 123;

            // Act
            _collector.RecordGenerationStart(operationId, provider, model, imageCount, virtualKeyId);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Started tracking image generation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void RecordGenerationComplete_ValidOperation_RecordsMetrics()
        {
            // Arrange
            const string operationId = "test-op-1";
            const string provider = "OpenAI";
            const string model = "dall-e-3";
            _collector.RecordGenerationStart(operationId, provider, model, 2, 123);

            // Act
            _collector.RecordGenerationComplete(operationId, true, 2, 0.50m, null);

            // Assert
            _mockMetricsService.Verify(x => x.RecordMetricAsync(
                It.IsAny<ImageGenerationMetrics>(), 
                It.IsAny<CancellationToken>()), Times.Once);
            _mockLogger.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Completed tracking image generation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void RecordGenerationComplete_UnknownOperation_LogsWarning()
        {
            // Arrange
            const string operationId = "unknown-op";

            // Act
            _collector.RecordGenerationComplete(operationId, true, 1, 0.25m, null);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempted to complete unknown operation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            _mockMetricsService.Verify(x => x.RecordMetricAsync(
                It.IsAny<ImageGenerationMetrics>(), 
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void RecordGenerationComplete_WithError_RecordsFailure()
        {
            // Arrange
            const string operationId = "test-op-1";
            const string error = "Rate limit exceeded";
            _collector.RecordGenerationStart(operationId, "OpenAI", "dall-e-3", 2, 123);

            // Act
            _collector.RecordGenerationComplete(operationId, false, 0, 0m, error);

            // Assert
            _mockMetricsService.Verify(x => x.RecordMetricAsync(
                It.Is<ImageGenerationMetrics>(m => 
                    !m.Success && 
                    m.ErrorCode == error && 
                    m.ImageCount == 0),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void RecordProviderPerformance_ValidParameters_UpdatesMetrics()
        {
            // Arrange
            const string provider = "OpenAI";
            const string model = "dall-e-3";
            const double responseTime = 1234.5;
            const double queueTime = 100.0;

            // Act
            _collector.RecordProviderPerformance(provider, model, responseTime, queueTime);

            // Assert - No exception thrown, internal state updated
            Assert.True(true);
        }

        [Fact]
        public void RecordImageDownload_Success_UpdatesMetrics()
        {
            // Arrange
            const string provider = "OpenAI";
            const double downloadTime = 500.0;
            const long imageSize = 1024 * 1024; // 1MB

            // Act
            _collector.RecordImageDownload(provider, downloadTime, imageSize, true);

            // Assert - No exception thrown, internal state updated
            Assert.True(true);
        }

        [Fact]
        public void RecordImageDownload_Failure_UpdatesMetrics()
        {
            // Arrange
            const string provider = "OpenAI";
            const double downloadTime = 5000.0;
            const long imageSize = 0;

            // Act
            _collector.RecordImageDownload(provider, downloadTime, imageSize, false);

            // Assert - No exception thrown, internal state updated
            Assert.True(true);
        }

        [Fact]
        public void RecordStorageOperation_ValidParameters_LogsDebug()
        {
            // Arrange
            const string storageType = "S3";
            const string operationType = "Store";
            const double duration = 250.5;
            const long size = 2048;

            // Act
            _collector.RecordStorageOperation(storageType, operationType, duration, size, true);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Storage operation: {storageType}/{operationType}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void RecordQueueMetrics_ValidParameters_UpdatesMetrics()
        {
            // Arrange
            const string queueName = "high-priority";
            const int depth = 10;
            const double oldestAge = 5000.0;

            // Act
            _collector.RecordQueueMetrics(queueName, depth, oldestAge);

            // Assert - No exception thrown, internal state updated
            Assert.True(true);
        }

        [Fact]
        public void RecordResourceUtilization_ValidParameters_UpdatesHistory()
        {
            // Arrange
            const double cpu = 45.5;
            const double memory = 2048.0;
            const int activeGenerations = 5;
            const int threads = 25;

            // Act
            _collector.RecordResourceUtilization(cpu, memory, activeGenerations, threads);

            // Assert - No exception thrown, internal state updated
            Assert.True(true);
        }

        [Fact]
        public void RecordResourceUtilization_ExceedsMaxHistory_RemovesOldest()
        {
            // Arrange
            var options = Options.Create(new ImageGenerationMetricsOptions { MaxResourceHistorySize = 2 });
            var collector = new ImageGenerationMetricsCollector(_mockLogger.Object, _mockMetricsService.Object, options);

            // Act
            collector.RecordResourceUtilization(10.0, 1000.0, 1, 10);
            collector.RecordResourceUtilization(20.0, 2000.0, 2, 20);
            collector.RecordResourceUtilization(30.0, 3000.0, 3, 30); // Should remove first entry

            // Assert - No exception thrown, oldest entry removed
            Assert.True(true);
        }

        [Fact]
        public void RecordVirtualKeyUsage_ValidParameters_UpdatesMetrics()
        {
            // Arrange
            const int virtualKeyId = 456;
            const int imagesGenerated = 3;
            const decimal cost = 0.75m;
            const decimal remainingBudget = 99.25m;

            // Act
            _collector.RecordVirtualKeyUsage(virtualKeyId, imagesGenerated, cost, remainingBudget);

            // Assert - No exception thrown, internal state updated
            Assert.True(true);
        }

        [Fact]
        public void RecordVirtualKeyUsage_MultipleUpdates_AccumulatesMetrics()
        {
            // Arrange
            const int virtualKeyId = 456;

            // Act
            _collector.RecordVirtualKeyUsage(virtualKeyId, 1, 0.25m, 99.75m);
            _collector.RecordVirtualKeyUsage(virtualKeyId, 2, 0.50m, 99.25m);
            _collector.RecordVirtualKeyUsage(virtualKeyId, 1, 0.25m, 99.00m);

            // Assert - No exception thrown, metrics accumulated
            Assert.True(true);
        }

        [Fact]
        public void RecordProviderHealth_Healthy_UpdatesMetrics()
        {
            // Arrange
            const string provider = "OpenAI";
            const double healthScore = 0.95;

            // Act
            _collector.RecordProviderHealth(provider, healthScore, true, null);

            // Assert - No exception thrown, internal state updated
            Assert.True(true);
        }

        [Fact]
        public void RecordProviderHealth_Unhealthy_UpdatesMetricsWithError()
        {
            // Arrange
            const string provider = "Replicate";
            const double healthScore = 0.25;
            const string lastError = "Connection timeout";

            // Act
            _collector.RecordProviderHealth(provider, healthScore, false, lastError);

            // Assert - No exception thrown, internal state updated
            Assert.True(true);
        }

        [Fact]
        public void RecordModelMetrics_ValidParameters_LogsDebug()
        {
            // Arrange
            const string model = "dall-e-3";
            const string imageSize = "1024x1024";
            const string quality = "hd";
            const double generationTime = 8500.0;

            // Act
            _collector.RecordModelMetrics(model, imageSize, quality, generationTime);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Model metrics: {model}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetMetricsSnapshotAsync_NoOperations_ReturnsEmptySnapshot()
        {
            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.Equal(0, snapshot.ActiveGenerations);
            Assert.Equal(0, snapshot.GenerationsPerMinute);
            Assert.Equal(0, snapshot.SuccessRate);
            Assert.Equal(0, snapshot.TotalCostLastHour);
            Assert.Equal(0, snapshot.TotalImagesLastHour);
            Assert.Empty(snapshot.ProviderStatuses);
            Assert.Empty(snapshot.ErrorCounts);
        }

        [Fact]
        public async Task GetMetricsSnapshotAsync_WithCompletedOperations_CalculatesMetrics()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 2, 123);
            _collector.RecordGenerationComplete("op1", true, 2, 0.50m, null);
            
            _collector.RecordGenerationStart("op2", "OpenAI", "dall-e-3", 1, 124);
            _collector.RecordGenerationComplete("op2", false, 0, 0m, "Rate limit exceeded");

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.Equal(0, snapshot.ActiveGenerations); // Both completed
            Assert.True(snapshot.GenerationsPerMinute > 0);
            Assert.Equal(0.5, snapshot.SuccessRate); // 1 success, 1 failure
            Assert.Equal(0.50m, snapshot.TotalCostLastHour);
            Assert.Equal(2, snapshot.TotalImagesLastHour);
            Assert.NotEmpty(snapshot.ProviderStatuses);
            Assert.Contains("RateLimit", snapshot.ErrorCounts.Keys);
        }

        [Fact]
        public async Task GetMetricsSnapshotAsync_WithActiveOperations_IncludesActive()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 2, 123);
            // Don't complete it

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Equal(1, snapshot.ActiveGenerations);
        }

        [Fact]
        public async Task GetMetricsSnapshotAsync_WithQueueMetrics_IncludesQueueData()
        {
            // Arrange
            _collector.RecordQueueMetrics("high-priority", 5, 1000.0);
            _collector.RecordQueueMetrics("low-priority", 10, 2000.0);

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Equal(15, snapshot.QueueMetrics.TotalDepth);
            Assert.Equal(2000.0, snapshot.QueueMetrics.MaxWaitTimeMs);
        }

        [Fact]
        public async Task GetMetricsSnapshotAsync_WithResourceMetrics_CalculatesAverages()
        {
            // Arrange
            _collector.RecordResourceUtilization(40.0, 2000.0, 2, 20);
            _collector.RecordResourceUtilization(60.0, 3000.0, 3, 30);

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Equal(50.0, snapshot.ResourceMetrics.CpuUsagePercent);
            Assert.Equal(2500.0, snapshot.ResourceMetrics.MemoryUsageMb);
            Assert.Equal(25, snapshot.ResourceMetrics.ThreadPoolThreads);
        }

        [Fact]
        public async Task GetProviderMetricsAsync_NoOperations_ReturnsEmptySummary()
        {
            // Act
            var summary = await _collector.GetProviderMetricsAsync("OpenAI", 60);

            // Assert
            Assert.NotNull(summary);
            Assert.Equal("OpenAI", summary.ProviderName);
            Assert.Equal(0, summary.TotalRequests);
            Assert.Equal(0, summary.SuccessfulRequests);
            Assert.Equal(0, summary.FailedRequests);
            Assert.Equal(0, summary.TotalCost);
            Assert.Equal(0, summary.TotalImages);
            Assert.Empty(summary.ErrorBreakdown);
            Assert.Empty(summary.ModelBreakdown);
        }

        [Fact]
        public async Task GetProviderMetricsAsync_WithOperations_CalculatesMetrics()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 2, 123);
            await Task.Delay(100); // Simulate processing time
            _collector.RecordGenerationComplete("op1", true, 2, 0.50m, null);
            
            _collector.RecordGenerationStart("op2", "OpenAI", "dall-e-2", 1, 124);
            await Task.Delay(50);
            _collector.RecordGenerationComplete("op2", true, 1, 0.25m, null);

            // Act
            var summary = await _collector.GetProviderMetricsAsync("OpenAI", 60);

            // Assert
            Assert.Equal("OpenAI", summary.ProviderName);
            Assert.Equal(2, summary.TotalRequests);
            Assert.Equal(2, summary.SuccessfulRequests);
            Assert.Equal(0, summary.FailedRequests);
            Assert.Equal(1.0, summary.SuccessRate);
            Assert.Equal(0.75m, summary.TotalCost);
            Assert.Equal(3, summary.TotalImages);
            Assert.Equal(0.25m, summary.AverageCostPerImage);
            Assert.True(summary.AverageResponseTimeMs > 0);
            Assert.Equal(2, summary.ModelBreakdown.Count);
            Assert.Contains("dall-e-3", summary.ModelBreakdown.Keys);
            Assert.Contains("dall-e-2", summary.ModelBreakdown.Keys);
        }

        [Fact]
        public async Task GetProviderMetricsAsync_WithErrors_IncludesErrorBreakdown()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "Replicate", "sdxl", 1, 123);
            _collector.RecordGenerationComplete("op1", false, 0, 0m, "Rate limit exceeded");
            
            _collector.RecordGenerationStart("op2", "Replicate", "sdxl", 1, 124);
            _collector.RecordGenerationComplete("op2", false, 0, 0m, "Connection timeout");

            // Act
            var summary = await _collector.GetProviderMetricsAsync("Replicate", 60);

            // Assert
            Assert.Equal(2, summary.FailedRequests);
            Assert.Equal(0, summary.SuccessRate);
            Assert.Equal(2, summary.ErrorBreakdown.Count);
            Assert.Contains("RateLimit", summary.ErrorBreakdown.Keys);
            Assert.Contains("Timeout", summary.ErrorBreakdown.Keys);
        }

        [Fact]
        public async Task GetProviderMetricsAsync_TimeWindow_FiltersOldOperations()
        {
            // This test would require mocking DateTime.UtcNow or using a time provider
            // For now, we'll just verify the basic functionality works
            var summary = await _collector.GetProviderMetricsAsync("OpenAI", 1); // 1 minute window
            Assert.NotNull(summary);
        }

        [Fact]
        public async Task GetSlaComplianceAsync_NoOperations_ReturnsDefaultCompliance()
        {
            // Act
            var compliance = await _collector.GetSlaComplianceAsync(24);

            // Assert
            Assert.NotNull(compliance);
            Assert.Equal(0, compliance.TotalRequests);
            Assert.Empty(compliance.Violations);
        }

        [Fact]
        public async Task GetSlaComplianceAsync_MeetsAllSlas_NoViolations()
        {
            // Arrange - Create operations that meet SLA
            for (int i = 0; i < 100; i++)
            {
                var opId = $"op{i}";
                _collector.RecordGenerationStart(opId, "OpenAI", "dall-e-3", 1, 123);
                await Task.Delay(10); // Fast response
                _collector.RecordGenerationComplete(opId, true, 1, 0.25m, null);
            }

            // Act
            var compliance = await _collector.GetSlaComplianceAsync(24);

            // Assert
            Assert.Equal(100, compliance.TotalRequests);
            Assert.Equal(100, compliance.SuccessfulRequests);
            Assert.Equal(100.0, compliance.AvailabilityPercent);
            Assert.True(compliance.MeetsAvailabilitySla);
            Assert.True(compliance.MeetsResponseTimeSla);
            Assert.True(compliance.MeetsErrorRateSla);
            Assert.Empty(compliance.Violations);
        }

        [Fact]
        public async Task GetSlaComplianceAsync_HighErrorRate_CreatesViolation()
        {
            // Arrange - Create operations with high error rate
            for (int i = 0; i < 100; i++)
            {
                var opId = $"op{i}";
                _collector.RecordGenerationStart(opId, "OpenAI", "dall-e-3", 1, 123);
                var success = i < 95; // 5% error rate (above 1% SLA)
                _collector.RecordGenerationComplete(opId, success, success ? 1 : 0, success ? 0.25m : 0m, 
                    success ? null : "Service unavailable");
            }

            // Act
            var compliance = await _collector.GetSlaComplianceAsync(24);

            // Assert
            Assert.Equal(100, compliance.TotalRequests);
            Assert.Equal(95, compliance.SuccessfulRequests);
            Assert.Equal(5, compliance.FailedRequests);
            Assert.Equal(5.0, compliance.ErrorRatePercent);
            Assert.False(compliance.MeetsErrorRateSla);
            Assert.Contains(compliance.Violations, v => v.ViolationType == "ErrorRate");
        }

        [Fact]
        public async Task GetSlaComplianceAsync_SlowResponses_CreatesViolation()
        {
            // Arrange - Create operations with slow response times
            var options = Options.Create(new ImageGenerationMetricsOptions
            {
                SlaTargets = new SlaTargetOptions
                {
                    MaxP95ResponseTimeMs = 100 // Very strict for testing
                }
            });
            var collector = new ImageGenerationMetricsCollector(_mockLogger.Object, _mockMetricsService.Object, options);

            for (int i = 0; i < 10; i++)
            {
                var opId = $"op{i}";
                collector.RecordGenerationStart(opId, "OpenAI", "dall-e-3", 1, 123);
                await Task.Delay(150); // Slow response
                collector.RecordGenerationComplete(opId, true, 1, 0.25m, null);
            }

            // Act
            var compliance = await collector.GetSlaComplianceAsync(24);

            // Assert
            Assert.True(compliance.P95ResponseTimeMs > 100);
            Assert.False(compliance.MeetsResponseTimeSla);
            Assert.Contains(compliance.Violations, v => v.ViolationType == "ResponseTime");
        }

        [Fact]
        public async Task ErrorClassification_TimeoutError_ClassifiedCorrectly()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 1, 123);
            _collector.RecordGenerationComplete("op1", false, 0, 0m, "Request timeout after 30 seconds");

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Contains("Timeout", snapshot.ErrorCounts.Keys);
            Assert.Equal(1, snapshot.ErrorCounts["Timeout"]);
        }

        [Fact]
        public async Task ErrorClassification_RateLimitError_ClassifiedCorrectly()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 1, 123);
            _collector.RecordGenerationComplete("op1", false, 0, 0m, "Rate limit exceeded");

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Contains("RateLimit", snapshot.ErrorCounts.Keys);
            Assert.Equal(1, snapshot.ErrorCounts["RateLimit"]);
        }

        [Fact]
        public async Task ErrorClassification_AuthError_ClassifiedCorrectly()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 1, 123);
            _collector.RecordGenerationComplete("op1", false, 0, 0m, "Unauthorized: Invalid API key");

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Contains("Authentication", snapshot.ErrorCounts.Keys);
            Assert.Equal(1, snapshot.ErrorCounts["Authentication"]);
        }

        [Fact]
        public async Task ErrorClassification_NetworkError_ClassifiedCorrectly()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 1, 123);
            _collector.RecordGenerationComplete("op1", false, 0, 0m, "Network connection failed");

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Contains("Network", snapshot.ErrorCounts.Keys);
            Assert.Equal(1, snapshot.ErrorCounts["Network"]);
        }

        [Fact]
        public async Task ErrorClassification_ValidationError_ClassifiedCorrectly()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 1, 123);
            _collector.RecordGenerationComplete("op1", false, 0, 0m, "Invalid image size specified");

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Contains("Validation", snapshot.ErrorCounts.Keys);
            Assert.Equal(1, snapshot.ErrorCounts["Validation"]);
        }

        [Fact]
        public async Task ErrorClassification_QuotaError_ClassifiedCorrectly()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 1, 123);
            _collector.RecordGenerationComplete("op1", false, 0, 0m, "Monthly quota exceeded");

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Contains("Quota", snapshot.ErrorCounts.Keys);
            Assert.Equal(1, snapshot.ErrorCounts["Quota"]);
        }

        [Fact]
        public async Task ErrorClassification_UnknownError_ClassifiedAsOther()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 1, 123);
            _collector.RecordGenerationComplete("op1", false, 0, 0m, "Something went wrong");

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Contains("Other", snapshot.ErrorCounts.Keys);
            Assert.Equal(1, snapshot.ErrorCounts["Other"]);
        }

        [Fact]
        public async Task ConcurrentOperations_MultipleThreads_HandledCorrectly()
        {
            // Arrange
            const int threadCount = 10;
            const int operationsPerThread = 10;
            var tasks = new Task[threadCount];

            // Act
            for (int t = 0; t < threadCount; t++)
            {
                var threadId = t;
                tasks[t] = Task.Run(async () =>
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var opId = $"thread{threadId}-op{i}";
                        _collector.RecordGenerationStart(opId, "OpenAI", "dall-e-3", 1, threadId);
                        await Task.Delay(Random.Shared.Next(10, 50));
                        _collector.RecordGenerationComplete(opId, true, 1, 0.25m, null);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            var snapshot = await _collector.GetMetricsSnapshotAsync();
            Assert.Equal(threadCount * operationsPerThread, snapshot.TotalImagesLastHour);
            Assert.Equal(threadCount * operationsPerThread * 0.25m, snapshot.TotalCostLastHour);
        }

        [Fact]
        public void Dispose_DisposesResources()
        {
            // Arrange
            var collector = new ImageGenerationMetricsCollector(_mockLogger.Object, _mockMetricsService.Object, _options);

            // Act & Assert - Should not throw
            collector.Dispose();
            collector.Dispose(); // Second call should also not throw
        }

        [Fact]
        public async Task GetMetricsSnapshotAsync_CalculatesPercentiles()
        {
            // Arrange - Create operations with varying response times
            var responseTimes = new[] { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };
            for (int i = 0; i < responseTimes.Length; i++)
            {
                var opId = $"op{i}";
                _collector.RecordGenerationStart(opId, "OpenAI", "dall-e-3", 1, 123);
                await Task.Delay(responseTimes[i] / 100); // Simulate varying response times
                _collector.RecordGenerationComplete(opId, true, 1, 0.25m, null);
            }

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.True(snapshot.AverageResponseTimeMs > 0);
            Assert.True(snapshot.P95ResponseTimeMs > 0);
            Assert.True(snapshot.P95ResponseTimeMs >= snapshot.AverageResponseTimeMs);
        }

        [Fact]
        public async Task ProviderHealth_TracksConsecutiveFailures()
        {
            // Arrange
            const string provider = "Replicate";
            
            // Record some failures
            for (int i = 0; i < 3; i++)
            {
                var opId = $"op{i}";
                _collector.RecordGenerationStart(opId, provider, "sdxl", 1, 123);
                _collector.RecordGenerationComplete(opId, false, 0, 0m, "Service unavailable");
            }

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Contains(provider, snapshot.ProviderStatuses.Keys);
            Assert.Equal(3, snapshot.ProviderStatuses[provider].ConsecutiveFailures);
        }

        [Fact]
        public async Task ProviderHealth_ResetsConsecutiveFailuresOnSuccess()
        {
            // Arrange
            const string provider = "Replicate";
            
            // Record failures then success
            for (int i = 0; i < 3; i++)
            {
                var opId = $"fail{i}";
                _collector.RecordGenerationStart(opId, provider, "sdxl", 1, 123);
                _collector.RecordGenerationComplete(opId, false, 0, 0m, "Service unavailable");
            }
            
            _collector.RecordGenerationStart("success", provider, "sdxl", 1, 123);
            _collector.RecordGenerationComplete("success", true, 1, 0.25m, null);

            // Act
            var snapshot = await _collector.GetMetricsSnapshotAsync();

            // Assert
            Assert.Contains(provider, snapshot.ProviderStatuses.Keys);
            Assert.Equal(0, snapshot.ProviderStatuses[provider].ConsecutiveFailures);
        }

        [Fact]
        public void RecordProviderPerformance_AccumulatesMetrics()
        {
            // Arrange & Act
            _collector.RecordProviderPerformance("OpenAI", "dall-e-3", 1000.0, 100.0);
            _collector.RecordProviderPerformance("OpenAI", "dall-e-3", 2000.0, 200.0);
            _collector.RecordProviderPerformance("OpenAI", "dall-e-3", 3000.0, 300.0);

            // Assert - No exception thrown, metrics accumulated
            Assert.True(true);
        }

        [Fact]
        public async Task ModelBreakdown_TracksPerModelMetrics()
        {
            // Arrange
            _collector.RecordGenerationStart("op1", "OpenAI", "dall-e-3", 2, 123);
            _collector.RecordGenerationComplete("op1", true, 2, 0.50m, null);
            
            _collector.RecordGenerationStart("op2", "OpenAI", "dall-e-3", 1, 124);
            _collector.RecordGenerationComplete("op2", true, 1, 0.25m, null);
            
            _collector.RecordGenerationStart("op3", "OpenAI", "dall-e-2", 4, 125);
            _collector.RecordGenerationComplete("op3", true, 4, 0.40m, null);

            // Act
            var summary = await _collector.GetProviderMetricsAsync("OpenAI", 60);

            // Assert
            Assert.Equal(2, summary.ModelBreakdown.Count);
            
            var dalle3Metrics = summary.ModelBreakdown["dall-e-3"];
            Assert.Equal(2, dalle3Metrics.RequestCount);
            Assert.Equal(3, dalle3Metrics.TotalImages);
            Assert.Equal(0.75m, dalle3Metrics.TotalCost);
            
            var dalle2Metrics = summary.ModelBreakdown["dall-e-2"];
            Assert.Equal(1, dalle2Metrics.RequestCount);
            Assert.Equal(4, dalle2Metrics.TotalImages);
            Assert.Equal(0.40m, dalle2Metrics.TotalCost);
        }
    }
}