using System;
using System.Net.Http;

using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Extensions.Http;

namespace ConduitLLM.Providers.Extensions;

/// <summary>
/// Extension methods for registering LLM provider HttpClient instances with resilience policies.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Adds HttpClient registration with timeout and retry policies for all LLM provider clients.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddLLMProviderHttpClients(this IServiceCollection services)
    {
        // Configure retry options from configuration
        services.AddOptions<RetryOptions>()
            .BindConfiguration(RetryOptions.SectionName);

        // Configure timeout options from configuration
        services.AddOptions<TimeoutOptions>()
            .BindConfiguration(TimeoutOptions.SectionName);

        // Register provider clients with timeout and retry policies
        services.AddHttpClient<OpenAIClient>()
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<OpenAIClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<OpenAIClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<AnthropicClient>()
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<AnthropicClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<AnthropicClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<CohereClient>()
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<CohereClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<CohereClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<GeminiClient>()
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<GeminiClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<GeminiClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<GroqClient>()
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<GroqClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<GroqClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<HuggingFaceClient>()
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<HuggingFaceClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<HuggingFaceClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<MistralClient>()
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<MistralClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<MistralClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<OpenRouterClient>()
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<OpenRouterClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<OpenRouterClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<VertexAIClient>()
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<VertexAIClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<VertexAIClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        // Register MiniMaxClient with standard timeout/retry policies for non-video operations
        // This will be overridden by VideoHttpClientExtensions for video generation
        services.AddHttpClient("minimaxLLMClient")
            // --- Outer Policy: Timeout ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetService<ILogger<MiniMaxClient>>();
                var timeoutOptions = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                    timeoutOptions.EnableTimeoutLogging ? logger : null);
            })
            // --- Inner Policy: Retry ---
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetService<ILogger<MiniMaxClient>>();
                var retryOptions = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });

        // Note: We're not registering BedrockClient and SageMakerClient here since they 
        // use the AWS SDK which has its own retry mechanism

        return services;
    }
}
