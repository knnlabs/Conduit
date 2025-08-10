using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using FluentAssertions;
using Moq;
using Xunit;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CostCalculationServiceSearchAndInferenceTests
    {
        #region Video Refund Tests

        [Fact]
        public async Task CalculateCostAsync_WithNegativeVideoDuration_HandlesAsRefund()
        {
            // Arrange
            var modelId = "minimax/video-01";
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.05m // $0.05 per second
            };
            
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = -10, // Negative 10 seconds (refund)
                VideoResolution = "1280x720"
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // -10 * 0.05 = -0.5
            result.Should().Be(-0.5m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNegativeVideoDurationAndMultiplier_AppliesMultiplierToRefund()
        {
            // Arrange
            var modelId = "minimax/video-01";
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.05m,
                VideoResolutionMultipliers = JsonSerializer.Serialize(new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m,
                    ["1280x720"] = 1.0m
                })
            };
            
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = -20, // Negative 20 seconds
                VideoResolution = "1920x1080" // Higher resolution
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // -20 * 0.05 * 1.5 = -1.0 * 1.5 = -1.5
            result.Should().Be(-1.5m);
        }

        [Theory]
        [InlineData(-5, "1280x720", 0.1, 1.0, -0.5)]    // Basic negative with standard res
        [InlineData(-10, "1920x1080", 0.1, 2.0, -2.0)]  // Negative with 2x multiplier
        [InlineData(-30, "4K", 0.02, 1.0, -0.6)]        // Unknown resolution, no multiplier
        [InlineData(-60, null, 0.05, 1.0, -3.0)]        // Null resolution
        public async Task CalculateCostAsync_WithVariousNegativeVideoDurations_CalculatesCorrectly(
            double videoDuration, string? resolution, decimal costPerSecond, decimal multiplier, decimal expectedTotal)
        {
            // Arrange
            var modelId = "video/model";
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = costPerSecond,
                VideoResolutionMultipliers = resolution != null && multiplier != 1.0m ? 
                    JsonSerializer.Serialize(new Dictionary<string, decimal> { [resolution] = multiplier }) : null
            };
            
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = videoDuration,
                VideoResolution = resolution
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            result.Should().Be(expectedTotal);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNegativeVideoDurationAndTokens_CombinesAllCosts()
        {
            // Arrange
            var modelId = "video/model-with-chat";
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                VideoCostPerSecond = 0.1m
            };
            
            var usage = new Usage
            {
                PromptTokens = 1000,       // Positive tokens
                CompletionTokens = 500,    // Positive tokens
                TotalTokens = 1500,
                VideoDurationSeconds = -15 // Negative video duration
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // Token cost: (1000 * 0.00001) + (500 * 0.00002) = 0.01 + 0.01 = 0.02
            // Video cost: -15 * 0.1 = -1.5
            // Total: 0.02 - 1.5 = -1.48
            result.Should().Be(-1.48m);
        }

        #endregion
    }
}