using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ConduitLLM.Providers.Configuration;

namespace ConduitLLM.Providers.Extensions;

/// <summary>
/// Extension methods for registering LLM provider HttpClient instances with resilience policies.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Adds HttpClient registration with retry policies for all LLM provider clients.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddLLMProviderHttpClients(this IServiceCollection services)
    {
        // Configure retry options from configuration
        services.AddOptions<RetryOptions>()
            .BindConfiguration(RetryOptions.SectionName);

        // Register provider clients with retry policies
        services.AddHttpClient<OpenAIClient>()
            .AddPolicyHandler((provider, _) => {
                var logger = provider.GetRequiredService<ILogger<OpenAIClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    options.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<AnthropicClient>()
            .AddPolicyHandler((provider, _) => {
                var logger = provider.GetRequiredService<ILogger<AnthropicClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    options.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<CohereClient>()
            .AddPolicyHandler((provider, _) => {
                var logger = provider.GetRequiredService<ILogger<CohereClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    options.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<GeminiClient>()
            .AddPolicyHandler((provider, _) => {
                var logger = provider.GetRequiredService<ILogger<GeminiClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    options.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<GroqClient>()
            .AddPolicyHandler((provider, _) => {
                var logger = provider.GetRequiredService<ILogger<GroqClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    options.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<HuggingFaceClient>()
            .AddPolicyHandler((provider, _) => {
                var logger = provider.GetRequiredService<ILogger<HuggingFaceClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    options.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<MistralClient>()
            .AddPolicyHandler((provider, _) => {
                var logger = provider.GetRequiredService<ILogger<MistralClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    options.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<OpenRouterClient>()
            .AddPolicyHandler((provider, _) => {
                var logger = provider.GetRequiredService<ILogger<OpenRouterClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    options.EnableRetryLogging ? logger : null);
            });

        services.AddHttpClient<VertexAIClient>()
            .AddPolicyHandler((provider, _) => {
                var logger = provider.GetRequiredService<ILogger<VertexAIClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    options.EnableRetryLogging ? logger : null);
            });

        // Note: We're not registering BedrockClient and SageMakerClient here since they 
        // use the AWS SDK which has its own retry mechanism

        return services;
    }
}
