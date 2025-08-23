using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Providers.Extensions;

/// <summary>
/// Extension methods for configuring HttpClient with operation-aware timeout policies.
/// </summary>
public static class OperationAwareHttpClientExtensions
{
    /// <summary>
    /// Adds operation-aware timeout and retry policies to an HttpClient.
    /// </summary>
    /// <typeparam name="TClient">The type of the HttpClient service</typeparam>
    /// <param name="builder">The HttpClient builder</param>
    /// <param name="defaultOperationType">Default operation type if not specified in request</param>
    /// <returns>The HttpClient builder for chaining</returns>
    public static IHttpClientBuilder AddOperationAwarePolicies<TClient>(
        this IHttpClientBuilder builder,
        string defaultOperationType = OperationTypes.Completion)
        where TClient : class
    {
        return builder
            // --- Outer Policy: Operation-Aware Timeout ---
            .AddPolicyHandler((provider, request) =>
            {
                var logger = provider.GetService<ILogger<TClient>>();
                var timeoutProvider = provider.GetService<IOperationTimeoutProvider>();
                
                // Try to get operation type from request options
                var operationType = defaultOperationType;
                if (request.Options.TryGetValue(new HttpRequestOptionsKey<string>("OperationType"), out var opTypeString))
                {
                    operationType = opTypeString;
                }

                // If timeout provider is available, use operation-aware policy
                if (timeoutProvider != null)
                {
                    return ResiliencePolicies.GetOperationTimeoutPolicy(operationType, timeoutProvider, logger);
                }
                
                // Fallback to traditional timeout policy
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetService<ILogger<TClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });
    }

    /// <summary>
    /// Creates an HttpRequestMessage with the specified operation type.
    /// </summary>
    /// <param name="method">The HTTP method</param>
    /// <param name="requestUri">The request URI</param>
    /// <param name="operationType">The operation type for timeout configuration</param>
    /// <returns>An HttpRequestMessage configured with the operation type</returns>
    public static HttpRequestMessage CreateOperationRequest(
        HttpMethod method,
        string requestUri,
        string operationType)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Options.Set(new HttpRequestOptionsKey<string>("OperationType"), operationType);
        return request;
    }

    /// <summary>
    /// Creates an HttpRequestMessage with the specified operation type.
    /// </summary>
    /// <param name="method">The HTTP method</param>
    /// <param name="requestUri">The request URI</param>
    /// <param name="operationType">The operation type for timeout configuration</param>
    /// <returns>An HttpRequestMessage configured with the operation type</returns>
    public static HttpRequestMessage CreateOperationRequest(
        HttpMethod method,
        Uri requestUri,
        string operationType)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Options.Set(new HttpRequestOptionsKey<string>("OperationType"), operationType);
        return request;
    }
}