using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

using AwesomeAssertions;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for proportional refunds of requests that were billed from a trusted provider-reported
    /// cost (via ProviderCostRefundContext).
    /// </summary>
    public partial class CostCalculationServiceRefundTests
    {
        [Fact]
        public async Task CalculateRefundAsync_ProviderCost_FullTokenRefund_RefundsFullChargedAmount()
        {
            // Arrange
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var context = new ProviderCostRefundContext { OriginalChargedCost = 0.02m };

            // Act
            var result = await _service.CalculateRefundAsync(
                "openrouter/model", originalUsage, refundUsage, "full refund", "txn-1", context);

            // Assert
            result.RefundAmount.Should().Be(0.02m);
        }

        [Fact]
        public async Task CalculateRefundAsync_ProviderCost_HalfTokenRefund_RefundsHalfChargedAmount()
        {
            // Arrange — refunding half the billable tokens
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            var context = new ProviderCostRefundContext { OriginalChargedCost = 0.02m };

            // Act
            var result = await _service.CalculateRefundAsync(
                "openrouter/model", originalUsage, refundUsage, "partial refund", "txn-2", context);

            // Assert
            result.RefundAmount.Should().Be(0.01m);
        }

        [Fact]
        public async Task CalculateRefundAsync_ProviderCost_DoesNotConsultModelCost()
        {
            // Arrange
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var context = new ProviderCostRefundContext { OriginalChargedCost = 0.02m };

            // Act
            await _service.CalculateRefundAsync(
                "openrouter/model", originalUsage, refundUsage, "reason", "txn-3", context);

            // Assert — the ModelCost lookup is never performed for provider-cost refunds
            _modelCostServiceMock.Verify(
                x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CalculateRefundAsync_ProviderCost_NoTokenBasis_RefundsFullChargedAmount()
        {
            // Arrange — image-only original (no billable tokens) → any refund is a full refund
            var originalUsage = new Usage { ImageCount = 1 };
            var refundUsage = new Usage { ImageCount = 1 };
            var context = new ProviderCostRefundContext { OriginalChargedCost = 0.04m };

            // Act
            var result = await _service.CalculateRefundAsync(
                "openrouter/image", originalUsage, refundUsage, "reason", "txn-4", context);

            // Assert
            result.RefundAmount.Should().Be(0.04m);
        }

        [Fact]
        public async Task CalculateRefundAsync_ProviderCost_ExcessRefund_StillRejectedByValidation()
        {
            // Arrange — refund tokens exceed original; validation must reject before proration
            var originalUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            var refundUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var context = new ProviderCostRefundContext { OriginalChargedCost = 0.02m };

            // Act
            var result = await _service.CalculateRefundAsync(
                "openrouter/model", originalUsage, refundUsage, "reason", "txn-5", context);

            // Assert
            result.RefundAmount.Should().Be(0m);
            result.ValidationMessages.Should().NotBeEmpty();
        }
    }
}
