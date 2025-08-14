using System;
using System.Net.Http;

using ConduitLLM.Providers.Configuration;
using ConduitLLM.Providers.OpenAI;
using ConduitLLM.Providers.Groq;
using ConduitLLM.Providers.Replicate;
using ConduitLLM.Providers.Fireworks;
using ConduitLLM.Providers.OpenAICompatible;
using ConduitLLM.Providers.MiniMax;
using ConduitLLM.Providers.Ultravox;
using ConduitLLM.Providers.ElevenLabs;
using ConduitLLM.Providers.Cerebras;

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

        // Note: Replicate, Fireworks, OpenAICompatible, Ultravox, ElevenLabs, and Cerebras
        // clients will be registered here when their HttpClient implementations are available

        return services;
    }
}
