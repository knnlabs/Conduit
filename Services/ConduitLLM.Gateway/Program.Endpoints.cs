using System.Text.Json;
using ConduitLLM.Core.Utilities;

public partial class Program
{
    public static void ConfigureEndpoints(WebApplication app)
    {
        // Get JsonSerializerOptions from DI
        var jsonSerializerOptions = app.Services.GetRequiredService<JsonSerializerOptions>();

        // Map SignalR hubs for real-time updates

        // Customer-facing hubs require virtual key authentication
        app.MapHub<ConduitLLM.Gateway.Hubs.VideoGenerationHub>("/hubs/video-generation")
            .RequireAuthorization();
        Console.WriteLine("[Gateway API] SignalR VideoGenerationHub registered at /hubs/video-generation (requires authentication)");
        
        // Public video generation hub using task-scoped tokens (no virtual key required)
        app.MapHub<ConduitLLM.Gateway.Hubs.PublicVideoGenerationHub>("/hubs/public/video-generation");
        Console.WriteLine("[Gateway API] SignalR PublicVideoGenerationHub registered at /hubs/public/video-generation (token-based auth)");

        app.MapHub<ConduitLLM.Gateway.Hubs.ImageGenerationHub>("/hubs/image-generation")
            .RequireAuthorization();
        Console.WriteLine("[Gateway API] SignalR ImageGenerationHub registered at /hubs/image-generation (requires authentication)");

        app.MapHub<ConduitLLM.Gateway.Hubs.TaskHub>("/hubs/tasks")
            .RequireAuthorization();
        Console.WriteLine("[Gateway API] SignalR TaskHub registered at /hubs/tasks (requires authentication)");

        app.MapHub<ConduitLLM.Gateway.Hubs.SystemNotificationHub>("/hubs/notifications")
            .RequireAuthorization();
        Console.WriteLine("[Gateway API] SignalR SystemNotificationHub registered at /hubs/notifications (requires authentication)");

        app.MapHub<ConduitLLM.Gateway.Hubs.SpendNotificationHub>("/hubs/spend")
            .RequireAuthorization();
        Console.WriteLine("[Gateway API] SignalR SpendNotificationHub registered at /hubs/spend (requires authentication)");

        app.MapHub<ConduitLLM.Gateway.Hubs.WebhookDeliveryHub>("/hubs/webhooks")
            .RequireAuthorization();
        Console.WriteLine("[Gateway API] SignalR WebhookDeliveryHub registered at /hubs/webhooks (requires authentication)");


        // Admin-only hub for metrics dashboard
        app.MapHub<ConduitLLM.Gateway.Hubs.MetricsHub>("/hubs/metrics")
            .RequireAuthorization("AdminOnly");
        Console.WriteLine("[Gateway API] SignalR MetricsHub registered at /hubs/metrics (requires admin authentication)");

        // Admin-only hub for health monitoring
        app.MapHub<ConduitLLM.Gateway.Hubs.HealthMonitoringHub>("/hubs/health-monitoring")
            .RequireAuthorization("AdminOnly");
        Console.WriteLine("[Gateway API] SignalR HealthMonitoringHub registered at /hubs/health-monitoring (requires admin authentication)");

        // Admin-only hub for security monitoring
        app.MapHub<ConduitLLM.Gateway.Hubs.SecurityMonitoringHub>("/hubs/security-monitoring")
            .RequireAuthorization("AdminOnly");
        Console.WriteLine("[Gateway API] SignalR SecurityMonitoringHub registered at /hubs/security-monitoring (requires admin authentication)");

        // Virtual key management hub for real-time key management updates
        app.MapHub<ConduitLLM.Gateway.Hubs.VirtualKeyManagementHub>("/hubs/virtual-key-management")
            .RequireAuthorization();
        Console.WriteLine("[Gateway API] SignalR VirtualKeyManagementHub registered at /hubs/virtual-key-management (requires authentication)");

        // Usage analytics hub for real-time analytics and monitoring
        app.MapHub<ConduitLLM.Gateway.Hubs.UsageAnalyticsHub>("/hubs/usage-analytics")
            .RequireAuthorization();
        Console.WriteLine("[Gateway API] SignalR UsageAnalyticsHub registered at /hubs/usage-analytics (requires authentication)");

        // Enhanced video generation hub with acknowledgment support
        app.MapHub<ConduitLLM.Gateway.Hubs.EnhancedVideoGenerationHub>("/hubs/enhanced-video-generation")
            .RequireAuthorization();
        Console.WriteLine("[Gateway API] SignalR EnhancedVideoGenerationHub registered at /hubs/enhanced-video-generation (requires authentication)");

        // Map health check endpoints
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            // Exclude monitoring and performance checks from basic health endpoint
            Predicate = check => !check.Tags.Contains("monitoring") && !check.Tags.Contains("performance")
        });
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready") || check.Tags.Count == 0
        });

        // Map Prometheus metrics endpoint for scraping
        // Allow unauthenticated access from private networks (Docker internal, localhost)
        // Require authentication for external/public network requests
        app.UseOpenTelemetryPrometheusScrapingEndpoint(
            context => context.Request.Path == "/metrics" &&
                      (IpAddressHelper.IsPrivateNetworkRequest(context) ||
                       context.User.Identity?.IsAuthenticated == true));
        Console.WriteLine("[Gateway API] Prometheus metrics endpoint registered at /metrics (private network or authenticated)");

        Console.WriteLine("[Gateway API] All API endpoints are now handled by controllers.");
    }
}