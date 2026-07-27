using ConduitLLM.Core.Models;
using ConduitLLM.Tests.TestHelpers;
using AwesomeAssertions;
using Moq;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CostCalculationServiceBasicTests
    {
        [Fact]
        public async Task CalculateCostAsync_WithNullModelId_ReturnsZero()
        {
            // Arrange
            var usage = new Usage { PromptTokens = 100, CompletionTokens = 50, TotalTokens = 150 };

            // Act
            var result = await _service.CalculateCostAsync(null, usage);

            // Assert
            result.Should().Be(0m);
            _modelCostServiceMock.Verify(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CalculateCostAsync_WithEmptyModelId_ReturnsZero()
        {
            // Arrange
            var usage = new Usage { PromptTokens = 100, CompletionTokens = 50, TotalTokens = 150 };

            // Act
            var result = await _service.CalculateCostAsync(string.Empty, usage);

            // Assert
            result.Should().Be(0m);
            _modelCostServiceMock.Verify(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNullUsage_ReturnsZero()
        {
            // Arrange
            var modelId = "openai/gpt-4o";

            // Act
            var result = await _service.CalculateCostAsync(modelId, null);

            // Assert
            result.Should().Be(0m);
            _modelCostServiceMock.Verify(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CalculateCostAsync_WithModelCostNotFound_ThrowsForReconciliation()
        {
            // Arrange
            var modelId = "unknown/model";
            var usage = new Usage { PromptTokens = 100, CompletionTokens = 50, TotalTokens = 150 };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var act = () => _service.CalculateCostAsync(modelId, usage);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*No active model cost configuration*unknown/model*");
        }

        [Fact]
        public async Task CalculateCostAsync_WithZeroCosts_ReturnsZero()
        {
            // Arrange
            var modelId = "free/model";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            result.Should().Be(0m);
        }
    }
}
