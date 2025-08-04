using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Core.Events;
using ConduitLLM.Admin.Hubs;

namespace ConduitLLM.Admin.Consumers
{
    /// <summary>
    /// Consumes ProviderHealthChanged domain events and forwards them to Admin SignalR clients.
    /// This ensures admin users receive real-time provider health updates.
    /// </summary>
    public class ProviderHealthChangedConsumer : IConsumer<ProviderHealthChanged>
    {
        private readonly IHubContext<AdminNotificationHub> _hubContext;
        private readonly ILogger<ProviderHealthChangedConsumer> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHealthChangedConsumer"/> class.
        /// </summary>
        /// <param name="hubContext">The SignalR hub context.</param>
        /// <param name="logger">The logger instance.</param>
        public ProviderHealthChangedConsumer(
            IHubContext<AdminNotificationHub> hubContext,
            ILogger<ProviderHealthChangedConsumer> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task Consume(ConsumeContext<ProviderHealthChanged> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ProviderHealthChanged event for {ProviderId} - Status: {Status}, IsHealthy: {IsHealthy}",
                    @event.ProviderId, @event.Status, @event.IsHealthy);

                // Create a simple health update object
                var healthUpdate = new
                {
                    ProviderId = @event.ProviderId,
                    Status = @event.Status,
                    IsHealthy = @event.IsHealthy,
                    HealthData = @event.HealthData,
                    Timestamp = DateTime.UtcNow
                };

                // Send to all admins and those subscribed to this specific provider
                await _hubContext.Clients.Groups("admin", $"admin-provider-{@event.ProviderId}")
                    .SendAsync("ProviderHealthUpdate", healthUpdate);

                _logger.LogDebug(
                    "Successfully forwarded ProviderHealthChanged event to Admin SignalR clients for provider {ProviderId}",
                    @event.ProviderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to process ProviderHealthChanged event for provider {ProviderId}",
                    @event.ProviderId);
                throw;
            }
        }
    }
}