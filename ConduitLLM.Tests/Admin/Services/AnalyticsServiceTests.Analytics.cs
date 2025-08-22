using ConduitLLM.Configuration.Entities;

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
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { 
                    ModelName = "gpt-4", 
                    Cost = 0.05m,
                    InputTokens = 100,
                    OutputTokens = 50,
                    ResponseTimeMs = 1500,
                    StatusCode = 200,
                    Timestamp = DateTime.UtcNow,
                    VirtualKeyId = 1
                },
                new() { 
                    ModelName = "gpt-3.5-turbo", 
                    Cost = 0.02m,
                    InputTokens = 200,
                    OutputTokens = 100,
                    ResponseTimeMs = 800,
                    StatusCode = 200,
                    Timestamp = DateTime.UtcNow,
                    VirtualKeyId = 2
                },
                new() { 
                    ModelName = "gpt-4", 
                    Cost = 0.00m,
                    InputTokens = 50,
                    OutputTokens = 0,
                    ResponseTimeMs = 500,
                    StatusCode = 429, // Error
                    Timestamp = DateTime.UtcNow,
                    VirtualKeyId = 1
                }
            };
            
            var virtualKeys = new List<VirtualKey>
            {
                new() { Id = 1, KeyName = "Production Key" },
                new() { Id = 2, KeyName = "Development Key" }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);
            
            _mockVirtualKeyRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKeys);

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