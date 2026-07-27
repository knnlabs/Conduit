using System.Net;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Models;

public class ProviderErrorClassifierTests
{
    [Theory]
    [InlineData("invalid_api_key", ProviderErrorType.InvalidApiKey)]
    [InlineData("InsufficientBalance", ProviderErrorType.InsufficientBalance)]
    [InlineData("access-forbidden", ProviderErrorType.AccessForbidden)]
    [InlineData("rate_limit_exceeded", ProviderErrorType.RateLimitExceeded)]
    [InlineData("model_not_found", ProviderErrorType.ModelNotFound)]
    [InlineData("503", ProviderErrorType.ServiceUnavailable)]
    [InlineData("network_error", ProviderErrorType.NetworkError)]
    [InlineData("gateway_timeout", ProviderErrorType.Timeout)]
    [InlineData("validation_error", ProviderErrorType.Unknown)]
    public void FromErrorCode_MapsKnownWireCodes(
        string errorCode,
        ProviderErrorType expected)
    {
        Assert.Equal(expected, ProviderErrorClassifier.FromErrorCode(errorCode));
    }

    [Fact]
    public void ClassifyFailure_PrefersStructuredCodeOverMessage()
    {
        var result = ProviderErrorClassifier.ClassifyFailure(
            "rate_limit_exceeded",
            "The request timed out");

        Assert.Equal(ProviderErrorType.RateLimitExceeded, result);
    }

    [Fact]
    public void ClassifyException_UnwrapsCommunicationException()
    {
        var inner = new LLMCommunicationException(
            "provider error",
            HttpStatusCode.Forbidden,
            "insufficient_quota");
        var outer = new InvalidOperationException("wrapped", inner);

        Assert.Equal(
            ProviderErrorType.InsufficientBalance,
            ProviderErrorClassifier.ClassifyException(outer));
    }

    [Theory]
    [InlineData(ProviderErrorType.InvalidApiKey, "invalid_api_key", "authentication")]
    [InlineData(ProviderErrorType.RateLimitExceeded, "rate_limit_exceeded", "rate_limit")]
    [InlineData(ProviderErrorType.ServiceUnavailable, "service_unavailable", "provider")]
    [InlineData(ProviderErrorType.Unknown, "unknown", "unknown")]
    public void MetricMappings_AreStable(
        ProviderErrorType errorType,
        string expectedLabel,
        string expectedCategory)
    {
        Assert.Equal(expectedLabel, ProviderErrorClassifier.ToMetricLabel(errorType));
        Assert.Equal(expectedCategory, ProviderErrorClassifier.ToMetricCategory(errorType));
    }
}
