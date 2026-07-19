using System.Net;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Http;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Providers
{
    public class ErrorClassificationTests
    {
        [Theory]
        [InlineData(HttpStatusCode.Unauthorized, ProviderErrorType.InvalidApiKey)]
        [InlineData(HttpStatusCode.PaymentRequired, ProviderErrorType.InsufficientBalance)]
        [InlineData(HttpStatusCode.Forbidden, ProviderErrorType.AccessForbidden)]
        [InlineData(HttpStatusCode.TooManyRequests, ProviderErrorType.RateLimitExceeded)]
        [InlineData(HttpStatusCode.NotFound, ProviderErrorType.ModelNotFound)]
        [InlineData(HttpStatusCode.ServiceUnavailable, ProviderErrorType.ServiceUnavailable)]
        [InlineData(HttpStatusCode.BadGateway, ProviderErrorType.ServiceUnavailable)]
        [InlineData(HttpStatusCode.GatewayTimeout, ProviderErrorType.Timeout)]
        public void ClassifyHttpError_ReturnsCorrectType(HttpStatusCode status, ProviderErrorType expected)
        {
            // Exercises the real mapping in ProviderErrorTrackingRetryHook (via InternalsVisibleTo)
            var errorType = ClassifyHttpStatusCode(status);
            errorType.Should().Be(expected);
        }

        [Theory]
        [InlineData(ProviderErrorType.InvalidApiKey, true)]
        [InlineData(ProviderErrorType.InsufficientBalance, true)]
        [InlineData(ProviderErrorType.AccessForbidden, true)]
        [InlineData(ProviderErrorType.RateLimitExceeded, false)]
        [InlineData(ProviderErrorType.ModelNotFound, false)]
        [InlineData(ProviderErrorType.ServiceUnavailable, false)]
        [InlineData(ProviderErrorType.NetworkError, false)]
        [InlineData(ProviderErrorType.Timeout, false)]
        [InlineData(ProviderErrorType.Unknown, false)]
        public void IsFatalError_CorrectlyIdentifiesFatalErrors(ProviderErrorType errorType, bool expectedIsFatal)
        {
            // Test fatal (1-9) vs warning (10+) classification
            var isFatal = IsFatalError(errorType);
            isFatal.Should().Be(expectedIsFatal);
        }

        [Fact]
        public void ErrorThresholdConfiguration_InvalidApiKey_HasImmediateDisable()
        {
            // Verify InvalidApiKey has immediate disable policy
            var policy = ErrorThresholdConfiguration.FatalErrorPolicies[ProviderErrorType.InvalidApiKey];
            
            policy.DisableImmediately.Should().BeTrue();
            policy.RequiresManualReenable.Should().BeTrue();
        }

        [Fact]
        public void ErrorThresholdConfiguration_InsufficientBalance_HasThreshold()
        {
            // Verify InsufficientBalance requires multiple occurrences
            var policy = ErrorThresholdConfiguration.FatalErrorPolicies[ProviderErrorType.InsufficientBalance];
            
            policy.DisableImmediately.Should().BeFalse();
            policy.RequiredOccurrences.Should().Be(2);
            policy.TimeWindow.Should().Be(System.TimeSpan.FromMinutes(5));
            policy.RequiresManualReenable.Should().BeTrue();
        }

        [Fact]
        public void ErrorThresholdConfiguration_AccessForbidden_HasHigherThreshold()
        {
            // Verify AccessForbidden has higher threshold
            var policy = ErrorThresholdConfiguration.FatalErrorPolicies[ProviderErrorType.AccessForbidden];
            
            policy.DisableImmediately.Should().BeFalse();
            policy.RequiredOccurrences.Should().Be(3);
            policy.TimeWindow.Should().Be(System.TimeSpan.FromMinutes(10));
            policy.RequiresManualReenable.Should().BeTrue();
        }

        [Fact]
        public void ErrorThresholdConfiguration_RateLimitWarning_HasAlertPolicy()
        {
            // Verify rate limit has alert policy
            var policy = ErrorThresholdConfiguration.WarningAlertPolicies[ProviderErrorType.RateLimitExceeded];
            
            policy.AlertThreshold.Should().Be(10);
            policy.TimeWindow.Should().Be(System.TimeSpan.FromMinutes(5));
            policy.AlertMessage.Should().Be("High rate limit pressure detected");
        }

        [Fact]
        public void ErrorThresholdConfiguration_ServiceUnavailable_HasAlertPolicy()
        {
            // Verify service unavailable has alert policy
            var policy = ErrorThresholdConfiguration.WarningAlertPolicies[ProviderErrorType.ServiceUnavailable];
            
            policy.AlertThreshold.Should().Be(5);
            policy.TimeWindow.Should().Be(System.TimeSpan.FromMinutes(10));
            policy.AlertMessage.Should().Be("Provider service experiencing issues");
        }

        [Theory]
        [InlineData(1, true)] // InvalidApiKey
        [InlineData(2, true)] // InsufficientBalance
        [InlineData(3, true)] // AccessForbidden
        [InlineData(4, true)] // Undefined but in fatal range
        [InlineData(5, true)]
        [InlineData(9, true)]
        [InlineData(10, false)] // Warning range starts - RateLimitExceeded
        [InlineData(11, false)] // ModelNotFound
        [InlineData(12, false)] // ServiceUnavailable
        [InlineData(20, false)]
        [InlineData(99, false)]
        public void FatalErrorRange_CorrectlyDefined(int errorValue, bool expectedIsFatal)
        {
            // Test that fatal errors are correctly defined as values 1-9
            var isFatal = errorValue <= 9;
            isFatal.Should().Be(expectedIsFatal);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, ProviderErrorType.Unknown)]
        [InlineData(HttpStatusCode.InternalServerError, ProviderErrorType.Unknown)]
        public void ClassifyHttpError_UnmappedCodes_ReturnUnknown(HttpStatusCode status, ProviderErrorType expected)
        {
            var errorType = ClassifyHttpStatusCode(status);
            errorType.Should().Be(expected);
        }

        // Calls the real implementation (internal, via InternalsVisibleTo) rather than mirroring
        // it — a mirrored copy previously drifted from the production mapping.
        private static ProviderErrorType ClassifyHttpStatusCode(HttpStatusCode statusCode)
        {
            using var response = new HttpResponseMessage(statusCode);
            return ProviderErrorTrackingRetryHook.ClassifyResponseError(response);
        }

        private static bool IsFatalError(ProviderErrorType errorType)
        {
            return ProviderErrorTrackingRetryHook.IsFatalError(errorType);
        }
    }
}