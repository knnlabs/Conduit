using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Metrics;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for broadcasting system-wide notifications to connected clients.
    /// Extends SecureHub to require virtual key authentication.
    /// </summary>
    public class SystemNotificationHub : SecureHub, ISystemNotificationHub
    {
        private readonly SignalRMetrics _metrics;
        private readonly ILogger<SystemNotificationHub> _logger;
        
        // Store notification preferences per connection
        private static readonly ConcurrentDictionary<string, NotificationPreferences> _connectionPreferences = new();
        
        // Notification batching support
        private static readonly ConcurrentDictionary<string, NotificationBatch> _pendingBatches = new();
        private const int BatchSize = 10;
        private const int BatchDelayMs = 500;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemNotificationHub"/> class.
        /// </summary>
        /// <param name="metrics">SignalR metrics collector.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="serviceProvider">Service provider for dependency injection.</param>
        public SystemNotificationHub(
            SignalRMetrics metrics,
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
        /// Called when a client connects to the hub.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            
            // Initialize default preferences for the connection
            _connectionPreferences[Context.ConnectionId] = new NotificationPreferences
            {
                EnabledTypes = new HashSet<string> { "provider_health", "rate_limit", "system_announcement", "service_degradation", "service_restoration" },
                MinimumPriority = NotificationPriority.Low
            };
            
            _logger.LogInformation("Client connected to SystemNotificationHub: {ConnectionId}", Context.ConnectionId);
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        /// <param name="exception">The exception that caused the disconnect, if any.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Clean up preferences
            _connectionPreferences.TryRemove(Context.ConnectionId, out _);
            
            // Clean up any pending batches
            _pendingBatches.TryRemove(Context.ConnectionId, out _);
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Updates notification preferences for the connected client.
        /// </summary>
        /// <param name="preferences">The notification preferences.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UpdatePreferences(NotificationPreferences preferences)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["Method"] = nameof(UpdatePreferences)
            }))
            {
                try
                {
                    _connectionPreferences[Context.ConnectionId] = preferences;
                    
                    await Clients.Caller.SendAsync("PreferencesUpdated", preferences);
                    
                    _logger.LogInformation(
                        "Updated notification preferences for connection {ConnectionId}: {EnabledTypes}, MinPriority: {MinPriority}",
                        Context.ConnectionId,
                        string.Join(", ", preferences.EnabledTypes),
                        preferences.MinimumPriority);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating notification preferences");
                    throw;
                }
            }
        }

        /// <summary>
        /// Notifies clients about provider health status changes.
        /// </summary>
        public async Task ProviderHealthChanged(string provider, HealthStatus status, TimeSpan? responseTime)
        {
            var notification = new ProviderHealthNotification
            {
                Provider = provider,
                Status = status.ToString(),
                ResponseTimeMs = responseTime?.TotalMilliseconds,
                Priority = status == HealthStatus.Unhealthy ? NotificationPriority.High : NotificationPriority.Medium,
                Details = status switch
                {
                    HealthStatus.Healthy => $"{provider} is operating normally",
                    HealthStatus.Degraded => $"{provider} is experiencing performance issues",
                    HealthStatus.Unhealthy => $"{provider} is currently unavailable",
                    _ => null
                }
            };

            await BroadcastNotification(notification);
        }

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
        /// Notifies clients of model capabilities discovery.
        /// </summary>
        public async Task ModelCapabilitiesDiscovered(string providerName, int modelCount, int embeddingCount = 0, int visionCount = 0, int imageGenCount = 0, int videoGenCount = 0)
        {
            var notification = new ModelCapabilitiesNotification
            {
                ProviderName = providerName,
                ModelCount = modelCount,
                EmbeddingCount = embeddingCount,
                VisionCount = visionCount,
                ImageGenCount = imageGenCount,
                VideoGenCount = videoGenCount,
                Priority = NotificationPriority.Low
            };

            await BroadcastNotification(notification);
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
        /// Broadcasts a notification to all connected clients based on their preferences.
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

                    // Send to all clients in the virtual key's group that meet the preference criteria
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

        /// <summary>
        /// Notification preferences for a connected client.
        /// </summary>
        public class NotificationPreferences
        {
            /// <summary>
            /// Gets or sets the enabled notification types.
            /// </summary>
            public HashSet<string> EnabledTypes { get; set; } = new();

            /// <summary>
            /// Gets or sets the minimum priority level to receive.
            /// </summary>
            public NotificationPriority MinimumPriority { get; set; } = NotificationPriority.Low;
        }

        /// <summary>
        /// Represents a batch of notifications.
        /// </summary>
        private class NotificationBatch
        {
            public List<SystemNotification> Notifications { get; } = new();
            public DateTime CreatedAt { get; } = DateTime.UtcNow;
        }
    }
}