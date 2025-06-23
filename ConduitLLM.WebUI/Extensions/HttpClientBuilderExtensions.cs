using System;

using ConduitLLM.Core.Configuration;
using ConduitLLM.WebUI.Services.Resilience;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Polly;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for configuring HTTP clients with resilience policies.
    /// </summary>
    public static class HttpClientBuilderExtensions
    {
        /// <summary>
        /// Adds resilience policies to an HTTP client.
        /// </summary>
        public static IHttpClientBuilder AddResiliencePolicies(
            this IHttpClientBuilder builder,
            Action<ResiliencePolicyOptions>? configure = null)
        {
            var options = new ResiliencePolicyOptions();
            configure?.Invoke(options);

            return builder
                .AddPolicyHandler((services, request) =>
                {
                    var logger = services.GetRequiredService<ILogger<IHttpClientBuilder>>();
                    var timeoutProvider = services.GetService<IOperationTimeoutProvider>();

                    // Determine operation type from request URI
                    var requestUri = request.RequestUri?.ToString() ?? "";
                    var requestPath = request.RequestUri?.AbsolutePath ?? "";
                    
                    string operationType = HttpClientBuilderExtensionsHelpers.DetermineOperationType(requestPath);
                    
                    // Check if this is a video generation endpoint - these should NOT have timeouts
                    if (requestUri.Contains("/videos/generations", StringComparison.OrdinalIgnoreCase) ||
                        requestPath.Contains("/videos/generations", StringComparison.OrdinalIgnoreCase) ||
                        requestUri.Contains("videos/generations", StringComparison.OrdinalIgnoreCase))
                    {
                        // Video generation can take 3-5+ minutes, so only apply retry policy
                        logger.LogWarning("Skipping timeout policy for video generation endpoint: URI={Uri}, Path={Path}", 
                            requestUri, requestPath);
                        return AdminApiResiliencePolicies.GetRetryPolicy(logger, retryCount: 3);
                    }

                    // Get timeout from operation-aware provider if available
                    int timeoutSeconds = options.TimeoutSeconds;
                    if (timeoutProvider != null && !string.IsNullOrEmpty(operationType))
                    {
                        var operationTimeout = timeoutProvider.GetTimeoutOrDefault(operationType, TimeSpan.FromSeconds(options.TimeoutSeconds));
                        timeoutSeconds = (int)operationTimeout.TotalSeconds;
                        logger.LogInformation("Using operation-aware timeout for {OperationType}: {TimeoutSeconds}s", operationType, timeoutSeconds);
                    }

                    // Use different policies based on HTTP method
                    if (request.Method == HttpMethod.Get)
                    {
                        // GET requests are generally safe to retry
                        return AdminApiResiliencePolicies.GetCombinedPolicy(
                            logger,
                            retryCount: options.RetryCount,
                            circuitBreakerThreshold: options.CircuitBreakerThreshold,
                            timeoutSeconds: timeoutSeconds);
                    }
                    else if (request.Method == HttpMethod.Delete)
                    {
                        // DELETE requests might be idempotent, use fewer retries
                        return AdminApiResiliencePolicies.GetCombinedPolicy(
                            logger,
                            retryCount: Math.Min(options.RetryCount, 2),
                            circuitBreakerThreshold: options.CircuitBreakerThreshold,
                            timeoutSeconds: timeoutSeconds);
                    }
                    else
                    {
                        // POST/PUT requests might not be idempotent, be more careful
                        return AdminApiResiliencePolicies.GetCombinedPolicy(
                            logger,
                            retryCount: 1,
                            circuitBreakerThreshold: options.CircuitBreakerThreshold,
                            timeoutSeconds: timeoutSeconds);
                    }
                });
        }

        /// <summary>
        /// Adds resilience policies specifically for Admin API operations.
        /// </summary>
        public static IHttpClientBuilder AddAdminApiResiliencePolicies(this IHttpClientBuilder builder)
        {
            return builder
                .AddResiliencePolicies(options =>
                {
                    options.RetryCount = 3;
                    options.CircuitBreakerThreshold = 5;
                    options.TimeoutSeconds = 30;
                })
                .ConfigureHttpClient(client =>
                {
                    // Set a default timeout on the HttpClient itself as a safety net
                    client.Timeout = TimeSpan.FromSeconds(60);
                });
        }

        /// <summary>
        /// Adds resilience policies for critical operations.
        /// </summary>
        public static IHttpClientBuilder AddCriticalOperationPolicies(this IHttpClientBuilder builder)
        {
            return builder
                .AddResiliencePolicies(options =>
                {
                    options.RetryCount = 5;
                    options.CircuitBreakerThreshold = 3;
                    options.TimeoutSeconds = 120;
                });
        }

        /// <summary>
        /// Adds resilience policies for non-critical operations.
        /// </summary>
        public static IHttpClientBuilder AddNonCriticalOperationPolicies(this IHttpClientBuilder builder)
        {
            return builder
                .AddResiliencePolicies(options =>
                {
                    options.RetryCount = 2;
                    options.CircuitBreakerThreshold = 10;
                    options.TimeoutSeconds = 60;
                });
        }
    }

    /// <summary>
    /// Options for configuring resilience policies.
    /// </summary>
    public class ResiliencePolicyOptions
    {
        /// <summary>
        /// Number of retry attempts for transient failures.
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Number of failures before opening the circuit breaker.
        /// </summary>
        public int CircuitBreakerThreshold { get; set; } = 5;

        /// <summary>
        /// Timeout in seconds for each request.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Duration in seconds to keep the circuit breaker open.
        /// </summary>
        public int CircuitBreakerDurationSeconds { get; set; } = 30;
    }
    
    /// <summary>
    /// Extension methods for HttpClientBuilderExtensions internal use.
    /// </summary>
    internal static class HttpClientBuilderExtensionsHelpers
    {
        /// <summary>
        /// Determines the operation type from the request path.
        /// </summary>
        /// <param name="requestPath">The request URI path.</param>
        /// <returns>The operation type string.</returns>
        public static string DetermineOperationType(string requestPath)
        {
            if (string.IsNullOrEmpty(requestPath))
                return OperationTypes.Completion;

            var path = requestPath.ToLowerInvariant();

            if (path.Contains("/chat/completions"))
                return OperationTypes.Chat;
            else if (path.Contains("/images/generations"))
                return OperationTypes.ImageGeneration;
            else if (path.Contains("/videos/generations"))
                return OperationTypes.VideoGeneration;
            else if (path.Contains("/health") || path.Contains("/healthz"))
                return OperationTypes.HealthCheck;
            else if (path.Contains("/models"))
                return OperationTypes.ModelDiscovery;
            else if (path.Contains("/completions"))
                return OperationTypes.Completion;
            else
                return OperationTypes.Completion; // Default
        }
    }
}
