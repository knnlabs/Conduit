using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Additional test cases for CostCalculationService to improve test coverage.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    [Trait("Component", "Core")]
    public class CostCalculationServiceAdditionalTests : TestBase
    {
        private readonly Mock<IModelCostService> _modelCostServiceMock;
        private readonly Mock<ILogger<CostCalculationService>> _loggerMock;
        private readonly CostCalculationService _service;

        public CostCalculationServiceAdditionalTests(ITestOutputHelper output) : base(output)
        {
            _modelCostServiceMock = new Mock<IModelCostService>();
            _loggerMock = CreateLogger<CostCalculationService>();
            _service = new CostCalculationService(_modelCostServiceMock.Object, _loggerMock.Object);
        }

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
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00002m,
                VideoCostPerSecond = null // No video cost defined
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Should only calculate text costs: (100 * 0.00001) + (50 * 0.00002) = 0.001 + 0.001 = 0.002
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
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00002m,
                ImageCostPerImage = null // No image cost defined
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Should only calculate text costs: (100 * 0.00001) + (50 * 0.00002) = 0.001 + 0.001 = 0.002
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
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m
                    // No multiplier for empty string
                }
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
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m
                }
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
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00002m,
                EmbeddingTokenCost = null // No embedding cost defined
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
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.01m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1280x720"] = 1.0m
                }
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

        #region Refund Method Additional Tests

        [Fact]
        public async Task CalculateRefundAsync_WithZeroRefundUsage_ReturnsZeroRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0 };
            var refundReason = "No actual usage to refund";

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m,
                    ["1280x720"] = 1.0m
                    // 4K-UHD not in dictionary
                }
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m,
                    ["1280x720"] = 1.0m
                }
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,       // Regular input cost
                OutputTokenCost = 0.00003m,      // Regular output cost
                EmbeddingTokenCost = 0.0000001m  // Embedding cost (not used here)
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
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

        #region Logging Tests

        [Fact]
        public async Task CalculateRefundAsync_LogsRefundInformation()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            var refundReason = "Service interruption";
            var originalTransactionId = "txn_12345";

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, refundReason, originalTransactionId);

            // Assert
            _loggerMock.VerifyLog(
                LogLevel.Information,
                "Calculated refund for model",
                Times.Once());
        }

        [Fact]
        public async Task CalculateRefundAsync_WithModelNotFound_LogsWarning()
        {
            // Arrange
            var modelId = "non-existent-model";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCostInfo?)null);

            // Act
            await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Test refund");

            // Assert
            _loggerMock.VerifyLog(
                LogLevel.Warning,
                "Cost information not found for model",
                Times.Once());
        }

        #endregion

        #region Concurrent and Thread Safety Tests

        [Fact]
        public async Task CalculateCostAsync_ConcurrentCalls_HandledCorrectly()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            var tasks = new List<Task<decimal>>();

            // Act - Create 100 concurrent tasks
            for (int i = 0; i < 100; i++)
            {
                var usage = new Usage
                {
                    PromptTokens = i * 10,
                    CompletionTokens = i * 5,
                    TotalTokens = i * 15
                };
                tasks.Add(_service.CalculateCostAsync(modelId, usage));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - Verify all calculations are correct
            for (int i = 0; i < 100; i++)
            {
                var expectedCost = (i * 10 * 0.00001m) + (i * 5 * 0.00003m);
                results[i].Should().Be(expectedCost);
            }
        }

        #endregion
    }
}