using Microsoft.Extensions.Logging;
using Polly;
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
    public static IAsyncPolicy<HttpResponseMessage> GetStandardRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000))
            );
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
