using System.Diagnostics;
using System.Net;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Http;

/// <summary>
/// Bridges retry-pipeline outcomes into <see cref="IProviderErrorTrackingService"/> so that
/// failing provider keys are attributed and auto-disabled. Ported from the legacy
/// <c>ResiliencePolicies.ErrorTracking</c> with identical semantics:
/// rate-limit (429) errors are tracked on every retry; fatal-class errors are tracked only on
/// the final retry attempt (to avoid duplicate records); key/provider attribution comes from
/// <see cref="ProviderKeyContext"/> (AsyncLocal, set by ContextAwareLLMClient).
/// </summary>
public sealed class ProviderErrorTrackingRetryHook
{
    private readonly IProviderErrorTrackingService _errorTracker;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger? _logger;

    public ProviderErrorTrackingRetryHook(
        IProviderErrorTrackingService errorTracker,
        IHttpContextAccessor? httpContextAccessor,
        ILogger? logger)
    {
        _errorTracker = errorTracker ?? throw new ArgumentNullException(nameof(errorTracker));
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Records a failed attempt that is about to be retried.
    /// </summary>
    /// <param name="response">The failed response (null for exception outcomes, which are not tracked).</param>
    /// <param name="retryAttempt">1-based retry attempt number about to run.</param>
    /// <param name="maxRetryAttempts">Configured maximum retries.</param>
    public async ValueTask OnRetryAsync(HttpResponseMessage? response, int retryAttempt, int maxRetryAttempts)
    {
        if (response == null)
        {
            return;
        }

        try
        {
            // Get key context from ProviderKeyContext (set by ContextAwareLLMClient)
            var context = ProviderKeyContext.Current;
            if (context == null)
            {
                return;
            }

            var errorType = ClassifyResponseError(response);

            bool shouldTrack = false;
            string errorMessage = string.Empty;

            if (errorType == ProviderErrorType.RateLimitExceeded)
            {
                // Always track rate limit warnings
                shouldTrack = true;
                errorMessage = "Rate limit exceeded";
            }
            else if (retryAttempt == maxRetryAttempts)
            {
                // Track fatal errors only on final retry to avoid duplicates
                shouldTrack = IsFatalError(errorType);
                errorMessage = await ExtractErrorMessageFromResponse(response);
            }

            if (shouldTrack)
            {
                await _errorTracker.TrackErrorAsync(new ProviderErrorInfo
                {
                    KeyCredentialId = context.KeyId,
                    ProviderId = context.ProviderId,
                    ErrorType = errorType,
                    ErrorMessage = errorMessage,
                    HttpStatusCode = (int)response.StatusCode,
                    RetryAttempt = retryAttempt,
                    RequestId = ResolveCorrelationId(),
                });

                _logger?.LogInformation(
                    "Tracked {ErrorType} error for key {KeyId} on retry {RetryAttempt}/{MaxRetries}",
                    errorType, context.KeyId, retryAttempt, maxRetryAttempts);
            }
        }
        catch (Exception ex)
        {
            // Don't let error tracking break the retry flow
            _logger?.LogError(ex, "Failed to track provider error during retry");
        }
    }

    /// <summary>
    /// Classifies HTTP response into error type.
    /// </summary>
    internal static ProviderErrorType ClassifyResponseError(HttpResponseMessage response)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ProviderErrorType.InvalidApiKey,
            HttpStatusCode.PaymentRequired => ProviderErrorType.InsufficientBalance,
            HttpStatusCode.Forbidden => ProviderErrorType.AccessForbidden,
            HttpStatusCode.TooManyRequests => ProviderErrorType.RateLimitExceeded,
            HttpStatusCode.NotFound => ProviderErrorType.ModelNotFound,
            HttpStatusCode.ServiceUnavailable => ProviderErrorType.ServiceUnavailable,
            HttpStatusCode.BadGateway => ProviderErrorType.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout => ProviderErrorType.Timeout,
            _ => ProviderErrorType.Unknown
        };
    }

    /// <summary>
    /// Determines if an error type is fatal (should disable key).
    /// </summary>
    internal static bool IsFatalError(ProviderErrorType errorType)
    {
        return (int)errorType <= 9; // Fatal errors are 1-9
    }

    private static async Task<string> ExtractErrorMessageFromResponse(HttpResponseMessage response)
    {
        try
        {
            if (response.Content != null)
            {
                var content = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content.Length > 500
                        ? content.Substring(0, 500) + "..."
                        : content;
                }
            }
        }
        catch
        {
            // Ignore content read errors
        }

        return $"{response.StatusCode}: {response.ReasonPhrase ?? "Unknown error"}";
    }

    /// <summary>
    /// Resolves the correlation/request ID for the current call. Order of precedence matches
    /// <c>CorrelationContextService</c>: HttpContext.Items["CorrelationId"], then
    /// HttpContext.TraceIdentifier, then Activity baggage "correlation.id", then Activity TraceId.
    /// </summary>
    private string? ResolveCorrelationId()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            if (httpContext.Items.TryGetValue("CorrelationId", out var corr) &&
                corr is string s && !string.IsNullOrEmpty(s))
            {
                return s;
            }

            if (!string.IsNullOrEmpty(httpContext.TraceIdentifier))
            {
                return httpContext.TraceIdentifier;
            }
        }

        var activity = Activity.Current;
        if (activity != null)
        {
            var baggage = activity.GetBaggageItem("correlation.id");
            if (!string.IsNullOrEmpty(baggage))
            {
                return baggage;
            }

            if (activity.TraceId != default)
            {
                return activity.TraceId.ToString();
            }
        }

        return null;
    }
}
