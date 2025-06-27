#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Metrics;

namespace ConduitLLM.Http.HealthChecks
{
    /// <summary>
    /// Health check for SignalR infrastructure.
    /// </summary>
    public class SignalRHealthCheck : IHealthCheck
    {
        private readonly IHubContext<SystemNotificationHub> _notificationHubContext;
        private readonly IHubContext<TaskHub> _taskHubContext;
        private readonly IHubContext<ImageGenerationHub> _imageHubContext;
        private readonly IHubContext<VideoGenerationHub> _videoHubContext;
        private readonly SignalRMetrics _metrics;
        private readonly ILogger<SignalRHealthCheck> _logger;

        public SignalRHealthCheck(
            IHubContext<SystemNotificationHub> notificationHubContext,
            IHubContext<TaskHub> taskHubContext,
            IHubContext<ImageGenerationHub> imageHubContext,
            IHubContext<VideoGenerationHub> videoHubContext,
            SignalRMetrics metrics,
            ILogger<SignalRHealthCheck> logger)
        {
            _notificationHubContext = notificationHubContext ?? throw new ArgumentNullException(nameof(notificationHubContext));
            _taskHubContext = taskHubContext ?? throw new ArgumentNullException(nameof(taskHubContext));
            _imageHubContext = imageHubContext ?? throw new ArgumentNullException(nameof(imageHubContext));
            _videoHubContext = videoHubContext ?? throw new ArgumentNullException(nameof(videoHubContext));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var data = new Dictionary<string, object>();
                var healthyHubs = 0;
                var totalHubs = 4;

                // Check each hub context
                if (await CheckHubContext(_notificationHubContext, "SystemNotificationHub", data))
                    healthyHubs++;
                
                if (await CheckHubContext(_taskHubContext, "TaskHub", data))
                    healthyHubs++;
                
                if (await CheckHubContext(_imageHubContext, "ImageGenerationHub", data))
                    healthyHubs++;
                
                if (await CheckHubContext(_videoHubContext, "VideoGenerationHub", data))
                    healthyHubs++;

                // Add metric data
                data["active_connections"] = "Available via metrics endpoint";
                data["authentication_failures_rate"] = "Available via metrics endpoint";
                data["hub_errors_rate"] = "Available via metrics endpoint";
                data["message_processing_p95"] = "Available via metrics endpoint";

                // Determine health status
                if (healthyHubs == totalHubs)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                        "All SignalR hubs are healthy",
                        data);
                }
                else if (healthyHubs > 0)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                        $"{healthyHubs}/{totalHubs} SignalR hubs are healthy",
                        null,
                        data);
                }
                else
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                        "All SignalR hubs are unhealthy",
                        null,
                        data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking SignalR health");
                
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "SignalR health check failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    });
            }
        }

        private async Task<bool> CheckHubContext<THub>(
            IHubContext<THub> hubContext, 
            string hubName,
            Dictionary<string, object> data) where THub : Hub
        {
            try
            {
                // Test that we can access the hub context
                var clients = hubContext.Clients;
                
                // Test sending a message to a non-existent group (shouldn't throw)
                await clients.Group($"health-check-{Guid.NewGuid()}")
                    .SendAsync("HealthCheck", DateTimeOffset.UtcNow);
                
                data[$"{hubName}_status"] = "healthy";
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hub context check failed for {HubName}", hubName);
                data[$"{hubName}_status"] = $"unhealthy: {ex.Message}";
                return false;
            }
        }
    }
}