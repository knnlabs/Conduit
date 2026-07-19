using ConduitLLM.Core.Models;
using FluentAssertions;
using Moq;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Basic refund calculation tests
    /// </summary>
    public partial class CostCalculationServiceRefundTests
    {
        [Fact]
        public async Task CalculateRefundAsync_WithValidInputs_CalculatesCorrectRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            var refundReason = "Service interruption";
            var originalTransactionId = "txn_12345";

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m, // $0.01 per 1K tokens
                OutputCostPerMillionTokens = 30.00m  // $0.03 per 1K tokens
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
            result.RefundAmount.Should().Be(0.0125m); // (500 * 10.00 / 1_000_000) + (250 * 30.00 / 1_000_000)
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
        public async Task CalculateRefundAsync_WithRefundExceedingOriginal_RejectsWithZeroAmount()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 1500, CompletionTokens = 750, TotalTokens = 2250 };

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
                modelId, originalUsage, refundUsage, "Excessive refund test");

            // Assert - a refund exceeding the original charge must be rejected, not credited.
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0m, "an over-limit refund must never produce a credit");
            result.ValidationMessages.Should().HaveCount(2);
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund prompt tokens (1500) cannot exceed original (1000)"));
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund completion tokens (750) cannot exceed original (500)"));
        }

        [Fact]
        public async Task CalculateRefundAsync_WithRefundExceedingOriginalOnOneDimension_RejectsEntireRefund()
        {
            // Arrange - completion tokens exceed original by 1; everything else is within bounds.
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 1000, CompletionTokens = 501, TotalTokens = 1501 };

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
                modelId, originalUsage, refundUsage, "Boundary test");

            // Assert - a single over-limit dimension rejects the whole refund (no partial credit).
            result.RefundAmount.Should().Be(0m);
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund completion tokens (501) cannot exceed original (500)"));
        }

        [Fact]
        public async Task CalculateRefundAsync_WithAnthropicCachedInputTokens_RefundsFreshInputTokens()
        {
            var modelId = "anthropic/claude-sonnet";
            var originalUsage = new Usage
            {
                PromptTokens = 500,
                CachedInputTokens = 10000,
                CachedInputTokensIncludedInPrompt = false
            };
            var refundUsage = new Usage
            {
                PromptTokens = 500,
                CachedInputTokens = 10000,
                CachedInputTokensIncludedInPrompt = false
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 3m,
                OutputCostPerMillionTokens = 15m,
                CachedInputCostPerMillionTokens = 0.3m
            };
            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            var result = await _service.CalculateRefundAsync(modelId, originalUsage, refundUsage, "Full refund");

            result.RefundAmount.Should().Be(0.0045m);
            result.Breakdown!.InputTokenRefund.Should().Be(0.0045m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithReasoningTokens_DoesNotDoubleRefundCompletionSubset()
        {
            var modelId = "reasoning/model";
            var originalUsage = new Usage { CompletionTokens = 500, ReasoningTokens = 200 };
            var refundUsage = new Usage { CompletionTokens = 500, ReasoningTokens = 200 };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10m,
                OutputCostPerMillionTokens = 30m,
                ReasoningCostPerMillionTokens = 60m
            };
            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            var result = await _service.CalculateRefundAsync(modelId, originalUsage, refundUsage, "Full refund");

            result.RefundAmount.Should().Be(0.021m);
            result.Breakdown!.OutputTokenRefund.Should().Be(0.009m);
        }
    }
}
