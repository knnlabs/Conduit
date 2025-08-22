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

        [Fact]
        public async Task CalculateCostAsync_WithImageGeneration_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 3
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                ImageCostPerImage = 0.04m // $0.04 per image
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 3 * 0.04 = 0.12
            result.Should().Be(0.12m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithVideoGeneration_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "runway/gen-2";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = 4.0,
                VideoResolution = "1920x1080"
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.5m, // $0.50 per second
                VideoResolutionMultipliers = JsonSerializer.Serialize(new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.2m
                })
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 4.0 * 0.5 * 1.2 = 2.4
            result.Should().Be(2.4m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithCombinedUsage_CalculatesAllComponents()
        {
            // Arrange
            var modelId = "multimodal/gpt-4-vision";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                ImageCount = 2,
                VideoDurationSeconds = 2.0,
                VideoResolution = "1280x720"
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m,
                ImageCostPerImage = 0.01275m,
                VideoCostPerSecond = 0.2m,
                VideoResolutionMultipliers = JsonSerializer.Serialize(new Dictionary<string, decimal>
                {
                    ["1280x720"] = 1.0m
                })
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 
            // Text: (1000 * 10 / 1M) + (500 * 30 / 1M) = 0.01 + 0.015 = 0.025
            // Images: 2 * 0.01275 = 0.0255
            // Video: 2.0 * 0.2 * 1.0 = 0.4
            // Total: 0.025 + 0.0255 + 0.4 = 0.4505
            result.Should().Be(0.4505m);
        }
    }
}