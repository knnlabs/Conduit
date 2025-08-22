using ConduitLLM.Configuration.DTOs.Audio;

using FluentAssertions;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminAudioUsageServiceTests
    {
        #region GetUsageSummaryAsync Tests

        [Fact]
        public async Task GetUsageSummaryAsync_WithValidParameters_ShouldReturnSummary()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-30);
            var endDate = DateTime.UtcNow;
            var expectedSummary = new AudioUsageSummaryDto
            {
                TotalOperations = 100,
                TotalCost = 50.5m,
                TotalDurationSeconds = 3600,
                SuccessfulOperations = 95,
                FailedOperations = 5,
                TotalCharacters = 10000,
                TotalInputTokens = 5000,
                TotalOutputTokens = 4000
            };

            _mockRepository.Setup(x => x.GetUsageSummaryAsync(startDate, endDate, It.IsAny<string?>(), It.IsAny<int?>()))
                .ReturnsAsync(expectedSummary);

            // Act
            var result = await _service.GetUsageSummaryAsync(startDate, endDate);

            // Assert
            result.Should().BeEquivalentTo(expectedSummary);
        }

        [Fact]
        public async Task GetUsageSummaryAsync_WithVirtualKeyFilter_ShouldReturnFilteredSummary()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var virtualKey = "test-key-hash";
            
            var expectedSummary = new AudioUsageSummaryDto
            {
                TotalOperations = 20,
                TotalCost = 10.5m,
                SuccessfulOperations = 20,
                FailedOperations = 0
            };

            _mockRepository.Setup(x => x.GetUsageSummaryAsync(startDate, endDate, virtualKey, It.IsAny<int?>()))
                .ReturnsAsync(expectedSummary);

            // Act
            var result = await _service.GetUsageSummaryAsync(startDate, endDate, virtualKey);

            // Assert
            result.TotalOperations.Should().Be(20);
            result.TotalCost.Should().Be(10.5m);
        }

        #endregion
    }
}