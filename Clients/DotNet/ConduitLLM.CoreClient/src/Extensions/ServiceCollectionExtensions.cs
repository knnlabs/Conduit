using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using ConduitLLM.CoreClient.Client;
using System.ComponentModel.DataAnnotations;
using Polly;

namespace ConduitLLM.CoreClient.Extensions;

/// <summary>
/// Extension methods for configuring ConduitLLM Core Client services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ConduitLLM Core Client services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing core client settings.</param>
    /// <param name="configurationSectionName">The configuration section name (default: "ConduitCore").</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ValidationException">Thrown when configuration is invalid.</exception>
    public static IServiceCollection AddConduitCoreClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string configurationSectionName = "ConduitCore")
    {
        // Bind configuration
        var configSection = configuration.GetSection(configurationSectionName);
        var clientConfig = new ConduitCoreClientConfiguration();
        configSection.Bind(clientConfig);

        return services.AddConduitCoreClient(clientConfig);
    }

    /// <summary>
    /// Adds ConduitLLM Core Client services to the service collection with explicit configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The core client configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ValidationException">Thrown when configuration is invalid.</exception>
    public static IServiceCollection AddConduitCoreClient(
        this IServiceCollection services,
        ConduitCoreClientConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        // Validate configuration
        ValidateConfiguration(configuration);

        // Register configuration as singleton
        services.AddSingleton(configuration);

        // Configure HTTP client with retry policies
        services.AddHttpClient<ConduitCoreClient>(client =>
        {
            client.BaseAddress = new Uri(configuration.BaseUrl.TrimEnd('/'));
            client.Timeout = TimeSpan.FromSeconds(configuration.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", configuration.ApiKey);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ConduitLLM.CoreClient/1.0.0");

            // Add organization header if specified
            if (!string.IsNullOrEmpty(configuration.OrganizationId))
            {
                client.DefaultRequestHeaders.Add("OpenAI-Organization", configuration.OrganizationId);
            }

            // Add custom headers
            foreach (var header in configuration.DefaultHeaders)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        })
        .AddPolicyHandler(GetRetryPolicy(configuration))
        .AddPolicyHandler(GetTimeoutPolicy(configuration));

        // Register the core client
        services.AddScoped<ConduitCoreClient>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(ConduitCoreClient));
            var logger = serviceProvider.GetService<ILogger<ConduitCoreClient>>();

            return new ConduitCoreClient(configuration, httpClient, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds ConduitLLM Core Client services with configuration from environment variables.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are missing.</exception>
    public static IServiceCollection AddConduitCoreClientFromEnvironment(this IServiceCollection services)
    {
        var configuration = ConduitCoreClientConfiguration.FromEnvironment();
        return services.AddConduitCoreClient(configuration);
    }

    /// <summary>
    /// Adds ConduitLLM Core Client services with API key and base URL.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="baseUrl">Optional base URL override.</param>
    /// <param name="configureOptions">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when apiKey is invalid.</exception>
    public static IServiceCollection AddConduitCoreClient(
        this IServiceCollection services,
        string apiKey,
        string? baseUrl = null,
        Action<ConduitCoreClientConfiguration>? configureOptions = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        var configuration = ConduitCoreClientConfiguration.FromApiKey(apiKey, baseUrl);
        configureOptions?.Invoke(configuration);

        return services.AddConduitCoreClient(configuration);
    }

    /// <summary>
    /// Adds ConduitLLM Core Client services with configuration action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConduitCoreClient(
        this IServiceCollection services,
        Action<ConduitCoreClientConfiguration> configureOptions)
    {
        var configuration = new ConduitCoreClientConfiguration();
        configureOptions(configuration);

        return services.AddConduitCoreClient(configuration);
    }

    private static void ValidateConfiguration(ConduitCoreClientConfiguration configuration)
    {
        var context = new ValidationContext(configuration);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(configuration, context, results, true))
        {
            var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
            throw new ValidationException($"Invalid core client configuration: {errors}");
        }
    }

    private static Polly.IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ConduitCoreClientConfiguration configuration)
    {
        return Polly.Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && IsRetriableStatusCode(r.StatusCode))
            .RetryAsync(configuration.MaxRetries);
    }

    private static Polly.IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(ConduitCoreClientConfiguration configuration)
    {
        return Polly.Policy.TimeoutAsync<HttpResponseMessage>(
            timeout: TimeSpan.FromSeconds(configuration.TimeoutSeconds));
    }

    private static bool IsRetriableStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.RequestTimeout => true,
            System.Net.HttpStatusCode.TooManyRequests => true,
            System.Net.HttpStatusCode.InternalServerError => true,
            System.Net.HttpStatusCode.BadGateway => true,
            System.Net.HttpStatusCode.ServiceUnavailable => true,
            System.Net.HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }
}

/// <summary>
/// Extension methods for configuring ConduitLLM Core Client with options pattern.
/// </summary>
public static class ConduitCoreClientOptionsExtensions
{
    /// <summary>
    /// Configures ConduitLLM Core Client options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="configurationSectionName">The configuration section name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureConduitCoreClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string configurationSectionName = "ConduitCore")
    {
        services.Configure<ConduitCoreClientConfiguration>(
            configuration.GetSection(configurationSectionName));

        return services;
    }

    /// <summary>
    /// Configures ConduitLLM Core Client options with an action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">The configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureConduitCoreClient(
        this IServiceCollection services,
        Action<ConduitCoreClientConfiguration> configureOptions)
    {
        services.Configure(configureOptions);
        return services;
    }
}