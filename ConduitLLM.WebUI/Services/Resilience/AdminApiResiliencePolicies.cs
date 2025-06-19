using System;
using System.Net;
using System.Net.Http;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace ConduitLLM.WebUI.Services.Resilience
{
    /// <summary>
    /// Provides resilience policies for Admin API HTTP calls.
    /// </summary>
    public static class AdminApiResiliencePolicies
    {
        /// <summary>
        /// Creates a retry policy for transient HTTP errors.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger, int retryCount = 3)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // Handles HttpRequestException and 5XX, 408 status codes
                .OrResult(msg => !msg.IsSuccessStatusCode && 
                                msg.StatusCode != HttpStatusCode.NotFound && 
                                msg.StatusCode != HttpStatusCode.Unauthorized && 
                                msg.StatusCode != HttpStatusCode.Forbidden)
                .WaitAndRetryAsync(
                    retryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff: 2, 4, 8 seconds
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var requestUri = outcome.Result?.RequestMessage?.RequestUri?.ToString() ?? "unknown";
                        if (outcome.Exception != null)
                        {
                            logger.LogWarning(
                                "Retry {RetryCount} after {Delay}ms for {RequestUri}. Exception: {Exception}",
                                retryCount, timespan.TotalMilliseconds, requestUri, outcome.Exception.Message);
                        }
                        else if (outcome.Result != null)
                        {
                            logger.LogWarning(
                                "Retry {RetryCount} after {Delay}ms for {RequestUri}. Status: {StatusCode}",
                                retryCount, timespan.TotalMilliseconds, requestUri, outcome.Result.StatusCode);
                        }
                    });
        }

        /// <summary>
        /// Creates a circuit breaker policy to prevent cascading failures.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger,
            int handledEventsAllowedBeforeBreaking = 5,
            TimeSpan? durationOfBreak = null)
        {
            durationOfBreak ??= TimeSpan.FromSeconds(30);

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode && 
                                msg.StatusCode != HttpStatusCode.NotFound && 
                                msg.StatusCode != HttpStatusCode.Unauthorized && 
                                msg.StatusCode != HttpStatusCode.Forbidden)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking,
                    durationOfBreak.Value,
                    onBreak: (result, duration) =>
                    {
                        logger.LogError(
                            "Circuit breaker opened for {Duration}s. Reason: {Reason}",
                            duration.TotalSeconds,
                            result.Exception?.Message ?? $"Status {result.Result?.StatusCode}");
                    },
                    onReset: () =>
                    {
                        logger.LogInformation("Circuit breaker reset. Admin API connection restored.");
                    },
                    onHalfOpen: () =>
                    {
                        logger.LogInformation("Circuit breaker is half-open. Testing Admin API connection.");
                    });
        }

        /// <summary>
        /// Creates a timeout policy to prevent hanging requests.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutSeconds = 30)
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(
                timeoutSeconds,
                TimeoutStrategy.Pessimistic); // Pessimistic = cancels HttpClient request
        }

        /// <summary>
        /// Creates a combined policy with retry, circuit breaker, and timeout.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(ILogger logger,
            int retryCount = 3,
            int circuitBreakerThreshold = 5,
            int timeoutSeconds = 30)
        {
            var timeout = GetTimeoutPolicy(timeoutSeconds);
            var retry = GetRetryPolicy(logger, retryCount);
            var circuitBreaker = GetCircuitBreakerPolicy(logger, circuitBreakerThreshold);

            // Order matters: Timeout (innermost) -> CircuitBreaker -> Retry (outermost)
            return Policy.WrapAsync(retry, circuitBreaker, timeout);
        }

        /// <summary>
        /// Creates a policy for non-critical operations with more lenient settings.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetNonCriticalOperationPolicy(ILogger logger)
        {
            return GetCombinedPolicy(logger,
                retryCount: 2,
                circuitBreakerThreshold: 10,
                timeoutSeconds: 60);
        }

        /// <summary>
        /// Creates a policy for critical operations with stricter settings.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetCriticalOperationPolicy(ILogger logger)
        {
            return GetCombinedPolicy(logger,
                retryCount: 5,
                circuitBreakerThreshold: 3,
                timeoutSeconds: 120);
        }

        /// <summary>
        /// Gets a fallback policy that returns a default response on failure.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy(
            HttpResponseMessage fallbackResponse,
            ILogger logger)
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .Or<BrokenCircuitException>()
                .FallbackAsync(
                    fallbackResponse,
                    onFallbackAsync: (result, context) =>
                    {
                        logger.LogWarning(
                            "Fallback policy activated. Original error: {Error}",
                            result.Exception?.Message ?? $"Status {result.Result?.StatusCode}");
                        return Task.CompletedTask;
                    });
        }
    }
}
