using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CostCalculationServiceBasicTests
    {
        [Fact]
        public async Task CalculateCostAsync_WithTextGeneration_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m, // $10 per million tokens
                OutputCostPerMillionTokens = 30.00m  // $30 per million tokens
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: (1000 * 10.00 / 1_000_000) + (500 * 30.00 / 1_000_000) = 0.01 + 0.015 = 0.025
            result.Should().Be(0.025m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithEmbedding_UsesEmbeddingCost()
        {
            // Arrange
            var modelId = "openai/text-embedding-ada-002";
            var usage = new Usage
            {
                PromptTokens = 2000,
                CompletionTokens = 0,
                TotalTokens = 2000
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 10.00m,
                EmbeddingCostPerMillionTokens = 0.10m // $0.10 per million tokens
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 2000 * 0.10 / 1_000_000 = 0.0002
            result.Should().Be(0.0002m);
        }

        // Tests removed: CalculateCostAsync_WithImageGeneration_CalculatesCorrectly,
        // CalculateCostAsync_WithVideoGeneration_CalculatesCorrectly,
        // CalculateCostAsync_WithCombinedUsage_CalculatesAllComponents
        // Image and video cost calculation now handled via RulesBased pricing configuration
    }
}