using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Admin.Hubs;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Admin.Consumers
{
    /// <summary>
    /// Consumes ProviderHealthChanged domain events and forwards them to Admin SignalR clients.
    /// This ensures admin users receive real-time provider health updates.
    /// </summary>
    public class ProviderHealthChangedConsumer : IConsumer<ProviderHealthChanged>
    {
        private readonly AdminNotificationService _notificationService;
        private readonly ILogger<ProviderHealthChangedConsumer> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHealthChangedConsumer"/> class.
        /// </summary>
        /// <param name="notificationService">The admin notification service for SignalR.</param>
        /// <param name="logger">The logger instance.</param>
        public ProviderHealthChangedConsumer(
            AdminNotificationService notificationService,
            ILogger<ProviderHealthChangedConsumer> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task Consume(ConsumeContext<ProviderHealthChanged> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ProviderHealthChanged event for {ProviderName} - Status: {Status}, IsHealthy: {IsHealthy}",
                    @event.ProviderName, @event.Status, @event.IsHealthy);

                // Parse health status from event
                var healthStatus = @event.Status.ToLowerInvariant() switch
                {
                    "healthy" => HealthStatus.Healthy,
                    "degraded" => HealthStatus.Degraded,
                    "unhealthy" => HealthStatus.Unhealthy,
                    _ => @event.IsHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy
                };

                // Extract response time from health data if available
                TimeSpan? responseTime = null;
                if (@event.HealthData.TryGetValue("responseTime", out var responseTimeObj) && 
                    responseTimeObj is double responseTimeMs)
                {
                    responseTime = TimeSpan.FromMilliseconds(responseTimeMs);
                }

                // Forward to SignalR clients via Admin notification service
                await _notificationService.NotifyProviderHealthUpdate(
                    @event.ProviderName,
                    healthStatus,
                    responseTime,
                    @event.HealthData);

                _logger.LogDebug(
                    "Successfully forwarded ProviderHealthChanged event to Admin SignalR clients for provider {ProviderName}",
                    @event.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to process ProviderHealthChanged event for provider {ProviderName}",
                    @event.ProviderName);
                throw;
            }
        }
    }
}