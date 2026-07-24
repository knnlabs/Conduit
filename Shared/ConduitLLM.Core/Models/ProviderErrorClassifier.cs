using System.Net;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Provides the common status/body classification used by provider-key tracking
/// and same-request failover.
/// </summary>
public static class ProviderErrorClassifier
{
    public static ProviderErrorType Classify(HttpStatusCode? statusCode, string? responseDetails = null)
    {
        var errorType = statusCode switch
        {
            HttpStatusCode.Unauthorized => ProviderErrorType.InvalidApiKey,
            HttpStatusCode.PaymentRequired => ProviderErrorType.InsufficientBalance,
            HttpStatusCode.Forbidden => ProviderErrorType.AccessForbidden,
            HttpStatusCode.TooManyRequests => ProviderErrorType.RateLimitExceeded,
            HttpStatusCode.NotFound => ProviderErrorType.ModelNotFound,
            HttpStatusCode.ServiceUnavailable => ProviderErrorType.ServiceUnavailable,
            HttpStatusCode.BadGateway => ProviderErrorType.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout => ProviderErrorType.Timeout,
            HttpStatusCode.RequestTimeout => ProviderErrorType.Timeout,
            HttpStatusCode.InternalServerError => ProviderErrorType.ServiceUnavailable,
            _ => ProviderErrorType.Unknown
        };

        if (errorType == ProviderErrorType.AccessForbidden &&
            IsBalanceResponse(responseDetails))
        {
            return ProviderErrorType.InsufficientBalance;
        }

        return errorType;
    }

    public static bool IsFatal(ProviderErrorType errorType)
        => errorType is ProviderErrorType.InvalidApiKey
            or ProviderErrorType.InsufficientBalance
            or ProviderErrorType.AccessForbidden;

    private static bool IsBalanceResponse(string? responseDetails)
    {
        if (string.IsNullOrWhiteSpace(responseDetails))
        {
            return false;
        }

        var details = responseDetails.ToLowerInvariant();
        return details.Contains("insufficient_quota", StringComparison.Ordinal) ||
               details.Contains("exceeded your current quota", StringComparison.Ordinal) ||
               details.Contains("billing", StringComparison.Ordinal) ||
               details.Contains("payment", StringComparison.Ordinal) ||
               details.Contains("credit", StringComparison.Ordinal);
    }
}
