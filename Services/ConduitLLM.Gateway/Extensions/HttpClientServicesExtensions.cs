using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Polly;
using Polly.Extensions.Http;

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
        .AddPolicyHandler(GetImageDownloadRetryPolicy());

        // Register IImageDownloadService for DI-friendly image downloading
        services.AddScoped<IImageDownloadService, ImageDownloadService>();

        // Register HTTP clients for function providers (Exa and Tavily)
        services.AddHttpClient("ExaFunctionClient", client =>
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
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient("TavilyFunctionClient", client =>
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
        .AddPolicyHandler(GetRetryPolicy());

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
        .AddPolicyHandler(GetImageDownloadRetryPolicy())
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
        .AddPolicyHandler(GetVideoDownloadRetryPolicy())
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(15)));

        // Configure HttpClient for discovery providers
        services.AddHttpClient("DiscoveryProviders", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM/1.0");
        });

        // Register File Retrieval Service with retry-enabled HttpClient for resilient URL fetching
        services.AddHttpClient<IFileRetrievalService, FileRetrievalService>()
            .AddPolicyHandler(GetRetryPolicy())
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            });

        return services;
    }

    /// <summary>
    /// Polly retry policy for image downloads with exponential backoff
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetImageDownloadRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.Values.FirstOrDefault() as ILogger;
                    logger?.LogWarning("Image download retry {RetryCount} after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Polly retry policy for video downloads with longer exponential backoff
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetVideoDownloadRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(3, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.Values.FirstOrDefault() as ILogger;
                    logger?.LogWarning("Video download retry {RetryCount} after {Delay}s", retryCount, timespan.TotalSeconds);
                });
    }

    /// <summary>
    /// Standard retry policy for HTTP requests with exponential backoff and jitter
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000))
            );
    }
}
