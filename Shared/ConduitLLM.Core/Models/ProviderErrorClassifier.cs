using System.Net;

using ConduitLLM.Core.Exceptions;

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
            { } status when (int)status >= 500 => ProviderErrorType.ServiceUnavailable,
            _ => ProviderErrorType.Unknown
        };

        if (errorType == ProviderErrorType.AccessForbidden &&
            IsBalanceResponse(responseDetails))
        {
            return ProviderErrorType.InsufficientBalance;
        }

        return errorType;
    }

    /// <summary>
    /// Classifies an exception from a provider call into a <see cref="ProviderErrorType"/>
    /// for error tracking. Communication exceptions carrying an HTTP status defer to
    /// <see cref="Classify"/> (including the balance-in-403 body refinement).
    /// </summary>
    public static ProviderErrorType ClassifyException(Exception ex)
    {
        var communicationException = LLMCommunicationException.FindWithStatus(ex);
        if (communicationException is not null)
        {
            return Classify(communicationException.StatusCode, communicationException.ResponseBody);
        }

        return ex switch
        {
            RateLimitExceededException => ProviderErrorType.RateLimitExceeded,
            RequestTimeoutException => ProviderErrorType.Timeout,
            ModelNotFoundException => ProviderErrorType.ModelNotFound,
            ServiceUnavailableException => ProviderErrorType.ServiceUnavailable,
            HttpRequestException => ProviderErrorType.NetworkError,
            _ => ProviderErrorType.Unknown
        };
    }

    /// <summary>
    /// Converts a structured provider error code (including the snake_case codes used
    /// on customer-facing events) into the canonical error type.
    /// </summary>
    public static ProviderErrorType FromErrorCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return ProviderErrorType.Unknown;
        }

        var normalized = new string(errorCode
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return normalized switch
        {
            "401" or "invalidapikey" or "authentication" or "authenticationerror"
                or "autherror" or "unauthorized" => ProviderErrorType.InvalidApiKey,
            "402" or "insufficientbalance" or "insufficientcredits"
                or "insufficientquota" or "paymentrequired" => ProviderErrorType.InsufficientBalance,
            "403" or "accessforbidden" or "forbidden" => ProviderErrorType.AccessForbidden,
            "429" or "ratelimit" or "ratelimitexceeded"
                or "toomanyrequests" => ProviderErrorType.RateLimitExceeded,
            "404" or "modelnotfound" or "notfound" => ProviderErrorType.ModelNotFound,
            "500" or "502" or "503" or "serviceunavailable"
                or "servererror" or "internalservererror" or "badgateway" => ProviderErrorType.ServiceUnavailable,
            "network" or "networkerror" or "httperror" or "socketerror" => ProviderErrorType.NetworkError,
            "408" or "504" or "timeout" or "requesttimeout"
                or "gatewaytimeout" or "providertimeout" => ProviderErrorType.Timeout,
            _ => ProviderErrorType.Unknown
        };
    }

    /// <summary>
    /// Classifies the code/message carried by an asynchronous provider failure event.
    /// Structured codes take precedence over message heuristics.
    /// </summary>
    public static ProviderErrorType ClassifyFailure(string? errorCode, string? errorMessage)
    {
        var fromCode = FromErrorCode(errorCode);
        if (fromCode != ProviderErrorType.Unknown)
        {
            return fromCode;
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return ProviderErrorType.Unknown;
        }

        if (IsBalanceResponse(errorMessage) ||
            errorMessage.Contains("insufficient credits", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderErrorType.InsufficientBalance;
        }

        if (ContainsAny(errorMessage, "invalid api key", "authentication failed", "unauthorized"))
        {
            return ProviderErrorType.InvalidApiKey;
        }

        if (errorMessage.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderErrorType.AccessForbidden;
        }

        if (ContainsAny(errorMessage, "rate limit", "too many requests"))
        {
            return ProviderErrorType.RateLimitExceeded;
        }

        if (ContainsAny(errorMessage, "model not found", "resource not found"))
        {
            return ProviderErrorType.ModelNotFound;
        }

        if (ContainsAny(errorMessage, "service unavailable", "server error", "internal server", "bad gateway"))
        {
            return ProviderErrorType.ServiceUnavailable;
        }

        if (ContainsAny(errorMessage, "timeout", "timed out"))
        {
            return ProviderErrorType.Timeout;
        }

        if (ContainsAny(errorMessage, "network", "connection refused", "socket"))
        {
            return ProviderErrorType.NetworkError;
        }

        return ProviderErrorType.Unknown;
    }

    /// <summary>
    /// Stable low-cardinality label used by media metrics and structured failure logs.
    /// </summary>
    public static string ToMetricLabel(ProviderErrorType errorType) => errorType switch
    {
        ProviderErrorType.InvalidApiKey => "invalid_api_key",
        ProviderErrorType.InsufficientBalance => "insufficient_balance",
        ProviderErrorType.AccessForbidden => "access_forbidden",
        ProviderErrorType.RateLimitExceeded => "rate_limit_exceeded",
        ProviderErrorType.ModelNotFound => "model_not_found",
        ProviderErrorType.ServiceUnavailable => "service_unavailable",
        ProviderErrorType.NetworkError => "network_error",
        ProviderErrorType.Timeout => "timeout",
        _ => "unknown"
    };

    /// <summary>
    /// Stable broad category paired with <see cref="ToMetricLabel"/>.
    /// </summary>
    public static string ToMetricCategory(ProviderErrorType errorType) => errorType switch
    {
        ProviderErrorType.InvalidApiKey or ProviderErrorType.AccessForbidden => "authentication",
        ProviderErrorType.InsufficientBalance => "billing",
        ProviderErrorType.RateLimitExceeded => "rate_limit",
        ProviderErrorType.ModelNotFound => "validation",
        ProviderErrorType.ServiceUnavailable => "provider",
        ProviderErrorType.NetworkError => "network",
        ProviderErrorType.Timeout => "timeout",
        _ => "unknown"
    };

    public static bool IsFatal(ProviderErrorType errorType)
        => errorType is ProviderErrorType.InvalidApiKey
            or ProviderErrorType.InsufficientBalance
            or ProviderErrorType.AccessForbidden;

    /// <summary>
    /// Returns true when the response details carry a billing/quota hint. Used to refine
    /// a 403 into <see cref="ProviderErrorType.InsufficientBalance"/>, and by callers that
    /// have no HTTP status at all (e.g. balance reprobes) as a last-resort signal.
    /// </summary>
    public static bool IsBalanceResponse(string? responseDetails)
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

    private static bool ContainsAny(string value, params string[] candidates)
        => candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}
