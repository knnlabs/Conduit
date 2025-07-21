using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Metrics;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time webhook delivery tracking and notifications.
    /// </summary>
    public class WebhookDeliveryHub : SecureHub
    {
        private readonly ILogger<WebhookDeliveryHub> _logger;
        private readonly SignalRMetrics _metrics;
        
        // Track active delivery tracking sessions per connection
        private static readonly ConcurrentDictionary<string, HashSet<string>> _connectionWebhooks = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookDeliveryHub"/> class.
        /// </summary>
        /// <param name="metrics">SignalR metrics instance.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="serviceProvider">Service provider for dependency resolution.</param>
        public WebhookDeliveryHub(
            SignalRMetrics metrics,
            ILogger<WebhookDeliveryHub> logger,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the name of the hub for logging and metrics.
        /// </summary>
        /// <returns>The hub name.</returns>
        protected override string GetHubName() => "WebhookDeliveryHub";

        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            
            // Initialize webhook tracking for this connection
            _connectionWebhooks[Context.ConnectionId] = new HashSet<string>();
            
            _logger.LogInformation(
                "Client {ConnectionId} connected to WebhookDeliveryHub",
                Context.ConnectionId);
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        /// <param name="exception">Exception that caused the disconnect, if any.</param>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Clean up webhook tracking
            _connectionWebhooks.TryRemove(Context.ConnectionId, out _);
            
            await base.OnDisconnectedAsync(exception);
            
            _logger.LogInformation(
                "Client {ConnectionId} disconnected from WebhookDeliveryHub",
                Context.ConnectionId);
        }

        /// <summary>
        /// Subscribe to webhook delivery updates for specific URLs.
        /// </summary>
        /// <param name="webhookUrls">The webhook URLs to track.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SubscribeToWebhooks(string[] webhookUrls)
        {
            if (webhookUrls == null || webhookUrls.Length == 0)
            {
                return;
            }

            var virtualKeyId = GetVirtualKeyId();
            if (!virtualKeyId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Authentication required");
                return;
            }

            // Add to connection's webhook list
            if (_connectionWebhooks.TryGetValue(Context.ConnectionId, out var webhooks))
            {
                foreach (var url in webhookUrls)
                {
                    webhooks.Add(url);
                    
                    // Join a group for this webhook URL
                    var groupName = GetWebhookGroupName(url);
                    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                }
            }

            _logger.LogInformation(
                "Client {ConnectionId} subscribed to {Count} webhook URLs",
                Context.ConnectionId, webhookUrls.Length);
        }

        /// <summary>
        /// Unsubscribe from webhook delivery updates for specific URLs.
        /// </summary>
        /// <param name="webhookUrls">The webhook URLs to stop tracking.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UnsubscribeFromWebhooks(string[] webhookUrls)
        {
            if (webhookUrls == null || webhookUrls.Length == 0)
            {
                return;
            }

            // Remove from connection's webhook list
            if (_connectionWebhooks.TryGetValue(Context.ConnectionId, out var webhooks))
            {
                foreach (var url in webhookUrls)
                {
                    webhooks.Remove(url);
                    
                    // Leave the group for this webhook URL
                    var groupName = GetWebhookGroupName(url);
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                }
            }

            _logger.LogInformation(
                "Client {ConnectionId} unsubscribed from {Count} webhook URLs",
                Context.ConnectionId, webhookUrls.Length);
        }

        /// <summary>
        /// Request current statistics for tracked webhooks.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RequestStatistics()
        {
            var virtualKeyId = GetVirtualKeyId();
            if (!virtualKeyId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Authentication required");
                return;
            }

            // This would typically fetch statistics from a service
            // For now, send a placeholder response
            await Clients.Caller.SendAsync("StatisticsRequested", Context.ConnectionId);
            
            _logger.LogDebug(
                "Statistics requested by client {ConnectionId}",
                Context.ConnectionId);
        }

        /// <summary>
        /// Broadcast a delivery attempt to relevant clients.
        /// </summary>
        /// <param name="webhookUrl">The webhook URL.</param>
        /// <param name="attempt">The delivery attempt details.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastDeliveryAttempt(string webhookUrl, WebhookDeliveryAttempt attempt)
        {
            var groupName = GetWebhookGroupName(webhookUrl);
            
            await Clients.Group(groupName).SendAsync("DeliveryAttempted", attempt);
            
            RecordMetrics("delivery_attempt");
        }

        /// <summary>
        /// Broadcast a successful delivery to relevant clients.
        /// </summary>
        /// <param name="webhookUrl">The webhook URL.</param>
        /// <param name="success">The successful delivery details.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastDeliverySuccess(string webhookUrl, WebhookDeliverySuccess success)
        {
            var groupName = GetWebhookGroupName(webhookUrl);
            
            await Clients.Group(groupName).SendAsync("DeliverySucceeded", success);
            
            RecordMetrics("delivery_success");
        }

        /// <summary>
        /// Broadcast a delivery failure to relevant clients.
        /// </summary>
        /// <param name="webhookUrl">The webhook URL.</param>
        /// <param name="failure">The delivery failure details.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastDeliveryFailure(string webhookUrl, WebhookDeliveryFailure failure)
        {
            var groupName = GetWebhookGroupName(webhookUrl);
            
            await Clients.Group(groupName).SendAsync("DeliveryFailed", failure);
            
            RecordMetrics("delivery_failure");
        }

        /// <summary>
        /// Broadcast retry information to relevant clients.
        /// </summary>
        /// <param name="webhookUrl">The webhook URL.</param>
        /// <param name="retry">The retry information.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastRetryScheduled(string webhookUrl, WebhookRetryInfo retry)
        {
            var groupName = GetWebhookGroupName(webhookUrl);
            
            await Clients.Group(groupName).SendAsync("RetryScheduled", retry);
            
            RecordMetrics("retry_scheduled");
        }

        /// <summary>
        /// Gets the group name for a webhook URL.
        /// </summary>
        /// <param name="webhookUrl">The webhook URL.</param>
        /// <returns>The group name.</returns>
        private static string GetWebhookGroupName(string webhookUrl)
        {
            // Create a safe group name from the URL
            var uri = new Uri(webhookUrl);
            return $"webhook-{uri.Host.Replace(".", "-")}-{uri.AbsolutePath.Replace("/", "-")}";
        }

        /// <summary>
        /// Records metrics for hub activities.
        /// </summary>
        /// <param name="eventType">The type of event.</param>
        private void RecordMetrics(string eventType)
        {
            var tags = new TagList
            {
                { "hub", "WebhookDeliveryHub" },
                { "message_type", eventType }
            };
            _metrics.MessagesSent.Add(1, tags);
        }
    }
}