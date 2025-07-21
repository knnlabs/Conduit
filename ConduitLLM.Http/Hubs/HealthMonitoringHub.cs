using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.DTOs.HealthMonitoring;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Runtime.CompilerServices;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time health monitoring and alerts
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    public class HealthMonitoringHub : Hub
    {
        private readonly IHealthMonitoringService _healthMonitoringService;
        private readonly IAlertManagementService _alertManagementService;
        private readonly ILogger<HealthMonitoringHub> _logger;

        public HealthMonitoringHub(
            IHealthMonitoringService healthMonitoringService,
            IAlertManagementService alertManagementService,
            ILogger<HealthMonitoringHub> logger)
        {
            _healthMonitoringService = healthMonitoringService;
            _alertManagementService = alertManagementService;
            _logger = logger;
        }

        /// <summary>
        /// Called when a client connects
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Health monitoring client connected: {ConnectionId}", Context.ConnectionId);
            
            // Send current system health snapshot
            var snapshot = await _healthMonitoringService.GetSystemHealthSnapshotAsync();
            await Clients.Caller.SendAsync("SystemHealthSnapshot", snapshot);
            
            // Send active alerts
            var activeAlerts = await _alertManagementService.GetActiveAlertsAsync();
            await Clients.Caller.SendAsync("ActiveAlerts", activeAlerts);
            
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Stream real-time health updates
        /// </summary>
        public async IAsyncEnumerable<SystemHealthSnapshot> StreamHealthUpdates(
            int intervalSeconds = 5,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var snapshot = await _healthMonitoringService.GetSystemHealthSnapshotAsync();
                yield return snapshot;
                
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            }
        }

        /// <summary>
        /// Stream real-time alerts
        /// </summary>
        public ChannelReader<HealthAlert> StreamAlerts(CancellationToken cancellationToken = default)
        {
            var channel = Channel.CreateUnbounded<HealthAlert>();
            
            _ = Task.Run(async () =>
            {
                await foreach (var alert in _alertManagementService.GetAlertStreamAsync(cancellationToken))
                {
                    await channel.Writer.WriteAsync(alert, cancellationToken);
                }
                
                channel.Writer.Complete();
            }, cancellationToken);
            
            return channel.Reader;
        }

        /// <summary>
        /// Get component health details
        /// </summary>
        public async Task<ComponentHealth> GetComponentHealth(string componentName)
        {
            return await _healthMonitoringService.GetComponentHealthAsync(componentName);
        }

        /// <summary>
        /// Acknowledge an alert
        /// </summary>
        public async Task<bool> AcknowledgeAlert(string alertId, string? notes = null)
        {
            var user = Context.User?.Identity?.Name ?? "Unknown";
            var success = await _alertManagementService.AcknowledgeAlertAsync(alertId, user, notes);
            
            if (success)
            {
                // Notify all clients about the acknowledgment
                await Clients.All.SendAsync("AlertAcknowledged", new
                {
                    AlertId = alertId,
                    AcknowledgedBy = user,
                    AcknowledgedAt = DateTime.UtcNow,
                    Notes = notes
                });
            }
            
            return success;
        }

        /// <summary>
        /// Resolve an alert
        /// </summary>
        public async Task<bool> ResolveAlert(string alertId, string? resolution = null)
        {
            var user = Context.User?.Identity?.Name ?? "Unknown";
            var success = await _alertManagementService.ResolveAlertAsync(alertId, user, resolution);
            
            if (success)
            {
                // Notify all clients about the resolution
                await Clients.All.SendAsync("AlertResolved", new
                {
                    AlertId = alertId,
                    ResolvedBy = user,
                    ResolvedAt = DateTime.UtcNow,
                    Resolution = resolution
                });
            }
            
            return success;
        }

        /// <summary>
        /// Get alert history
        /// </summary>
        public async Task<List<AlertHistoryEntry>> GetAlertHistory(string alertId)
        {
            return await _alertManagementService.GetAlertHistoryAsync(alertId);
        }

        /// <summary>
        /// Create or update an alert rule
        /// </summary>
        public async Task<AlertRule> SaveAlertRule(AlertRule rule)
        {
            var savedRule = await _alertManagementService.SaveAlertRuleAsync(rule);
            
            // Notify all clients about the rule change
            await Clients.All.SendAsync("AlertRuleUpdated", savedRule);
            
            return savedRule;
        }

        /// <summary>
        /// Delete an alert rule
        /// </summary>
        public async Task<bool> DeleteAlertRule(string ruleId)
        {
            var success = await _alertManagementService.DeleteAlertRuleAsync(ruleId);
            
            if (success)
            {
                await Clients.All.SendAsync("AlertRuleDeleted", ruleId);
            }
            
            return success;
        }

        /// <summary>
        /// Get all alert rules
        /// </summary>
        public async Task<List<AlertRule>> GetAlertRules()
        {
            return await _alertManagementService.GetAlertRulesAsync();
        }

        /// <summary>
        /// Create an alert suppression
        /// </summary>
        public async Task<AlertSuppression> CreateAlertSuppression(AlertSuppression suppression)
        {
            suppression.CreatedBy = Context.User?.Identity?.Name ?? "Unknown";
            var created = await _alertManagementService.CreateSuppressionAsync(suppression);
            
            // Notify all clients about the new suppression
            await Clients.All.SendAsync("AlertSuppressionCreated", created);
            
            return created;
        }

        /// <summary>
        /// Cancel an alert suppression
        /// </summary>
        public async Task<bool> CancelAlertSuppression(string suppressionId)
        {
            var success = await _alertManagementService.CancelSuppressionAsync(suppressionId);
            
            if (success)
            {
                await Clients.All.SendAsync("AlertSuppressionCancelled", suppressionId);
            }
            
            return success;
        }

        /// <summary>
        /// Get active suppressions
        /// </summary>
        public async Task<List<AlertSuppression>> GetActiveSuppressions()
        {
            return await _alertManagementService.GetActiveSuppressionsAsync();
        }

        /// <summary>
        /// Test alert notification
        /// </summary>
        public async Task TestAlert(AlertSeverity severity, string message)
        {
            var testAlert = new HealthAlert
            {
                Severity = severity,
                Type = AlertType.Custom,
                Component = "Test",
                Title = "Test Alert",
                Message = message,
                Context = new Dictionary<string, object>
                {
                    ["TriggeredBy"] = Context.User?.Identity?.Name ?? "Unknown",
                    ["IsTest"] = true
                }
            };
            
            await _alertManagementService.TriggerAlertAsync(testAlert);
        }

        /// <summary>
        /// Get performance metrics history
        /// </summary>
        public async Task<List<PerformanceMetrics>> GetPerformanceHistory(
            DateTime startTime,
            DateTime endTime,
            int intervalMinutes = 5)
        {
            return await _healthMonitoringService.GetPerformanceHistoryAsync(
                startTime,
                endTime,
                TimeSpan.FromMinutes(intervalMinutes));
        }

        /// <summary>
        /// Get resource metrics history
        /// </summary>
        public async Task<List<ResourceMetrics>> GetResourceHistory(
            DateTime startTime,
            DateTime endTime,
            int intervalMinutes = 5)
        {
            return await _healthMonitoringService.GetResourceHistoryAsync(
                startTime,
                endTime,
                TimeSpan.FromMinutes(intervalMinutes));
        }

        /// <summary>
        /// Force a health check on specific component
        /// </summary>
        public async Task<ComponentHealth> ForceHealthCheck(string componentName)
        {
            var health = await _healthMonitoringService.ForceHealthCheckAsync(componentName);
            
            // Notify all clients about the updated health status
            await Clients.All.SendAsync("ComponentHealthUpdated", health);
            
            return health;
        }

        /// <summary>
        /// Subscribe to specific component health updates
        /// </summary>
        public async Task SubscribeToComponent(string componentName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"component:{componentName}");
            _logger.LogInformation("Client {ConnectionId} subscribed to component {Component}", 
                Context.ConnectionId, componentName);
        }

        /// <summary>
        /// Unsubscribe from component health updates
        /// </summary>
        public async Task UnsubscribeFromComponent(string componentName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"component:{componentName}");
            _logger.LogInformation("Client {ConnectionId} unsubscribed from component {Component}", 
                Context.ConnectionId, componentName);
        }

        /// <summary>
        /// Subscribe to alerts of specific severity
        /// </summary>
        public async Task SubscribeToAlertSeverity(AlertSeverity severity)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"severity:{severity}");
            _logger.LogInformation("Client {ConnectionId} subscribed to {Severity} alerts", 
                Context.ConnectionId, severity);
        }

        /// <summary>
        /// Unsubscribe from alert severity
        /// </summary>
        public async Task UnsubscribeFromAlertSeverity(AlertSeverity severity)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"severity:{severity}");
            _logger.LogInformation("Client {ConnectionId} unsubscribed from {Severity} alerts", 
                Context.ConnectionId, severity);
        }

        /// <summary>
        /// Called when a client disconnects
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Health monitoring client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }

    /// <summary>
    /// Health monitoring service interface
    /// </summary>
    public interface IHealthMonitoringService
    {
        Task<SystemHealthSnapshot> GetSystemHealthSnapshotAsync();
        Task<ComponentHealth> GetComponentHealthAsync(string componentName);
        Task<ComponentHealth> ForceHealthCheckAsync(string componentName);
        Task<List<PerformanceMetrics>> GetPerformanceHistoryAsync(DateTime start, DateTime end, TimeSpan interval);
        Task<List<ResourceMetrics>> GetResourceHistoryAsync(DateTime start, DateTime end, TimeSpan interval);
    }

    /// <summary>
    /// Alert management service interface
    /// </summary>
    public interface IAlertManagementService
    {
        Task<List<HealthAlert>> GetActiveAlertsAsync();
        IAsyncEnumerable<HealthAlert> GetAlertStreamAsync(CancellationToken cancellationToken);
        Task<bool> AcknowledgeAlertAsync(string alertId, string user, string? notes);
        Task<bool> ResolveAlertAsync(string alertId, string user, string? resolution);
        Task<List<AlertHistoryEntry>> GetAlertHistoryAsync(string alertId);
        Task<AlertRule> SaveAlertRuleAsync(AlertRule rule);
        Task<bool> DeleteAlertRuleAsync(string ruleId);
        Task<List<AlertRule>> GetAlertRulesAsync();
        Task<AlertSuppression> CreateSuppressionAsync(AlertSuppression suppression);
        Task<bool> CancelSuppressionAsync(string suppressionId);
        Task<List<AlertSuppression>> GetActiveSuppressionsAsync();
        Task TriggerAlertAsync(HealthAlert alert);
    }
}