using ConduitLLM.Configuration.DTOs;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// General analytics tests for AnalyticsServiceTests
    /// </summary>
    public partial class AnalyticsServiceTests
    {
        #region GetAnalyticsSummaryAsync Tests

        [Fact]
        public async Task GetAnalyticsSummaryAsync_CalculatesMetrics()
        {
            // Arrange — mock database-level aggregation methods
            var summary = new RequestLogSummary
            {
                TotalRequests = 3,
                TotalCost = 0.07m,
                TotalInputTokens = 350,
                TotalOutputTokens = 150,
                AverageResponseTimeMs = 933.33,
                SuccessCount = 2,
                ErrorCount = 1
            };

            var modelAggregations = new List<ModelAggregation>
            {
                new() { ModelName = "gpt-4", TotalCost = 0.05m, RequestCount = 2, InputTokens = 150, OutputTokens = 50 },
                new() { ModelName = "gpt-3.5-turbo", TotalCost = 0.02m, RequestCount = 1, InputTokens = 200, OutputTokens = 100 }
            };

            var virtualKeyAggregations = new List<VirtualKeyAggregation>
            {
                new() { VirtualKeyId = 1, TotalCost = 0.05m, RequestCount = 2, LastUsed = DateTime.UtcNow, UniqueModels = 1 },
                new() { VirtualKeyId = 2, TotalCost = 0.02m, RequestCount = 1, LastUsed = DateTime.UtcNow, UniqueModels = 1 }
            };

            var dailyStats = new List<DailyStatisticsAggregation>
            {
                new() { Date = DateTime.UtcNow.Date, RequestCount = 3, Cost = 0.07m, InputTokens = 350, OutputTokens = 150, AverageResponseTime = 933.33, ErrorCount = 1 }
            };

            _mockRequestLogRepository
                .Setup(x => x.GetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(summary);

            _mockRequestLogRepository
                .Setup(x => x.GetAggregatedByModelAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelAggregations);

            _mockRequestLogRepository
                .Setup(x => x.GetAggregatedByVirtualKeyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKeyAggregations);

            _mockRequestLogRepository
                .Setup(x => x.GetDailyStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dailyStats);

            _mockVirtualKeyRepository
                .Setup(x => x.GetKeyNamesByIdsAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, string>
                {
                    { 1, "Production Key" },
                    { 2, "Development Key" }
                });

            // Act
            var result = await _service.GetAnalyticsSummaryAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.TotalRequests);
            Assert.Equal(0.07m, result.TotalCost);
            Assert.Equal(350, result.TotalInputTokens);
            Assert.Equal(150, result.TotalOutputTokens);
            Assert.Equal(2, result.UniqueVirtualKeys);
            Assert.Equal(2, result.UniqueModels);
            Assert.True(result.SuccessRate > 66 && result.SuccessRate < 67); // 2/3 success
            Assert.Equal(2, result.TopModels.Count);
            Assert.Equal(2, result.TopVirtualKeys.Count);
        }

        #endregion
    }
}
