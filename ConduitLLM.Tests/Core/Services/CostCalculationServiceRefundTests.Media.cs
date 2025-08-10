using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Media-related refund calculation tests (images, video)
    /// </summary>
    public partial class CostCalculationServiceRefundTests
    {
        [Fact]
        public async Task CalculateRefundAsync_WithImageRefund_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var originalUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0, ImageCount = 5 };
            var refundUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0, ImageCount = 2 };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                ImageCostPerImage = 0.04m // $0.04 per image
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Image generation failure");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.08m); // 2 * 0.04
            result.Breakdown!.ImageRefund.Should().Be(0.08m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithVideoRefund_IncludesResolutionMultiplier()
        {
            // Arrange
            var modelId = "some-video-model";
            var originalUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = 10.0,
                VideoResolution = "1920x1080"
            };
            var refundUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = 5.0,
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

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Video processing error");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.75m); // 5.0 * 0.1 * 1.5
            result.Breakdown!.VideoRefund.Should().Be(0.75m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithMixedUsageRefund_CalculatesAllComponents()
        {
            // Arrange
            var modelId = "multimodal-model";
            var originalUsage = new Usage 
            { 
                PromptTokens = 1000, 
                CompletionTokens = 500, 
                TotalTokens = 1500,
                ImageCount = 3,
                VideoDurationSeconds = 10.0,
                VideoResolution = "1280x720"
            };
            var refundUsage = new Usage 
            { 
                PromptTokens = 500, 
                CompletionTokens = 250, 
                TotalTokens = 750,
                ImageCount = 1,
                VideoDurationSeconds = 5.0,
                VideoResolution = "1280x720"
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m,
                ImageCostPerImage = 0.04m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = JsonSerializer.Serialize(new Dictionary<string, decimal>
                {
                    ["1280x720"] = 1.0m
                })
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial service failure");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.5525m); // 0.005 + 0.0075 + 0.04 + 0.5
            result.Breakdown!.InputTokenRefund.Should().Be(0.005m);
            result.Breakdown.OutputTokenRefund.Should().Be(0.0075m);
            result.Breakdown.ImageRefund.Should().Be(0.04m);
            result.Breakdown.VideoRefund.Should().Be(0.5m);
        }
    }
}