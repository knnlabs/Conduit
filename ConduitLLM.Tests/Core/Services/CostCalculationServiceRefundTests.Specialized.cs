using ConduitLLM.Core.Models;
using FluentAssertions;
using Moq;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Specialized refund calculation tests (embedding, search units, inference steps)
    /// </summary>
    public partial class CostCalculationServiceRefundTests
    {
        [Fact]
        public async Task CalculateRefundAsync_WithEmbeddingRefund_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/text-embedding-ada-002";
            var originalUsage = new Usage { PromptTokens = 5000, CompletionTokens = 0, TotalTokens = 5000 };
            var refundUsage = new Usage { PromptTokens = 2000, CompletionTokens = 0, TotalTokens = 2000 };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 0m,
                EmbeddingCostPerMillionTokens = 100.00m // $0.0001 per token
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

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 100.00m,       // Regular cost (expensive)
                OutputCostPerMillionTokens = 0m,
                EmbeddingCostPerMillionTokens = 10.00m,  // Embedding cost (10x cheaper)
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

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
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

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
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

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
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

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
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
    }
}