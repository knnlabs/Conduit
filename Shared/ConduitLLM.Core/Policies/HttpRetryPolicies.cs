using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;

namespace ConduitLLM.Core.Policies;

/// <summary>
/// Shared HTTP retry policies for use across the solution.
/// Centralizes retry logic to avoid duplication and ensure consistent behavior.
/// </summary>
public static class HttpRetryPolicies
{
    /// <summary>
    /// Standard retry policy for HTTP requests with exponential backoff and jitter.
    /// Handles transient HTTP errors (5xx, connection failures) and 429 Too Many Requests.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetStandardRetryPolicy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null,
        ILogger? logger = null,
        string policyKey = "HttpRetryPolicy")
    {
        return HandleTransientHttpErrors()
            .WaitAndRetryAsync(
                GetDecorrelatedJitterDelays(maxRetries, initialDelay, maxDelay),
                onRetry: (outcome, timespan, retryAttempt, _) =>
                {
                    logger?.LogWarning(
                        "Retry {RetryAttempt} after {DelayMs}ms delay due to {StatusCode}. Error: {Error}",
                        retryAttempt,
                        timespan.TotalMilliseconds,
                        outcome.Result?.StatusCode,
                        outcome.Exception?.Message);
                })
            .WithPolicyKey(policyKey);
    }

    /// <summary>Shared transient HTTP handle set used by policies that add custom retry callbacks.</summary>
    public static PolicyBuilder<HttpResponseMessage> HandleTransientHttpErrors() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(message => message.StatusCode == System.Net.HttpStatusCode.TooManyRequests);

    /// <summary>Creates capped decorrelated-jitter delays to avoid synchronized retry storms.</summary>
    public static IEnumerable<TimeSpan> GetDecorrelatedJitterDelays(
        int retryCount,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null)
    {
        var firstDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        var delayCap = maxDelay ?? TimeSpan.FromSeconds(30);

        return Backoff.DecorrelatedJitterBackoffV2(firstDelay, retryCount)
            .Select(delay => delay > delayCap ? delayCap : delay);
    }

    /// <summary>
    /// Retry policy for media downloads with exponential backoff and optional logging.
    /// </summary>
    /// <param name="backoffBase">Base for exponential backoff (e.g., 2 for images, 3 for videos).</param>
    /// <param name="mediaType">Label for log messages (e.g., "Image", "Video").</param>
    public static IAsyncPolicy<HttpResponseMessage> GetMediaDownloadRetryPolicy(
        int backoffBase = 2,
        string mediaType = "Media")
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(backoffBase, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.Values.FirstOrDefault() as Microsoft.Extensions.Logging.ILogger;
                    logger?.LogWarning("{MediaType} download retry {RetryCount} after {Delay}ms",
                        mediaType, retryCount, timespan.TotalMilliseconds);
                });
    }
}
