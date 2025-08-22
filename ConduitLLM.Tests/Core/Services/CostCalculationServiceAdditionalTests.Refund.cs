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
        #region Refund Method Additional Tests

        [Fact]
        public async Task CalculateRefundAsync_WithZeroRefundUsage_ReturnsZeroRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0 };
            var refundReason = "No actual usage to refund";

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, refundReason);

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0m);
            result.Breakdown.Should().NotBeNull();
            result.Breakdown!.InputTokenRefund.Should().Be(0m);
            result.Breakdown.OutputTokenRefund.Should().Be(0m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithNullOriginalTransactionId_HandlesGracefully()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            var refundReason = "Service interruption";

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, refundReason, null);

            // Assert
            result.Should().NotBeNull();
            result.OriginalTransactionId.Should().BeNull();
            result.RefundAmount.Should().Be(0.0125m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithVideoRefundNoMultipliers_UsesBaseCost()
        {
            // Arrange
            var modelId = "video-model";
            var originalUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = 20.0,
                VideoResolution = "1920x1080"
            };
            var refundUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = 10.0,
                VideoResolution = "1920x1080"
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.2m,
                VideoResolutionMultipliers = null // No multipliers
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Video processing error");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(2.0m); // 10.0 * 0.2
            result.Breakdown!.VideoRefund.Should().Be(2.0m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithUnknownVideoResolution_UsesBaseCost()
        {
            // Arrange
            var modelId = "video-model";
            var originalUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = 10.0,
                VideoResolution = "4K-UHD"
            };
            var refundUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = 5.0,
                VideoResolution = "4K-UHD" // Unknown resolution
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = JsonSerializer.Serialize(new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m,
                    ["1280x720"] = 1.0m
                    // 4K-UHD not in dictionary
                })
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Video quality issue");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.5m); // 5.0 * 0.1 (no multiplier)
            result.Breakdown!.VideoRefund.Should().Be(0.5m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithNegativeImageCount_ReturnsValidationError()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var originalUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0, ImageCount = 5 };
            var refundUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0, ImageCount = -2 };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                ImageCostPerImage = 0.04m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Invalid refund test");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund image count must be non-negative.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithNegativeVideoDuration_ReturnsValidationError()
        {
            // Arrange
            var modelId = "video-model";
            var originalUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = 10.0 
            };
            var refundUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = -5.0 
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.1m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Invalid video refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund video duration must be non-negative.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithMismatchedVideoResolution_StillCalculates()
        {
            // Arrange - refund uses different resolution than original
            var modelId = "video-model";
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
                VideoResolution = "1280x720" // Different resolution
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = JsonSerializer.Serialize(new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m,
                    ["1280x720"] = 1.0m
                })
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Resolution mismatch refund");

            // Assert
            result.Should().NotBeNull();
            // Uses the refund's resolution multiplier: 5.0 * 0.1 * 1.0 = 0.5
            result.RefundAmount.Should().Be(0.5m);
            result.Breakdown!.VideoRefund.Should().Be(0.5m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithOnlyInputTokenRefund_NoCompletionRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 600, CompletionTokens = 0, TotalTokens = 600 };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial input refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.006m); // 600 * 0.00001
            result.Breakdown!.InputTokenRefund.Should().Be(0.006m);
            result.Breakdown.OutputTokenRefund.Should().Be(0m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithOnlyOutputTokenRefund_NoInputRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 0, CompletionTokens = 300, TotalTokens = 300 };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial output refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.009m); // 300 * 0.00003
            result.Breakdown!.InputTokenRefund.Should().Be(0m);
            result.Breakdown.OutputTokenRefund.Should().Be(0.009m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithCancellationToken_PropagatesToken()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            using var cts = new CancellationTokenSource();

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, cts.Token))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Test refund", null, cts.Token);

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.0125m);
            _modelCostServiceMock.Verify(x => x.GetCostForModelAsync(modelId, cts.Token), Times.Once);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithRegularCostWhenEmbeddingCostExists_UsesInputCost()
        {
            // Arrange - Model has embedding cost but usage has completion tokens (not an embedding request)
            var modelId = "multimodal/model";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,       // Regular input cost
                OutputCostPerMillionTokens = 30.00m,      // Regular output cost
                EmbeddingCostPerMillionTokens = 0.10m  // Embedding cost (not used here)
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Regular refund");

            // Assert
            result.Should().NotBeNull();
            // Should use regular costs: (500 * 0.00001) + (250 * 0.00003) = 0.005 + 0.0075 = 0.0125
            result.RefundAmount.Should().Be(0.0125m);
            result.Breakdown!.InputTokenRefund.Should().Be(0.005m);
            result.Breakdown.OutputTokenRefund.Should().Be(0.0075m);
            result.Breakdown.EmbeddingRefund.Should().Be(0m);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0)] // All zero
        [InlineData(100, 50, 50, 25, 0.00125)] // Exact half refund
        [InlineData(1000, 500, 1000, 500, 0.025)] // Full refund
        [InlineData(2000, 1000, 100, 50, 0.0025)] // Small partial refund
        public async Task CalculateRefundAsync_WithVariousScenarios_CalculatesCorrectly(
            int originalPrompt, int originalCompletion, int refundPrompt, int refundCompletion, decimal expectedRefund)
        {
            // Arrange
            var modelId = "test/model";
            var originalUsage = new Usage 
            { 
                PromptTokens = originalPrompt, 
                CompletionTokens = originalCompletion, 
                TotalTokens = originalPrompt + originalCompletion 
            };
            var refundUsage = new Usage 
            { 
                PromptTokens = refundPrompt, 
                CompletionTokens = refundCompletion, 
                TotalTokens = refundPrompt + refundCompletion 
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Test scenario");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(expectedRefund);
        }

        #endregion
    }
}