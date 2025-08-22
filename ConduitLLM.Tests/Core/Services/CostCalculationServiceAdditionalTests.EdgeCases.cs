using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CostCalculationServiceAdditionalTests
    {
        #region Edge Cases for CalculateCostAsync

        [Fact]
        public async Task CalculateCostAsync_WithVideoButNoVideoCost_IgnoresVideoUsage()
        {
            // Arrange
            var modelId = "text-only-model";
            var usage = new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150,
                VideoDurationSeconds = 10.0, // Has video duration but model doesn't support video
                VideoResolution = "1920x1080"
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                VideoCostPerSecond = null // No video cost defined
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Should only calculate text costs: (100 * 10.00 / 1_000_000) + (50 * 20.00 / 1_000_000) = 0.001 + 0.001 = 0.002
            result.Should().Be(0.002m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithImageButNoImageCost_IgnoresImageUsage()
        {
            // Arrange
            var modelId = "text-only-model";
            var usage = new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150,
                ImageCount = 5 // Has images but model doesn't support image generation
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ImageCostPerImage = null // No image cost defined
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Should only calculate text costs: (100 * 10.00 / 1_000_000) + (50 * 20.00 / 1_000_000) = 0.001 + 0.001 = 0.002
            result.Should().Be(0.002m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithEmptyVideoResolution_UsesBaseCost()
        {
            // Arrange
            var modelId = "video/model";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = 10,
                VideoResolution = "" // Empty string resolution
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = JsonSerializer.Serialize(new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m
                    // No multiplier for empty string
                })
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 10 * 0.1 = 1.0 (no multiplier applied for empty resolution)
            result.Should().Be(1.0m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithZeroVideoDuration_ReturnsZeroVideoCost()
        {
            // Arrange
            var modelId = "video/model";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = 0, // Zero duration
                VideoResolution = "1920x1080"
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = JsonSerializer.Serialize(new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m
                })
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 0 * 0.1 * 1.5 = 0
            result.Should().Be(0m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithEmbeddingCostButNoEmbeddingCostDefined_UsesInputCost()
        {
            // Arrange
            var modelId = "model-without-embedding-cost";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 0, // No completions (typical for embeddings)
                TotalTokens = 1000
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                EmbeddingCostPerMillionTokens = null // No embedding cost defined
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Should use input cost: 1000 * 0.00001 = 0.01
            result.Should().Be(0.01m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithFractionalVideoSeconds_CalculatesPrecisely()
        {
            // Arrange
            var modelId = "video/model";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = 3.14159265359, // Pi seconds
                VideoResolution = "1280x720"
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.01m,
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
            // Expected: 3.14159265359 * 0.01 * 1.0 = 0.0314159265359
            result.Should().Be(0.0314159265359m);
        }

        #endregion
    }
}