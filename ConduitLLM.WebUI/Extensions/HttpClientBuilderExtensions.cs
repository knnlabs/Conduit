using System;

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

                    // Use different policies based on HTTP method
                    if (request.Method == HttpMethod.Get)
                    {
                        // GET requests are generally safe to retry
                        return AdminApiResiliencePolicies.GetCombinedPolicy(
                            logger,
                            retryCount: options.RetryCount,
                            circuitBreakerThreshold: options.CircuitBreakerThreshold,
                            timeoutSeconds: options.TimeoutSeconds);
                    }
                    else if (request.Method == HttpMethod.Delete)
                    {
                        // DELETE requests might be idempotent, use fewer retries
                        return AdminApiResiliencePolicies.GetCombinedPolicy(
                            logger,
                            retryCount: Math.Min(options.RetryCount, 2),
                            circuitBreakerThreshold: options.CircuitBreakerThreshold,
                            timeoutSeconds: options.TimeoutSeconds);
                    }
                    else
                    {
                        // POST/PUT requests might not be idempotent, be more careful
                        return AdminApiResiliencePolicies.GetCombinedPolicy(
                            logger,
                            retryCount: 1,
                            circuitBreakerThreshold: options.CircuitBreakerThreshold,
                            timeoutSeconds: options.TimeoutSeconds);
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
}
