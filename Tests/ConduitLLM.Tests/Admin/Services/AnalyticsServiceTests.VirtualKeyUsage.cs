using ConduitLLM.Configuration.DTOs;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Virtual key usage tests for AnalyticsServiceTests
    /// </summary>
    public partial class AnalyticsServiceTests
    {
        #region GetVirtualKeyUsageAsync Tests

        [Fact]
        public async Task GetVirtualKeyUsageAsync_FiltersById()
        {
            // Arrange — mock database-level aggregation for virtual key 1
            var summary = new RequestLogSummary
            {
                TotalRequests = 2,
                TotalCost = 0.08m,
                TotalInputTokens = 250,
                TotalOutputTokens = 125,
                AverageResponseTimeMs = 1350,
                SuccessCount = 2,
                ErrorCount = 0
            };

            var modelAggregations = new List<ModelAggregation>
            {
                new() { ModelName = "gpt-4", TotalCost = 0.08m, RequestCount = 2, InputTokens = 250, OutputTokens = 125 }
            };

            _mockRequestLogRepository
                .Setup(x => x.GetSummaryForVirtualKeyAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(summary);

            _mockRequestLogRepository
                .Setup(x => x.GetAggregatedByModelForVirtualKeyAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelAggregations);

            // Act
            var result = await _service.GetVirtualKeyUsageAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalRequests);
            Assert.Equal(0.08m, result.TotalCost);
            Assert.Equal(250, result.TotalInputTokens);
            Assert.Equal(125, result.TotalOutputTokens);
            Assert.Equal(1350, result.AverageResponseTimeMs);
            Assert.Single(result.ModelUsage);
            Assert.Equal("gpt-4", result.ModelUsage.Keys.First());
        }

        #endregion
    }
}
