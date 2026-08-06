using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Configuration.DTOs.SignalR;

using ConduitLLM.Gateway.Interfaces;
namespace ConduitLLM.Gateway.Hubs
{
    /// <summary>
    /// SignalR hub for broadcasting system-wide notifications to connected clients.
    /// Extends SecureHub to require virtual key authentication.
    /// </summary>
    public class SystemNotificationHub : SecureHub, ISystemNotificationHub
    {
        private readonly ISignalRMetrics _metrics;
        private readonly ILogger<SystemNotificationHub> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemNotificationHub"/> class.
        /// </summary>
        /// <param name="metrics">SignalR metrics collector.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="serviceProvider">Service provider for dependency injection.</param>
        public SystemNotificationHub(
            ISignalRMetrics metrics,
            ILogger<SystemNotificationHub> logger,
            IServiceProvider serviceProvider) : base(logger, serviceProvider)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the hub name for logging and metrics.
        /// </summary>
        /// <returns>The hub name.</returns>
        protected override string GetHubName() => "SystemNotificationHub";

        /// <summary>
        /// Sends rate limit warnings to connected clients.
        /// </summary>
        public async Task RateLimitWarning(int remaining, DateTime resetTime, string endpoint)
        {
            var totalLimit = remaining > 0 ? remaining * 10 : 100; // Estimate total limit
            var percentageUsed = ((double)(totalLimit - remaining) / totalLimit) * 100;
            
            var notification = new RateLimitNotification
            {
                Remaining = remaining,
                ResetTime = resetTime,
                Endpoint = endpoint,
                PercentageUsed = percentageUsed,
                Priority = remaining < 10 ? NotificationPriority.High : NotificationPriority.Medium
            };

            await BroadcastNotification(notification);
        }

        /// <summary>
        /// Broadcasts system announcements.
        /// </summary>
        public async Task SystemAnnouncement(string message, NotificationPriority priority)
        {
            var notification = new SystemAnnouncementNotification
            {
                Message = message,
                Priority = priority,
                Category = priority == NotificationPriority.Critical ? "urgent" : "general"
            };

            await BroadcastNotification(notification);
        }

        /// <summary>
        /// Notifies about service degradation.
        /// </summary>
        public async Task ServiceDegraded(string service, string reason)
        {
            var notification = new ServiceDegradationNotification
            {
                Service = service,
                Reason = reason,
                Priority = NotificationPriority.High
            };

            await BroadcastNotification(notification);
        }

        /// <summary>
        /// Notifies about service restoration.
        /// </summary>
        public async Task ServiceRestored(string service)
        {
            var notification = new ServiceRestorationNotification
            {
                Service = service,
                Priority = NotificationPriority.Medium
            };

            await BroadcastNotification(notification);
        }

        /// <summary>
        /// Notifies clients of a model mapping change.
        /// </summary>
        public async Task ModelMappingChanged(int mappingId, string modelAlias, string changeType)
        {
            var notification = new ModelMappingNotification
            {
                MappingId = mappingId,
                ModelAlias = modelAlias,
                ChangeType = changeType,
                Priority = NotificationPriority.Medium
            };

            await BroadcastNotification(notification);
            
            // Also notify model-specific subscribers
            var virtualKeyId = GetVirtualKeyId();
            if (virtualKeyId.HasValue)
            {
                var modelGroupName = $"vkey-{virtualKeyId.Value}-model-{modelAlias}";
                await Clients.Group(modelGroupName).SendAsync("OnModelUpdate", notification);
            }
        }


        /// <summary>
        /// Notifies clients of model availability change.
        /// </summary>
        public async Task ModelAvailabilityChanged(string modelId, bool isAvailable)
        {
            var notification = new ModelAvailabilityNotification
            {
                ModelId = modelId,
                IsAvailable = isAvailable,
                Priority = isAvailable ? NotificationPriority.Low : NotificationPriority.Medium
            };

            await BroadcastNotification(notification);
            
            // Also notify model-specific subscribers
            var virtualKeyId = GetVirtualKeyId();
            if (virtualKeyId.HasValue)
            {
                var modelGroupName = $"vkey-{virtualKeyId.Value}-model-{modelId}";
                await Clients.Group(modelGroupName).SendAsync("OnModelUpdate", notification);
            }
        }

        /// <summary>
        /// Broadcasts a notification to all connected clients for the current virtual key.
        /// </summary>
        private async Task BroadcastNotification(SystemNotification notification)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["NotificationType"] = notification.Type,
                ["NotificationId"] = notification.Id
            }))
            {
                try
                {
                    // Get the virtual key from context
                    var virtualKeyId = GetVirtualKeyId();
                    if (!virtualKeyId.HasValue)
                    {
                        _logger.LogWarning("Cannot broadcast notification - no virtual key in context");
                        return;
                    }

                    // Send to all clients in the virtual key's group.
                    var groupName = $"vkey-{virtualKeyId.Value}";
                    
                    // Track metrics
                    _metrics.MessagesSent.Add(1, new("hub", "SystemNotificationHub"), new("message_type", notification.Type));
                    
                    // For batching support, we could queue notifications here
                    // For now, send immediately
                    await Clients.Group(groupName).SendAsync($"On{notification.Type}", notification);
                    
                    _logger.LogInformation(
                        "Broadcast {NotificationType} notification to group {GroupName} with priority {Priority}",
                        notification.Type,
                        groupName,
                        notification.Priority);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1, new("hub", "SystemNotificationHub"), new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error broadcasting notification");
                    throw;
                }
            }
        }

    }
}
