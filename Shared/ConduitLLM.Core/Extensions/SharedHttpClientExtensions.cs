using ConduitLLM.Core.Policies;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Core.Extensions;

/// <summary>
/// Shared HTTP client registrations used by both Gateway and Admin services.
/// Centralizes configuration to avoid duplication and ensure consistent behavior.
/// </summary>
public static class SharedHttpClientExtensions
{
    /// <summary>
    /// Registers HTTP clients shared between Gateway and Admin: DiscoveryProviders,
    /// ImageDownload (ExternalImageFetch), Exa, and Tavily function providers.
    /// Also registers <see cref="IImageDownloadService"/> for DI-friendly image downloading.
    /// </summary>
    public static IServiceCollection AddSharedHttpClients(this IServiceCollection services)
    {
        // Configure HttpClient for discovery providers
        services.AddHttpClient("DiscoveryProviders", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM/1.0");
        });

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
        });

        // Register IImageDownloadService for DI-friendly image downloading
        services.AddScoped<Interfaces.IImageDownloadService, ImageDownloadService>();

        // Register HTTP clients for function providers (Exa and Tavily)
        AddFunctionProviderHttpClient(services, "ExaFunctionClient");
        AddFunctionProviderHttpClient(services, "TavilyFunctionClient");

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
