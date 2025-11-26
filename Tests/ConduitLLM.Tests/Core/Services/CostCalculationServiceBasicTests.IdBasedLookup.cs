using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

using FluentAssertions;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for ID-based cost calculation (CalculateCostByIdAsync)
    /// </summary>
    public partial class CostCalculationServiceBasicTests
    {
        [Fact]
        public async Task CalculateCostByIdAsync_WithValidModelCostId_CalculatesCorrectly()
        {
            // Arrange
            var modelCostId = 42;
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500
            };
            var modelCost = new ModelCost
            {
                Id = modelCostId,
                CostName = "test-model-cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostByIdAsync(modelCostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostByIdAsync(modelCostId, usage);

            // Assert
            // Expected: (1000 * 10.00 / 1_000_000) + (500 * 30.00 / 1_000_000) = 0.01 + 0.015 = 0.025
            result.Should().Be(0.025m);
        }

        [Fact]
        public async Task CalculateCostByIdAsync_WithNullUsage_ReturnsZero()
        {
            // Arrange
            var modelCostId = 42;

            // Act
            var result = await _service.CalculateCostByIdAsync(modelCostId, null!);

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public async Task CalculateCostByIdAsync_WithNonExistentModelCostId_ReturnsZero()
        {
            // Arrange
            var modelCostId = 999;
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostByIdAsync(modelCostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var result = await _service.CalculateCostByIdAsync(modelCostId, usage);

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public async Task CalculateCostByIdAsync_WithPerImagePricing_CalculatesCorrectly()
        {
            // Arrange
            var modelCostId = 76;
            var usage = new Usage
            {
                ImageCount = 2,
                ImageQuality = "standard",
                ImageResolution = "1024x1024"
            };
            // PerImage pricing uses JSON configuration with BaseRate (PascalCase for System.Text.Json)
            var pricingConfig = """{"BaseRate": 0.04}""";
            var modelCost = new ModelCost
            {
                Id = modelCostId,
                CostName = "flux-1.1-pro",
                PricingModel = PricingModel.PerImage,
                PricingConfiguration = pricingConfig,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostByIdAsync(modelCostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostByIdAsync(modelCostId, usage);

            // Assert
            // Expected: 2 images * $0.04 = $0.08
            result.Should().Be(0.08m);
        }

        [Fact]
        public async Task CalculateCostByIdAsync_WithBatchProcessing_AppliesDiscount()
        {
            // Arrange
            var modelCostId = 42;
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                IsBatch = true
            };
            var modelCost = new ModelCost
            {
                Id = modelCostId,
                CostName = "test-model-cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = 0.5m, // 50% discount
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostByIdAsync(modelCostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostByIdAsync(modelCostId, usage);

            // Assert
            // Expected: ((1000 * 10.00 / 1_000_000) + (500 * 30.00 / 1_000_000)) * 0.5 = 0.025 * 0.5 = 0.0125
            result.Should().Be(0.0125m);
        }

        [Fact]
        public async Task CalculateCostByIdAsync_VerifiesGetCostByIdAsyncIsCalled()
        {
            // Arrange
            var modelCostId = 42;
            var usage = new Usage { PromptTokens = 100 };

            _modelCostServiceMock
                .Setup(x => x.GetCostByIdAsync(modelCostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            await _service.CalculateCostByIdAsync(modelCostId, usage);

            // Assert
            _modelCostServiceMock.Verify(
                x => x.GetCostByIdAsync(modelCostId, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
