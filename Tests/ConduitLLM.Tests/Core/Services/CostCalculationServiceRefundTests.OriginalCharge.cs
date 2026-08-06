using ConduitLLM.Core.Models;

using AwesomeAssertions;

using Moq;

namespace ConduitLLM.Tests.Core.Services;

public partial class CostCalculationServiceRefundTests
{
    [Fact]
    public async Task CalculateRefundAsync_WithOriginalCharge_DoesNotUseCurrentModelPrice()
    {
        var originalUsage = new Usage
        {
            PromptTokens = 800,
            CompletionTokens = 200,
            TotalTokens = 1000
        };
        var refundUsage = new Usage
        {
            PromptTokens = 400,
            CompletionTokens = 100,
            TotalTokens = 500
        };
        var context = new ProviderCostRefundContext { OriginalChargedCost = 8m };

        var result = await _service.CalculateRefundAsync(
            "model-with-new-price", originalUsage, refundUsage, "partial", "42", context);

        result.RefundAmount.Should().Be(4m);
        result.IsPartialRefund.Should().BeTrue();
        _modelCostServiceMock.Verify(
            service => service.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [MemberData(nameof(NonTokenRefundCases))]
    public async Task CalculateRefundAsync_WithOriginalCharge_ProrationSupportsNonTokenPricing(
        Usage originalUsage,
        Usage refundUsage)
    {
        var context = new ProviderCostRefundContext { OriginalChargedCost = 12m };

        var result = await _service.CalculateRefundAsync(
            "non-token-model", originalUsage, refundUsage, "partial", "43", context);

        result.RefundAmount.Should().Be(6m);
        result.IsPartialRefund.Should().BeTrue();
        result.ValidationMessages.Should().BeEmpty();
    }

    public static TheoryData<Usage, Usage> NonTokenRefundCases => new()
    {
        {
            new Usage { ImageCount = 4 },
            new Usage { ImageCount = 2 }
        },
        {
            new Usage { VideoDurationSeconds = 10, VideoResolution = "1080p" },
            new Usage { VideoDurationSeconds = 5, VideoResolution = "1080p" }
        },
        {
            new Usage { InferenceSteps = 30 },
            new Usage { InferenceSteps = 15 }
        },
        {
            new Usage { SearchUnits = 8 },
            new Usage { SearchUnits = 4 }
        },
        {
            new Usage { AudioDurationSeconds = 120 },
            new Usage { AudioDurationSeconds = 60 }
        },
        {
            new Usage { TtsCharacters = 2000 },
            new Usage { TtsCharacters = 1000 }
        }
    };
}
