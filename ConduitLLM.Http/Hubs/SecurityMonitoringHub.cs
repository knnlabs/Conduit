using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Security.Interfaces;
using ConduitLLM.Configuration.DTOs.Security;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time security monitoring and alerts
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    public class SecurityMonitoringHub : Hub
    {
        private readonly ISecurityEventMonitoringService _securityEventMonitoring;
        private readonly ILogger<SecurityMonitoringHub> _logger;

        /// <summary>
        /// Initializes a new instance of the SecurityMonitoringHub
        /// </summary>
        public SecurityMonitoringHub(
            ISecurityEventMonitoringService securityEventMonitoring,
            ILogger<SecurityMonitoringHub> logger)
        {
            _securityEventMonitoring = securityEventMonitoring;
            _logger = logger;
        }

        /// <summary>
        /// Called when a client connects to the hub
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Admin client connected to security monitoring: {ConnectionId}", Context.ConnectionId);
            
            // Send current security metrics on connection
            var metrics = await _securityEventMonitoring.GetSecurityMetricsAsync();
            await Clients.Caller.SendAsync("SecurityMetricsUpdate", metrics);
            
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub
        /// </summary>
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Admin client disconnected from security monitoring: {ConnectionId}", Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Get current security metrics
        /// </summary>
        public async Task<SecurityMetricsDto> GetSecurityMetrics()
        {
            try
            {
                return await _securityEventMonitoring.GetSecurityMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security metrics");
                throw new HubException("Failed to retrieve security metrics");
            }
        }

        /// <summary>
        /// Get recent security events
        /// </summary>
        public async Task<List<SecurityEventDto>> GetRecentSecurityEvents(int minutes = 60)
        {
            try
            {
                if (minutes < 1 || minutes > 1440) // Max 24 hours
                {
                    throw new ArgumentException("Minutes must be between 1 and 1440");
                }

                return await _securityEventMonitoring.GetRecentSecurityEventsAsync(minutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent security events");
                throw new HubException("Failed to retrieve security events");
            }
        }

        /// <summary>
        /// Stream real-time security events
        /// </summary>
        public async IAsyncEnumerable<SecurityEventDto> StreamSecurityEvents(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting security event stream for {ConnectionId}", Context.ConnectionId);

            var lastEventTime = DateTime.UtcNow;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                // Get events since last check
                var events = await _securityEventMonitoring.GetRecentSecurityEventsAsync(1);
                
                foreach (var @event in events)
                {
                    if (@event.Timestamp > lastEventTime)
                    {
                        lastEventTime = @event.Timestamp;
                        yield return @event;
                    }
                }

                // Wait before next check
                await Task.Delay(1000, cancellationToken); // Check every second
            }
        }

        /// <summary>
        /// Subscribe to threat level changes
        /// </summary>
        public async Task SubscribeToThreatLevel()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "threat-level-subscribers");
            _logger.LogInformation("Client {ConnectionId} subscribed to threat level updates", Context.ConnectionId);
        }

        /// <summary>
        /// Unsubscribe from threat level changes
        /// </summary>
        public async Task UnsubscribeFromThreatLevel()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "threat-level-subscribers");
            _logger.LogInformation("Client {ConnectionId} unsubscribed from threat level updates", Context.ConnectionId);
        }

        /// <summary>
        /// Subscribe to security events by type
        /// </summary>
        public async Task SubscribeToEventType(SecurityEventType eventType)
        {
            var groupName = $"security-event-{eventType}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Client {ConnectionId} subscribed to {EventType} security events", 
                Context.ConnectionId, eventType);
        }

        /// <summary>
        /// Unsubscribe from security events by type
        /// </summary>
        public async Task UnsubscribeFromEventType(SecurityEventType eventType)
        {
            var groupName = $"security-event-{eventType}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Client {ConnectionId} unsubscribed from {EventType} security events", 
                Context.ConnectionId, eventType);
        }
    }
}