using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Handlers;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.Services.SpendNotification;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Extension methods for registering webhook-related services
/// </summary>
public static class WebhookServicesExtensions
{
    /// <summary>
    /// Adds webhook services including delivery, metrics, connection tracking, and circuit breakers
    /// </summary>
    public static IServiceCollection AddWebhookServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Webhook Delivery Service
        services.AddSingleton<IWebhookDeliveryService, WebhookDeliveryService>();

        // Register Distributed Spend Notification Service (Redis-based for multi-instance consistency) - with leader election
        services.AddSingleton<ISpendNotificationService, DistributedSpendNotificationService>();
        services.AddLeaderElectedHostedService<DistributedSpendNotificationService>(
            sp => (DistributedSpendNotificationService)sp.GetRequiredService<ISpendNotificationService>(),
            "SpendNotificationService");

        // Register Webhook Metrics Service (Redis-based when available)
        services.AddSingleton<ConduitLLM.Core.Services.IWebhookMetricsService>(sp =>
        {
            var redis = sp.GetService<IConnectionMultiplexer>();

            if (redis != null)
            {
                var logger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.RedisWebhookMetricsService>>();
                return new ConduitLLM.Core.Services.RedisWebhookMetricsService(redis, logger);
            }

            // Return null when Redis is not available - the notification service will handle fallback
            return null!;
        });

        // Register Webhook Connection Tracker (Redis-based when available)
        services.AddSingleton<ConduitLLM.Core.Services.IWebhookConnectionTracker>(sp =>
        {
            var redis = sp.GetService<IConnectionMultiplexer>();

            if (redis != null)
            {
                var logger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.RedisWebhookConnectionTracker>>();
                return new ConduitLLM.Core.Services.RedisWebhookConnectionTracker(redis, logger);
            }
            else
            {
                // Fall back to in-memory tracker
                var logger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.InMemoryWebhookConnectionTracker>>();
                return new ConduitLLM.Core.Services.InMemoryWebhookConnectionTracker(logger);
            }
        });

        // Register Webhook Delivery Notification Service - with leader election
        services.AddSingleton<IWebhookDeliveryNotificationService>(sp =>
        {
            var hubContext = sp.GetRequiredService<IHubContext<Hubs.WebhookDeliveryHub>>();
            var serviceProvider = sp;
            var logger = sp.GetRequiredService<ILogger<WebhookDeliveryNotificationService>>();
            return new WebhookDeliveryNotificationService(hubContext, serviceProvider, logger);
        });
        services.AddLeaderElectedHostedService<WebhookDeliveryNotificationService>(
            sp => (WebhookDeliveryNotificationService)sp.GetRequiredService<IWebhookDeliveryNotificationService>(),
            "WebhookDeliveryNotificationService");

        // Register Webhook Circuit Breaker for preventing repeated failures
        services.AddSingleton<ConduitLLM.Core.Services.IWebhookCircuitBreaker>(sp =>
        {
            var redis = sp.GetService<IConnectionMultiplexer>();

            if (redis != null)
            {
                // Use Redis-based distributed circuit breaker when available
                var redisLogger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.RedisWebhookCircuitBreaker>>();
                return new ConduitLLM.Core.Services.RedisWebhookCircuitBreaker(
                    redis,
                    redisLogger,
                    failureThreshold: 5,
                    openDuration: TimeSpan.FromMinutes(5),
                    halfOpenTestInterval: TimeSpan.FromSeconds(30));
            }
            else
            {
                // Fall back to in-memory circuit breaker
                var cache = sp.GetRequiredService<IMemoryCache>();
                var logger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.WebhookCircuitBreaker>>();

                return new ConduitLLM.Core.Services.WebhookCircuitBreaker(
                    cache,
                    logger,
                    failureThreshold: 5,
                    openDuration: TimeSpan.FromMinutes(5),
                    counterResetDuration: TimeSpan.FromMinutes(15));
            }
        });

        // Register Webhook Notification Service with optimized configuration for high throughput
        services.AddTransient<WebhookMetricsHandler>();
        services.AddHttpClient<IWebhookNotificationService, WebhookNotificationService>(
            "WebhookClient",
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM/1.0");
                client.DefaultRequestHeaders.ConnectionClose = false;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 100,
                EnableMultipleHttp2Connections = true,
                MaxResponseHeadersLength = 64 * 1024,
                ResponseDrainTimeout = TimeSpan.FromSeconds(5),
                ConnectTimeout = TimeSpan.FromSeconds(5),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(20),
                KeepAlivePingDelay = TimeSpan.FromSeconds(30)
            })
            .AddPolicyHandler((sp, _) => GetWebhookRetryPolicy(sp.GetRequiredService<ILogger<WebhookNotificationService>>()))
            .AddPolicyHandler((sp, _) => GetWebhookCircuitBreakerPolicy(sp.GetRequiredService<ILogger<WebhookNotificationService>>()))
            .AddHttpMessageHandler<WebhookMetricsHandler>();

        return services;
    }

    /// <summary>
    /// Polly retry policy for webhook delivery
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetWebhookRetryPolicy(ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode && msg.StatusCode != System.Net.HttpStatusCode.BadRequest)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    logger.LogWarning("Webhook retry attempt {RetryCount} after {DelayMs}ms. Status: {StatusCode}",
                        retryCount, timespan.TotalMilliseconds, outcome.Result?.StatusCode.ToString() ?? "N/A");
                });
    }

    /// <summary>
    /// Polly circuit breaker policy for webhook delivery
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetWebhookCircuitBreakerPolicy(ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (result, duration) =>
                {
                    logger.LogWarning("Webhook circuit breaker opened for {DurationSeconds} seconds", duration.TotalSeconds);
                },
                onReset: () =>
                {
                    logger.LogInformation("Webhook circuit breaker reset");
                });
    }
}
