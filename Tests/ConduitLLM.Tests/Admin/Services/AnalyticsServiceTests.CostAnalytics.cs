using ConduitLLM.Configuration.DTOs;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Cost analytics tests for AnalyticsServiceTests
    /// </summary>
    public partial class AnalyticsServiceTests
    {
        #region GetCostSummaryAsync Tests

        [Fact]
        public async Task GetCostSummaryAsync_CalculatesTotals()
        {
            // Arrange — mock database-level aggregation methods
            var modelAggregations = new List<ModelAggregation>
            {
                new() { ModelName = "gpt-4", TotalCost = 0.05m, RequestCount = 1, InputTokens = 100, OutputTokens = 50 },
                new() { ModelName = "gpt-3.5-turbo", TotalCost = 0.02m, RequestCount = 1, InputTokens = 200, OutputTokens = 100 }
            };

            var dailyCosts = new List<DateCostAggregation>
            {
                new() { Date = DateTime.UtcNow.Date, TotalCost = 0.05m, RequestCount = 1 },
                new() { Date = DateTime.UtcNow.AddDays(-2).Date, TotalCost = 0.02m, RequestCount = 1 }
            };

            var last24hSummary = new RequestLogSummary { TotalRequests = 1, TotalCost = 0.05m };

            _mockRequestLogRepository
                .Setup(x => x.GetAggregatedByModelAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelAggregations);

            _mockRequestLogRepository
                .Setup(x => x.GetAggregatedByVirtualKeyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VirtualKeyAggregation>());

            _mockRequestLogRepository
                .Setup(x => x.GetCostsByDateAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dailyCosts);

            _mockRequestLogRepository
                .Setup(x => x.GetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(last24hSummary);

            // Act
            var result = await _service.GetCostSummaryAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0.07m, result.TotalCost);
            Assert.True(result.Last24HoursCost > 0);
            Assert.NotEmpty(result.TopModelsBySpend);
        }

        [Fact]
        public async Task GetCostSummaryAsync_GroupsByModel()
        {
            // Arrange — pre-aggregated model data (as the DB would return)
            var modelAggregations = new List<ModelAggregation>
            {
                new() { ModelName = "gpt-4", TotalCost = 0.08m, RequestCount = 2, InputTokens = 300, OutputTokens = 100 },
                new() { ModelName = "claude-3", TotalCost = 0.02m, RequestCount = 1, InputTokens = 100, OutputTokens = 50 }
            };

            var dailyCosts = new List<DateCostAggregation>
            {
                new() { Date = DateTime.UtcNow.Date, TotalCost = 0.10m, RequestCount = 3 }
            };

            _mockRequestLogRepository
                .Setup(x => x.GetAggregatedByModelAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelAggregations);

            _mockRequestLogRepository
                .Setup(x => x.GetAggregatedByVirtualKeyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VirtualKeyAggregation>());

            _mockRequestLogRepository
                .Setup(x => x.GetCostsByDateAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dailyCosts);

            _mockRequestLogRepository
                .Setup(x => x.GetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RequestLogSummary { TotalRequests = 3, TotalCost = 0.10m });

            // Act
            var result = await _service.GetCostSummaryAsync();

            // Assert
            var gpt4Cost = result.TopModelsBySpend.FirstOrDefault(m => m.Name == "gpt-4");
            Assert.NotNull(gpt4Cost);
            Assert.Equal(0.08m, gpt4Cost.Cost);
            Assert.Equal(2, gpt4Cost.RequestCount);
        }

        #endregion
    }
}
