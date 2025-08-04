using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for refund calculation functionality
    /// </summary>
    public class CostCalculationServiceRefundTests : CostCalculationServiceTestBase
    {
        public CostCalculationServiceRefundTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task CalculateRefundAsync_WithValidInputs_CalculatesCorrectRefund()
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
                InputTokenCost = 0.00001m, // $0.01 per 1K tokens
                OutputTokenCost = 0.00003m  // $0.03 per 1K tokens
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, refundReason, originalTransactionId);

            // Assert
            result.Should().NotBeNull();
            result.ModelId.Should().Be(modelId);
            result.RefundReason.Should().Be(refundReason);
            result.OriginalTransactionId.Should().Be(originalTransactionId);
            result.RefundAmount.Should().Be(0.0125m); // (500 * 0.00001) + (250 * 0.00003)
            result.Breakdown.Should().NotBeNull();
            result.Breakdown!.InputTokenRefund.Should().Be(0.005m);
            result.Breakdown.OutputTokenRefund.Should().Be(0.0075m);
            result.IsPartialRefund.Should().BeFalse();
            result.ValidationMessages.Should().BeEmpty();
        }

        [Fact]
        public async Task CalculateRefundAsync_WithEmptyModelId_ReturnsValidationError()
        {
            // Arrange
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };

            // Act
            var result = await _service.CalculateRefundAsync(
                "", originalUsage, refundUsage, "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Model ID is required for refund calculation.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithNullUsageData_ReturnsValidationError()
        {
            // Act
            var result = await _service.CalculateRefundAsync(
                "openai/gpt-4o", null!, null!, "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Both original and refund usage data are required.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithEmptyRefundReason_ReturnsValidationError()
        {
            // Arrange
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };

            // Act
            var result = await _service.CalculateRefundAsync(
                "openai/gpt-4o", originalUsage, refundUsage, "");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund reason is required.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithRefundExceedingOriginal_ReturnsPartialRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 1500, CompletionTokens = 750, TotalTokens = 2250 };

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
                modelId, originalUsage, refundUsage, "Excessive refund test");

            // Assert
            result.Should().NotBeNull();
            result.IsPartialRefund.Should().BeTrue();
            result.ValidationMessages.Should().HaveCount(2);
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund prompt tokens (1500) cannot exceed original (1000)"));
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund completion tokens (750) cannot exceed original (500)"));
        }

        [Fact]
        public async Task CalculateRefundAsync_WithImageRefund_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var originalUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0, ImageCount = 5 };
            var refundUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0, ImageCount = 2 };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
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
        public async Task CalculateRefundAsync_WithEmbeddingRefund_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/text-embedding-ada-002";
            var originalUsage = new Usage { PromptTokens = 5000, CompletionTokens = 0, TotalTokens = 5000 };
            var refundUsage = new Usage { PromptTokens = 2000, CompletionTokens = 0, TotalTokens = 2000 };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0m,
                EmbeddingTokenCost = 0.0001m // $0.0001 per token
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Embedding service error");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.2m); // 2000 * 0.0001
            result.Breakdown!.EmbeddingRefund.Should().Be(0.2m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithEmbeddingAndImages_UsesEmbeddingCost()
        {
            // Arrange
            var modelId = "openai/multimodal-embed";
            var originalUsage = new Usage { PromptTokens = 5000, CompletionTokens = 0, TotalTokens = 5000, ImageCount = 3 };
            var refundUsage = new Usage { PromptTokens = 2000, CompletionTokens = 0, TotalTokens = 2000, ImageCount = 1 };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.0001m,       // Regular cost (expensive)
                OutputTokenCost = 0m,
                EmbeddingTokenCost = 0.00001m,  // Embedding cost (10x cheaper)
                ImageCostPerImage = 0.02m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial refund for embedding with images");

            // Assert
            result.Should().NotBeNull();
            // Embedding refund: 2000 * 0.00001 = 0.02
            // Image refund: 1 * 0.02 = 0.02
            // Total: 0.04
            result.RefundAmount.Should().Be(0.04m);
            result.Breakdown!.EmbeddingRefund.Should().Be(0.02m);
            result.Breakdown.ImageRefund.Should().Be(0.02m);
            result.Breakdown.InputTokenRefund.Should().Be(0m); // Should not use input token cost
        }

        [Fact]
        public async Task CalculateRefundAsync_WithNegativeValues_ReturnsValidationError()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = -100, CompletionTokens = -50, TotalTokens = -150 };

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
                modelId, originalUsage, refundUsage, "Invalid refund test");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund token counts must be non-negative.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithModelNotFound_ReturnsValidationMessage()
        {
            // Arrange
            var modelId = "non-existent-model";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCostInfo?)null);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain($"Cost information not found for model {modelId}.");
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

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m,
                ImageCostPerImage = 0.04m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1280x720"] = 1.0m
                }
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

        [Fact]
        public async Task CalculateRefundAsync_WithCachedTokens_CalculatesCorrectRefund()
        {
            // Arrange
            var modelId = "google/gemini-1.5-flash";
            var originalUsage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                CachedInputTokens = 400,
                CachedWriteTokens = 200
            };
            var refundUsage = new Usage
            {
                PromptTokens = 500,
                CompletionTokens = 250,
                TotalTokens = 750,
                CachedInputTokens = 200,
                CachedWriteTokens = 100
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m,
                CachedInputTokenCost = 0.000001m,
                CachedInputWriteCost = 0.000025m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial service interruption");

            // Assert
            // Regular input refund: 300 * 0.00001 = 0.003 (500 total - 200 cached = 300 regular)
            // Cached input refund: 200 * 0.000001 = 0.0002
            // Cache write refund: 100 * 0.000025 = 0.0025
            // Output refund: 250 * 0.00003 = 0.0075
            // Total refund: 0.003 + 0.0002 + 0.0025 + 0.0075 = 0.0132
            result.RefundAmount.Should().Be(0.0132m);
            result.IsPartialRefund.Should().BeFalse();
        }

        [Fact]
        public async Task CalculateRefundAsync_WithSearchUnits_CalculatesCorrectRefund()
        {
            // Arrange
            var modelId = "cohere/rerank-3.5";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 50
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 20
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerSearchUnit = 2.0m // $2.00 per 1K search units
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Service interruption");

            // Assert
            // Expected refund: 20 * (2.0 / 1000) = 20 * 0.002 = 0.04
            result.RefundAmount.Should().Be(0.04m);
            result.Breakdown!.SearchUnitRefund.Should().Be(0.04m);
            result.IsPartialRefund.Should().BeFalse();
        }

        [Fact]
        public async Task CalculateRefundAsync_WithSearchUnitsExceedingOriginal_ReportsValidationError()
        {
            // Arrange
            var modelId = "cohere/rerank-3.5";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 20
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 30 // More than original
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerSearchUnit = 2.0m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Invalid refund request");

            // Assert
            result.IsPartialRefund.Should().BeTrue();
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund search units"));
        }

        [Fact]
        public async Task CalculateRefundAsync_WithInferenceSteps_CalculatesCorrectRefund()
        {
            // Arrange
            var modelId = "fireworks/flux-pro";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 3,
                InferenceSteps = 20
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 1,
                InferenceSteps = 20
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerInferenceStep = 0.0005m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial image generation failure");

            // Assert
            // Expected refund: 20 * 0.0005 = 0.01
            result.RefundAmount.Should().Be(0.01m);
            result.Breakdown!.InferenceStepRefund.Should().Be(0.01m);
            result.IsPartialRefund.Should().BeFalse();
        }

        [Fact]
        public async Task CalculateRefundAsync_WithInferenceStepsExceedingOriginal_ReportsValidationError()
        {
            // Arrange
            var modelId = "fireworks/sdxl";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                InferenceSteps = 30
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                InferenceSteps = 50 // More than original
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerInferenceStep = 0.00013m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Invalid refund request");

            // Assert
            result.IsPartialRefund.Should().BeTrue();
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund inference steps"));
        }

        [Fact]
        public async Task CalculateRefundAsync_WithBatchProcessing_AppliesDiscountToRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                IsBatch = true
            };
            var refundUsage = new Usage
            {
                PromptTokens = 500,
                CompletionTokens = 200,
                TotalTokens = 700,
                IsBatch = true
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.001m,
                OutputTokenCost = 0.002m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = 0.5m // 50% discount
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId,
                originalUsage,
                refundUsage,
                "Test refund",
                "transaction-123"
            );

            // Assert
            result.Should().NotBeNull();
            result.ValidationMessages.Should().BeEmpty();
            // Expected refund without batch: (500 * 0.001) + (200 * 0.002) = 0.5 + 0.4 = 0.9
            // Expected with 50% batch discount: 0.9 * 0.5 = 0.45
            result.RefundAmount.Should().Be(0.45m);
            result.Breakdown.Should().NotBeNull();
        }

        [Fact]
        public async Task CalculateRefundAsync_WithImageQualityMultiplier_AppliesMultiplierToRefund()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 5,
                ImageQuality = "hd"
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 2,
                ImageQuality = "hd"
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                ImageCostPerImage = 0.04m,
                ImageQualityMultipliers = new Dictionary<string, decimal>
                {
                    ["standard"] = 1.0m,
                    ["hd"] = 2.0m
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId,
                originalUsage,
                refundUsage,
                "Quality issue with generated images",
                "transaction-456"
            );

            // Assert
            result.Should().NotBeNull();
            result.ValidationMessages.Should().BeEmpty();
            // Expected refund: 2 images * 0.04 base cost * 2.0 HD multiplier = 0.16
            result.RefundAmount.Should().Be(0.16m);
            result.Breakdown.Should().NotBeNull();
            result.Breakdown.ImageRefund.Should().Be(0.16m);
        }
    }
}