using Polly;
using Polly.Extensions.Http;
using Polly.Contrib.WaitAndRetry;
using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Defines resilience policies for HTTP requests made by LLM provider clients.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Creates a standard retry policy for HTTP requests to LLM provider APIs.
    /// Uses exponential backoff with jitter to handle transient failures.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelay">Initial delay before first retry (default: 1 second)</param>
    /// <param name="maxDelay">Maximum delay cap for any retry (default: 30 seconds)</param>
    /// <param name="logger">Optional logger for logging retry attempts</param>
    /// <returns>A configured Polly policy for HTTP requests</returns>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
        int maxRetries = 3, 
        TimeSpan? initialDelay = null, 
        TimeSpan? maxDelay = null,
        ILogger? logger = null)
    {
        // Default delays if not provided
        initialDelay ??= TimeSpan.FromSeconds(1);
        maxDelay ??= TimeSpan.FromSeconds(30);

        // Use decorrelated jitter backoff strategy for increased resilience and reduced potential for retry storms
        var delay = Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: initialDelay.Value, 
            retryCount: maxRetries,
            fastFirst: false); // No fast first retry

        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles 5xx status codes and connection failures
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // Handle 429 Too Many Requests
            .WaitAndRetryAsync(
                delay, 
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    // Log the retry attempt if a logger is provided
                    logger?.LogWarning(
                        "Retry {RetryAttempt} after {DelayMs}ms delay due to {StatusCode}. Error: {Error}",
                        retryAttempt,
                        timespan.TotalMilliseconds,
                        outcome.Result?.StatusCode,
                        outcome.Exception?.Message);
                })
            .WithPolicyKey("LLMProviderRetryPolicy"); // Assign a policy key for identification
    }
}
