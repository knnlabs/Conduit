using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Policies;
using ConduitLLM.Core.Services;
using Polly;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Extension methods for registering HTTP client services with retry and circuit breaker policies
/// </summary>
public static class HttpClientServicesExtensions
{
    /// <summary>
    /// Adds HTTP client services for image downloads, function providers, and file retrieval
    /// </summary>
    public static IServiceCollection AddHttpClientServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register HTTP client for external image fetching (used by IImageDownloadService)
        services.AddHttpClient(ImageDownloadService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM/1.0");
            client.DefaultRequestHeaders.Add("Accept", "image/*");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 20,
            EnableMultipleHttp2Connections = true
        })
        .AddPolicyHandler(HttpRetryPolicies.GetMediaDownloadRetryPolicy(backoffBase: 2, mediaType: "Image"));

        // Register IImageDownloadService for DI-friendly image downloading
        services.AddScoped<IImageDownloadService, ImageDownloadService>();

        // Register HTTP clients for function providers (Exa and Tavily)
        AddFunctionProviderHttpClient(services, "ExaFunctionClient");
        AddFunctionProviderHttpClient(services, "TavilyFunctionClient");

        // Register HTTP client for image downloads with retry policies
        services.AddHttpClient("ImageDownload", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM-ImageDownloader/1.0");
            client.DefaultRequestHeaders.Add("Accept", "image/*");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 20,
            EnableMultipleHttp2Connections = true,
            MaxResponseHeadersLength = 64 * 1024,
            ResponseDrainTimeout = TimeSpan.FromSeconds(10),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        })
        .AddPolicyHandler(HttpRetryPolicies.GetMediaDownloadRetryPolicy(backoffBase: 2, mediaType: "Image"))
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(120)));

        // Register HTTP client for video downloads with retry policies
        services.AddHttpClient("VideoDownload", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM-VideoDownloader/1.0");
            client.DefaultRequestHeaders.Add("Accept", "video/*");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true,
            MaxResponseHeadersLength = 64 * 1024,
            ResponseDrainTimeout = TimeSpan.FromSeconds(30),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        })
        .AddPolicyHandler(HttpRetryPolicies.GetMediaDownloadRetryPolicy(backoffBase: 3, mediaType: "Video"))
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(15)));

        // Configure HttpClient for discovery providers
        services.AddHttpClient("DiscoveryProviders", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM/1.0");
        });

        // Register File Retrieval Service with retry-enabled HttpClient for resilient URL fetching
        services.AddHttpClient<IFileRetrievalService, FileRetrievalService>()
            .AddPolicyHandler(HttpRetryPolicies.GetStandardRetryPolicy())
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            });

        return services;
    }

    /// <summary>
    /// Registers an HTTP client for a function provider with standard configuration.
    /// </summary>
    private static void AddFunctionProviderHttpClient(IServiceCollection services, string clientName)
    {
        services.AddHttpClient(clientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM-Functions");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        })
        .AddPolicyHandler(HttpRetryPolicies.GetStandardRetryPolicy());
    }
}
