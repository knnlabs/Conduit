using ConduitLLM.Configuration.Entities;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Model operation tests for AnalyticsServiceTests
    /// </summary>
    public partial class AnalyticsServiceTests
    {
        #region GetDistinctModelsAsync Tests

        [Fact]
        public async Task GetDistinctModelsAsync_ReturnsUniqueModels()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { ModelName = "gpt-4" },
                new() { ModelName = "gpt-3.5-turbo" },
                new() { ModelName = "gpt-4" }, // Duplicate
                new() { ModelName = "claude-3" }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);

            // Act
            var result = await _service.GetDistinctModelsAsync();

            // Assert
            var models = result.ToList();
            Assert.Equal(3, models.Count);
            Assert.Contains("gpt-4", models);
            Assert.Contains("gpt-3.5-turbo", models);
            Assert.Contains("claude-3", models);
        }

        [Fact]
        public async Task GetDistinctModelsAsync_UsesCaching()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { ModelName = "gpt-4" }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);

            // Act - Call twice
            var result1 = await _service.GetDistinctModelsAsync();
            var result2 = await _service.GetDistinctModelsAsync();

            // Assert - Repository should only be called once due to caching
            _mockRequestLogRepository.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(result1, result2);
        }

        #endregion
    }
}