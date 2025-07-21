using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http;
using ConduitLLM.AdminClient.Client;
using System.ComponentModel.DataAnnotations;
using Polly;

namespace ConduitLLM.AdminClient.Extensions;

/// <summary>
/// Extension methods for configuring ConduitLLM Admin Client services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ConduitLLM Admin Client services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing admin client settings.</param>
    /// <param name="configurationSectionName">The configuration section name (default: "ConduitAdmin").</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ValidationException">Thrown when configuration is invalid.</exception>
    public static IServiceCollection AddConduitAdminClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string configurationSectionName = "ConduitAdmin")
    {
        // Bind configuration
        var configSection = configuration.GetSection(configurationSectionName);
        var clientConfig = new ConduitAdminClientConfiguration();
        configSection.Bind(clientConfig);

        return services.AddConduitAdminClient(clientConfig);
    }

    /// <summary>
    /// Adds ConduitLLM Admin Client services to the service collection with explicit configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The admin client configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ValidationException">Thrown when configuration is invalid.</exception>
    public static IServiceCollection AddConduitAdminClient(
        this IServiceCollection services,
        ConduitAdminClientConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        // Validate configuration
        ValidateConfiguration(configuration);

        // Register configuration as singleton
        services.AddSingleton(configuration);

        // Add memory cache if not already registered
        services.TryAddSingleton<IMemoryCache, MemoryCache>();

        // Configure HTTP client with retry policies
        services.AddHttpClient<ConduitAdminClient>(client =>
        {
            client.BaseAddress = new Uri(ConduitAdminClientConfiguration.NormalizeApiUrl(configuration.AdminApiUrl));
            client.Timeout = TimeSpan.FromSeconds(configuration.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("X-Master-Key", configuration.MasterKey);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ConduitLLM.AdminClient/1.0.0");

            // Add custom headers
            foreach (var header in configuration.DefaultHeaders)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        })
        .AddPolicyHandler(GetRetryPolicy(configuration))
        .AddPolicyHandler(GetTimeoutPolicy(configuration));

        // Register the admin client
        services.AddScoped<ConduitAdminClient>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(ConduitAdminClient));
            var logger = serviceProvider.GetService<ILogger<ConduitAdminClient>>();
            var cache = serviceProvider.GetService<IMemoryCache>();

            return new ConduitAdminClient(configuration, httpClient, logger, cache);
        });

        return services;
    }

    /// <summary>
    /// Adds ConduitLLM Admin Client services with configuration from environment variables.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are missing.</exception>
    public static IServiceCollection AddConduitAdminClientFromEnvironment(this IServiceCollection services)
    {
        var configuration = ConduitAdminClientConfiguration.FromEnvironment();
        return services.AddConduitAdminClient(configuration);
    }

    /// <summary>
    /// Adds ConduitLLM Admin Client services with master key and URL.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="masterKey">The master key for authentication.</param>
    /// <param name="adminApiUrl">The admin API base URL.</param>
    /// <param name="configureOptions">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when masterKey or adminApiUrl is invalid.</exception>
    public static IServiceCollection AddConduitAdminClient(
        this IServiceCollection services,
        string masterKey,
        string adminApiUrl,
        Action<ConduitAdminClientConfiguration>? configureOptions = null)
    {
        if (string.IsNullOrWhiteSpace(masterKey))
            throw new ArgumentException("Master key cannot be null or empty", nameof(masterKey));

        if (string.IsNullOrWhiteSpace(adminApiUrl))
            throw new ArgumentException("Admin API URL cannot be null or empty", nameof(adminApiUrl));

        var configuration = new ConduitAdminClientConfiguration
        {
            MasterKey = masterKey,
            AdminApiUrl = adminApiUrl
        };

        configureOptions?.Invoke(configuration);

        return services.AddConduitAdminClient(configuration);
    }

    /// <summary>
    /// Adds ConduitLLM Admin Client services with configuration action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConduitAdminClient(
        this IServiceCollection services,
        Action<ConduitAdminClientConfiguration> configureOptions)
    {
        var configuration = new ConduitAdminClientConfiguration();
        configureOptions(configuration);

        return services.AddConduitAdminClient(configuration);
    }

    private static void ValidateConfiguration(ConduitAdminClientConfiguration configuration)
    {
        var context = new ValidationContext(configuration);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(configuration, context, results, true))
        {
            var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
            throw new ValidationException($"Invalid admin client configuration: {errors}");
        }
    }

    private static Polly.IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ConduitAdminClientConfiguration configuration)
    {
        return Polly.Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && IsRetriableStatusCode(r.StatusCode))
            .RetryAsync(configuration.MaxRetries);
    }

    private static Polly.IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(ConduitAdminClientConfiguration configuration)
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
/// Extension methods for configuring ConduitLLM Admin Client with options pattern.
/// </summary>
public static class ConduitAdminClientOptionsExtensions
{
    /// <summary>
    /// Configures ConduitLLM Admin Client options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="configurationSectionName">The configuration section name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureConduitAdminClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string configurationSectionName = "ConduitAdmin")
    {
        services.Configure<ConduitAdminClientConfiguration>(
            configuration.GetSection(configurationSectionName));

        return services;
    }

    /// <summary>
    /// Configures ConduitLLM Admin Client options with an action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">The configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureConduitAdminClient(
        this IServiceCollection services,
        Action<ConduitAdminClientConfiguration> configureOptions)
    {
        services.Configure(configureOptions);
        return services;
    }
}