using ConduitLLM.Configuration.Entities;

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
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { 
                    VirtualKeyId = 1, 
                    ModelName = "gpt-4", 
                    Cost = 0.05m,
                    InputTokens = 100,
                    OutputTokens = 50,
                    ResponseTimeMs = 1500,
                    Timestamp = DateTime.UtcNow
                },
                new() { 
                    VirtualKeyId = 2, // Different key
                    ModelName = "gpt-3.5-turbo", 
                    Cost = 0.02m,
                    InputTokens = 200,
                    OutputTokens = 100,
                    ResponseTimeMs = 800,
                    Timestamp = DateTime.UtcNow
                },
                new() { 
                    VirtualKeyId = 1, 
                    ModelName = "gpt-4", 
                    Cost = 0.03m,
                    InputTokens = 150,
                    OutputTokens = 75,
                    ResponseTimeMs = 1200,
                    Timestamp = DateTime.UtcNow
                }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);

            // Act
            var result = await _service.GetVirtualKeyUsageAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalRequests);
            Assert.Equal(0.08m, result.TotalCost);
            Assert.Equal(250, result.TotalInputTokens);
            Assert.Equal(125, result.TotalOutputTokens);
            Assert.Equal(1350, result.AverageResponseTimeMs); // (1500 + 1200) / 2
            Assert.Single(result.ModelUsage);
            Assert.Equal("gpt-4", result.ModelUsage.Keys.First());
        }

        #endregion
    }
}