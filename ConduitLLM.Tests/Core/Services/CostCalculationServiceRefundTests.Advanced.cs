using System.Text.Json;
using ConduitLLM.Core.Models;
using FluentAssertions;
using Moq;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Advanced refund calculation tests (cached tokens, batch processing, quality multipliers, validation)
    /// </summary>
    public partial class CostCalculationServiceRefundTests
    {
        [Fact]
        public async Task CalculateRefundAsync_WithNegativeValues_ReturnsValidationError()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = -100, CompletionTokens = -50, TotalTokens = -150 };

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
                .ReturnsAsync((ModelCost?)null);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain($"Cost information not found for model {modelId}.");
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

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m,
                CachedInputCostPerMillionTokens = 1.00m,
                CachedInputWriteCostPerMillionTokens = 25.00m
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
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 1000.00m,
                OutputCostPerMillionTokens = 2000.00m,
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
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                ImageCostPerImage = 0.04m,
                ImageQualityMultipliers = JsonSerializer.Serialize(new Dictionary<string, decimal>
                {
                    ["standard"] = 1.0m,
                    ["hd"] = 2.0m
                })
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