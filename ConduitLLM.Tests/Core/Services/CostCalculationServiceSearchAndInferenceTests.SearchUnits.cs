using ConduitLLM.Core.Models;
using FluentAssertions;
using Moq;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CostCalculationServiceSearchAndInferenceTests
    {
        #region Search Units Tests

        [Fact]
        public async Task CalculateCostAsync_WithSearchUnits_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "cohere/rerank-3.5";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 5
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                CostPerSearchUnit = 2.0m // $2.00 per 1K search units
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 5 * (2.0 / 1000) = 5 * 0.002 = 0.01
            result.Should().Be(0.01m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithSearchUnitsAndTokens_CalculatesBothCorrectly()
        {
            // Arrange
            var modelId = "hybrid/model";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                SearchUnits = 10
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m,
                CostPerSearchUnit = 1.5m // $1.50 per 1K search units
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Token cost: (1000 * 10.00 / 1_000_000) + (500 * 30.00 / 1_000_000) = 0.01 + 0.015 = 0.025
            // Search unit cost: 10 * (1.5 / 1000) = 10 * 0.0015 = 0.015
            // Total: 0.025 + 0.015 = 0.04
            result.Should().Be(0.04m);
        }

        #endregion
    }
}