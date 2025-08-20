using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using Moq;
using Xunit;

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
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { 
                    ModelName = "gpt-4", 
                    Cost = 0.05m, 
                    Timestamp = DateTime.UtcNow.AddHours(-12), // Within last 24 hours
                    InputTokens = 100,
                    OutputTokens = 50
                },
                new() { 
                    ModelName = "gpt-3.5-turbo", 
                    Cost = 0.02m, 
                    Timestamp = DateTime.UtcNow.AddDays(-2),
                    InputTokens = 200,
                    OutputTokens = 100
                }
            };
            
            var virtualKeys = new List<VirtualKey>
            {
                new() { Id = 1, KeyName = "Test Key 1" }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);
            
            _mockVirtualKeyRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKeys);

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
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { ModelName = "gpt-4", Cost = 0.05m, Timestamp = DateTime.UtcNow },
                new() { ModelName = "gpt-4", Cost = 0.03m, Timestamp = DateTime.UtcNow },
                new() { ModelName = "claude-3", Cost = 0.02m, Timestamp = DateTime.UtcNow }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);
            
            _mockVirtualKeyRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VirtualKey>());

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