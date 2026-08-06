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
            // Arrange - Repository now returns pre-filtered distinct models
            var distinctModels = new List<string> { "claude-3", "gpt-3.5-turbo", "gpt-4" };

            _mockRequestLogRepository
                .Setup(x => x.GetDistinctModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(distinctModels);

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
            // Arrange - Repository now returns pre-filtered distinct models
            var distinctModels = new List<string> { "gpt-4" };

            _mockRequestLogRepository
                .Setup(x => x.GetDistinctModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(distinctModels);

            // Act - Call twice
            var result1 = await _service.GetDistinctModelsAsync();
            var result2 = await _service.GetDistinctModelsAsync();

            // Assert - Repository should only be called once due to caching
            _mockRequestLogRepository.Verify(x => x.GetDistinctModelsAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(result1, result2);
        }

        [Fact]
        public async Task InvalidateCache_ExpiresCachedAnalyticsResults()
        {
            // Arrange
            _mockRequestLogRepository
                .SetupSequence(x => x.GetDistinctModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "gpt-4" })
                .ReturnsAsync(new List<string> { "claude-3" });

            var initialResult = await _service.GetDistinctModelsAsync();

            // Act
            var keysInvalidated = _service.InvalidateCache();
            var refreshedResult = await _service.GetDistinctModelsAsync();

            // Assert
            Assert.Equal(["gpt-4"], initialResult);
            Assert.Equal(1, keysInvalidated);
            Assert.Equal(["claude-3"], refreshedResult);
            _mockRequestLogRepository.Verify(
                x => x.GetDistinctModelsAsync(It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        #endregion
    }
}
