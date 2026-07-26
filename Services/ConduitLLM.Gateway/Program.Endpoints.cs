using ConduitLLM.Gateway.Endpoints;

public partial class Program
{
    public static void ConfigureEndpoints(WebApplication app)
    {
        app.MapModelsEndpoints();
        app.MapGatewayApiEndpoints();
        app.MapGatewayInternalOperationsEndpoints();

        // Map SignalR hubs for real-time updates

        // Customer-facing hubs require virtual key authentication
        app.MapHub<ConduitLLM.Gateway.Hubs.VideoGenerationHub>("/hubs/video-generation")
            .RequireAuthorization();

        // Public video generation hub using task-scoped tokens (no virtual key required)
        app.MapHub<ConduitLLM.Gateway.Hubs.PublicVideoGenerationHub>("/hubs/public/video-generation");

        app.MapHub<ConduitLLM.Gateway.Hubs.ImageGenerationHub>("/hubs/image-generation")
            .RequireAuthorization();

        app.MapHub<ConduitLLM.Gateway.Hubs.TaskHub>("/hubs/tasks")
            .RequireAuthorization();

        app.MapHub<ConduitLLM.Gateway.Hubs.SystemNotificationHub>("/hubs/notifications")
            .RequireAuthorization();

        app.MapHub<ConduitLLM.Gateway.Hubs.SpendNotificationHub>("/hubs/spend")
            .RequireAuthorization();

        app.MapHub<ConduitLLM.Gateway.Hubs.WebhookDeliveryHub>("/hubs/webhooks")
            .RequireAuthorization();

        // Virtual key management hub for real-time key management updates
        app.MapHub<ConduitLLM.Gateway.Hubs.VirtualKeyManagementHub>("/hubs/virtual-key-management")
            .RequireAuthorization();

        // Enhanced video generation hub with acknowledgment support
        app.MapHub<ConduitLLM.Gateway.Hubs.EnhancedVideoGenerationHub>("/hubs/enhanced-video-generation")
            .RequireAuthorization();

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
    }
}
