using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Providers.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Extension of ResiliencePolicies with error tracking capabilities
    /// </summary>
    public static partial class ResiliencePolicies
    {
        /// <summary>
        /// Creates a retry policy with integrated provider error tracking.
        /// Tracks errors during retries and automatically disables keys on fatal errors.
        /// Requires <see cref="IProviderErrorTrackingService"/> to be registered in
        /// <paramref name="serviceProvider"/>; throws at construction if it is not.
        /// </summary>
        /// <param name="serviceProvider">Service provider for resolving dependencies</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
        /// <param name="initialDelay">Initial delay before first retry (default: 1 second)</param>
        /// <param name="maxDelay">Maximum delay cap for any retry (default: 30 seconds)</param>
        /// <returns>A configured Polly policy with error tracking</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="IProviderErrorTrackingService"/> is not registered.
        /// </exception>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicyWithErrorTracking(
            IServiceProvider serviceProvider,
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null)
        {
            var logger = serviceProvider.GetService<ILogger<ILLMClient>>();
            // Caller (HttpClientExtensions) gates this method on the service being registered;
            // require it here so a misconfigured DI container fails fast instead of silently
            // dropping error tracking.
            var errorTracker = serviceProvider.GetRequiredService<IProviderErrorTrackingService>();
            // Optional: not all hosts of this library register IHttpContextAccessor (e.g.,
            // background workers). Correlation falls back to Activity.Current in that case.
            var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();

            // Use existing retry policy setup
            initialDelay ??= TimeSpan.FromSeconds(1);
            maxDelay ??= TimeSpan.FromSeconds(30);

            var delay = Polly.Contrib.WaitAndRetry.Backoff.DecorrelatedJitterBackoffV2(
                medianFirstRetryDelay: initialDelay.Value,
                retryCount: maxRetries,
                fastFirst: false);

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    delay,
                    onRetry: async (outcome, timespan, retryAttempt, context) =>
                    {
                        // Existing logging
                        logger?.LogWarning(
                            "Retry {RetryAttempt} after {DelayMs}ms delay due to {StatusCode}",
                            retryAttempt,
                            timespan.TotalMilliseconds,
                            outcome.Result?.StatusCode);

                        if (outcome.Result != null)
                        {
                            await TrackProviderErrorAsync(
                                outcome.Result,
                                outcome.Exception,
                                retryAttempt,
                                maxRetries,
                                errorTracker,
                                httpContextAccessor,
                                logger);
                        }
                    })
                .WithPolicyKey("LLMProviderRetryPolicyWithTracking");
        }
        
        /// <summary>
        /// Tracks provider errors during retry attempts
        /// </summary>
        private static async Task TrackProviderErrorAsync(
            HttpResponseMessage response,
            Exception? exception,
            int retryAttempt,
            int maxRetries,
            IProviderErrorTrackingService errorTracker,
            IHttpContextAccessor? httpContextAccessor,
            ILogger? logger)
        {
            try
            {
                // Get key context from ProviderKeyContext (set by ContextAwareLLMClient)
                var context = ProviderKeyContext.Current;

                if (context == null)
                {
                    // No key context, can't track error
                    return;
                }

                var keyId = context.KeyId;
                var providerId = context.ProviderId;

                // Classify the error
                var errorType = ClassifyResponseError(response);

                // Determine if we should track this error
                bool shouldTrack = false;
                string errorMessage = string.Empty;

                if (errorType == ProviderErrorType.RateLimitExceeded)
                {
                    // Always track rate limit warnings
                    shouldTrack = true;
                    errorMessage = "Rate limit exceeded";
                }
                else if (retryAttempt == maxRetries)
                {
                    // Track fatal errors only on final retry to avoid duplicates
                    shouldTrack = IsFatalError(errorType);
                    errorMessage = await ExtractErrorMessageFromResponse(response);
                }

                if (shouldTrack)
                {
                    await errorTracker.TrackErrorAsync(new ProviderErrorInfo
                    {
                        KeyCredentialId = keyId,
                        ProviderId = providerId,
                        ErrorType = errorType,
                        ErrorMessage = errorMessage,
                        HttpStatusCode = (int)response.StatusCode,
                        RetryAttempt = retryAttempt,
                        RequestId = ResolveCorrelationId(httpContextAccessor)
                    });

                    logger?.LogInformation(
                        "Tracked {ErrorType} error for key {KeyId} on retry {RetryAttempt}/{MaxRetries}",
                        errorType, keyId, retryAttempt, maxRetries);
                }
            }
            catch (Exception ex)
            {
                // Don't let error tracking break the retry flow
                logger?.LogError(ex, "Failed to track provider error during retry");
            }
        }

        /// <summary>
        /// Resolves the correlation/request ID for the current call.
        /// Order of precedence matches <see cref="CorrelationContextService"/>:
        ///   1. <c>HttpContext.Items["CorrelationId"]</c> (set by CorrelationIdMiddleware)
        ///   2. <c>HttpContext.TraceIdentifier</c>
        ///   3. <c>Activity.Current</c> baggage item "correlation.id"
        ///   4. <c>Activity.Current.TraceId</c> (W3C trace ID — works in background jobs)
        /// Returns null only when none of the above are populated.
        /// </summary>
        private static string? ResolveCorrelationId(IHttpContextAccessor? httpContextAccessor)
        {
            var httpContext = httpContextAccessor?.HttpContext;
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
        
        /// <summary>
        /// Classifies HTTP response into error type
        /// </summary>
        private static ProviderErrorType ClassifyResponseError(HttpResponseMessage response)
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
        /// Determines if an error type is fatal (should disable key)
        /// </summary>
        private static bool IsFatalError(ProviderErrorType errorType)
        {
            return (int)errorType <= 9; // Fatal errors are 1-9
        }
        
        /// <summary>
        /// Extracts error message from HTTP response
        /// </summary>
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
    }
}