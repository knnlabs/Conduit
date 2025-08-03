using System.Text.Json;
using System.Text.Json.Serialization;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Routing;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

public partial class Program
{
    public static void ConfigureEndpoints(WebApplication app)
    {
        // Get JsonSerializerOptions from DI
        var jsonSerializerOptions = app.Services.GetRequiredService<JsonSerializerOptions>();

        // Map SignalR hubs for real-time updates

        // Customer-facing hubs require virtual key authentication
        app.MapHub<ConduitLLM.Http.Hubs.VideoGenerationHub>("/hubs/video-generation")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR VideoGenerationHub registered at /hubs/video-generation (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.ImageGenerationHub>("/hubs/image-generation")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR ImageGenerationHub registered at /hubs/image-generation (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.TaskHub>("/hubs/tasks")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR TaskHub registered at /hubs/tasks (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.SystemNotificationHub>("/hubs/notifications")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR SystemNotificationHub registered at /hubs/notifications (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.SpendNotificationHub>("/hubs/spend")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR SpendNotificationHub registered at /hubs/spend (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.WebhookDeliveryHub>("/hubs/webhooks")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR WebhookDeliveryHub registered at /hubs/webhooks (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.ModelDiscoveryHub>("/hubs/model-discovery")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR ModelDiscoveryHub registered at /hubs/model-discovery (requires authentication)");

        // Admin-only hub for metrics dashboard
        app.MapHub<ConduitLLM.Http.Hubs.MetricsHub>("/hubs/metrics")
            .RequireAuthorization("AdminOnly");
        Console.WriteLine("[Conduit API] SignalR MetricsHub registered at /hubs/metrics (requires admin authentication)");

        // Admin-only hub for health monitoring
        app.MapHub<ConduitLLM.Http.Hubs.HealthMonitoringHub>("/hubs/health-monitoring")
            .RequireAuthorization("AdminOnly");
        Console.WriteLine("[Conduit API] SignalR HealthMonitoringHub registered at /hubs/health-monitoring (requires admin authentication)");

        // Admin-only hub for security monitoring
        app.MapHub<ConduitLLM.Http.Hubs.SecurityMonitoringHub>("/hubs/security-monitoring")
            .RequireAuthorization("AdminOnly");
        Console.WriteLine("[Conduit API] SignalR SecurityMonitoringHub registered at /hubs/security-monitoring (requires admin authentication)");

        // Virtual key management hub for real-time key management updates
        app.MapHub<ConduitLLM.Http.Hubs.VirtualKeyManagementHub>("/hubs/virtual-key-management")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR VirtualKeyManagementHub registered at /hubs/virtual-key-management (requires authentication)");

        // Usage analytics hub for real-time analytics and monitoring
        app.MapHub<ConduitLLM.Http.Hubs.UsageAnalyticsHub>("/hubs/usage-analytics")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR UsageAnalyticsHub registered at /hubs/usage-analytics (requires authentication)");

        // Enhanced video generation hub with acknowledgment support
        app.MapHub<ConduitLLM.Http.SignalR.Hubs.EnhancedVideoGenerationHub>("/hubs/enhanced-video-generation")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR EnhancedVideoGenerationHub registered at /hubs/enhanced-video-generation (requires authentication)");

        // Map health check endpoints without authentication requirement
        // Health endpoints should be accessible without authentication for monitoring tools
        app.MapSecureConduitHealthChecks(requireAuthorization: false);

        // Map Prometheus metrics endpoint for scraping
        app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");
        Console.WriteLine("[Conduit API] Prometheus metrics endpoint registered at /metrics");

        Console.WriteLine("[Conduit API] All API endpoints are now handled by controllers.");
    }
}