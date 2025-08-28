using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace ConduitLLM.Providers;

/// <summary>
/// Defines resilience policies for HTTP requests made by LLM provider clients.
/// </summary>
public static partial class ResiliencePolicies
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

    /// <summary>
    /// Creates a timeout policy for HTTP requests to LLM provider APIs.
    /// Uses a pessimistic timeout strategy to ensure strict timeout enforcement.
    /// </summary>
    /// <param name="timeout">Timeout duration (default: 100 seconds)</param>
    /// <param name="logger">Optional logger for logging timeout events</param>
    /// <returns>A configured Polly timeout policy for HTTP requests</returns>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(
        TimeSpan? timeout = null,
        ILogger? logger = null)
    {
        // Default timeout if not provided
        timeout ??= TimeSpan.FromSeconds(100);

        // Pessimistic strategy ensures the timeout is enforced strictly
        return Policy.TimeoutAsync<HttpResponseMessage>(
            timeout.Value,
            TimeoutStrategy.Pessimistic,
            onTimeoutAsync: (context, timespan, task, exception) =>
            {
                // Log the timeout event if a logger is provided
                logger?.LogWarning(
                    "HTTP request timed out after {TimeoutMs}ms. Request: {Operation}",
                    timespan.TotalMilliseconds,
                    context.OperationKey);

                return Task.CompletedTask;
            })
            .WithPolicyKey("LLMProviderTimeoutPolicy"); // Name for identification
    }

    /// <summary>
    /// Creates an operation-aware timeout policy for HTTP requests to LLM provider APIs.
    /// Uses the IOperationTimeoutProvider to determine appropriate timeout for the operation type.
    /// </summary>
    /// <param name="operationType">The type of operation being performed</param>
    /// <param name="timeoutProvider">The timeout provider for operation-specific timeouts</param>
    /// <param name="logger">Optional logger for logging timeout events</param>
    /// <returns>A configured Polly timeout policy for HTTP requests</returns>
    public static IAsyncPolicy<HttpResponseMessage> GetOperationTimeoutPolicy(
        string operationType,
        IOperationTimeoutProvider timeoutProvider,
        ILogger? logger = null)
    {
        if (timeoutProvider == null)
        {
            throw new ArgumentNullException(nameof(timeoutProvider));
        }

        // Check if timeout should be applied for this operation
        if (!timeoutProvider.ShouldApplyTimeout(operationType))
        {
            logger?.LogInformation("No timeout policy applied for operation type: {OperationType}", operationType);
            return Policy.NoOpAsync<HttpResponseMessage>();
        }

        var timeout = timeoutProvider.GetTimeout(operationType);
        
        logger?.LogInformation(
            "Applying timeout policy for operation '{OperationType}': {TimeoutSeconds} seconds", 
            operationType, 
            timeout.TotalSeconds);

        // Pessimistic strategy ensures the timeout is enforced strictly
        return Policy.TimeoutAsync<HttpResponseMessage>(
            timeout,
            TimeoutStrategy.Pessimistic,
            onTimeoutAsync: (context, timespan, task, exception) =>
            {
                // Log the timeout event if a logger is provided
                logger?.LogWarning(
                    "HTTP request timed out after {TimeoutMs}ms. Operation: {OperationType}, Context: {OperationKey}",
                    timespan.TotalMilliseconds,
                    operationType,
                    context.OperationKey);

                return Task.CompletedTask;
            })
            .WithPolicyKey($"LLMProviderTimeoutPolicy-{operationType}"); // Name for identification
    }

    /// <summary>
    /// Creates a no-timeout policy for long-running operations like video generation.
    /// This policy effectively disables timeout for operations that can take many minutes.
    /// </summary>
    /// <param name="logger">Optional logger for logging</param>
    /// <returns>A pass-through policy that doesn't enforce any timeout</returns>
    public static IAsyncPolicy<HttpResponseMessage> GetNoTimeoutPolicy(ILogger? logger = null)
    {
        logger?.LogInformation("Using no-timeout policy for long-running operation");
        
        // Return a no-op policy that doesn't enforce any timeout
        return Policy.NoOpAsync<HttpResponseMessage>();
    }
}
