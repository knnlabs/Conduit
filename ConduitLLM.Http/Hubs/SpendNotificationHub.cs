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
    /// SignalR hub for real-time spend tracking notifications.
    /// Extends SecureHub to require virtual key authentication.
    /// </summary>
    public class SpendNotificationHub : SecureHub
    {
        private readonly ISignalRMetrics _metrics;
        private readonly ILogger<SpendNotificationHub> _logger;
        
        // Track alert cooldowns to prevent spam
        private static readonly ConcurrentDictionary<string, AlertCooldown> _alertCooldowns = new();
        
        // Budget thresholds to monitor
        private static readonly decimal[] BudgetThresholds = { 0.5m, 0.8m, 0.9m, 1.0m };

        /// <summary>
        /// Initializes a new instance of the <see cref="SpendNotificationHub"/> class.
        /// </summary>
        /// <param name="metrics">SignalR metrics collector.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="serviceProvider">Service provider for dependency injection.</param>
        public SpendNotificationHub(
            ISignalRMetrics metrics,
            ILogger<SpendNotificationHub> logger,
            IServiceProvider serviceProvider) : base(logger, serviceProvider)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the hub name for logging and metrics.
        /// </summary>
        /// <returns>The hub name.</returns>
        protected override string GetHubName() => "SpendNotificationHub";

        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            
            var virtualKeyId = GetVirtualKeyId();
            if (virtualKeyId.HasValue)
            {
                // Initialize alert cooldown tracking for this virtual key
                var cooldownKey = $"vkey-{virtualKeyId.Value}";
                _alertCooldowns.TryAdd(cooldownKey, new AlertCooldown());
                
                _logger.LogInformation(
                    "Client connected to SpendNotificationHub: {ConnectionId} for VirtualKey: {VirtualKeyId}",
                    Context.ConnectionId,
                    virtualKeyId.Value);
            }
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        /// <param name="exception">The exception that caused the disconnect, if any.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Clean up cooldowns if no more connections for this virtual key
            var virtualKeyId = GetVirtualKeyId();
            if (virtualKeyId.HasValue)
            {
                var cooldownKey = $"vkey-{virtualKeyId.Value}";
                // In production, we'd check if there are other connections before removing
                // For now, we'll keep the cooldown to prevent issues
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Broadcasts a spend update to the virtual key's group.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="notification">The spend update notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendSpendUpdate(int virtualKeyId, SpendUpdateNotification notification)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["VirtualKeyId"] = virtualKeyId,
                ["NewSpend"] = notification.NewSpend,
                ["TotalSpend"] = notification.TotalSpend
            }))
            {
                try
                {
                    var groupName = $"vkey-{virtualKeyId}";
                    
                    // Check for budget alerts
                    if (notification.Budget.HasValue && notification.BudgetPercentage.HasValue)
                    {
                        await CheckAndSendBudgetAlerts(virtualKeyId, notification);
                    }
                    
                    // Send the spend update
                    await Clients.Group(groupName).SendAsync("SpendUpdate", notification);
                    
                    // Track metrics
                    _metrics.MessagesSent.Add(1, 
                        new("hub", "SpendNotificationHub"), 
                        new("message_type", "spend_update"));
                    
                    _logger.LogInformation(
                        "Sent spend update to group {GroupName}: ${NewSpend:F2} (Total: ${TotalSpend:F2})",
                        groupName,
                        notification.NewSpend,
                        notification.TotalSpend);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1, 
                        new("hub", "SpendNotificationHub"), 
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error sending spend update");
                    throw;
                }
            }
        }

        /// <summary>
        /// Sends a budget alert to the virtual key's group.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="alert">The budget alert notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendBudgetAlert(int virtualKeyId, BudgetAlertNotification alert)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["VirtualKeyId"] = virtualKeyId,
                ["AlertSeverity"] = alert.Severity,
                ["BudgetPercentage"] = alert.PercentageUsed
            }))
            {
                try
                {
                    var groupName = $"vkey-{virtualKeyId}";
                    
                    await Clients.Group(groupName).SendAsync("BudgetAlert", alert);
                    
                    _metrics.MessagesSent.Add(1, 
                        new("hub", "SpendNotificationHub"), 
                        new("message_type", "budget_alert"));
                    
                    _logger.LogWarning(
                        "Sent budget alert to group {GroupName}: {Percentage:F0}% used, Severity: {Severity}",
                        groupName,
                        alert.PercentageUsed,
                        alert.Severity);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1, 
                        new("hub", "SpendNotificationHub"), 
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error sending budget alert");
                    throw;
                }
            }
        }

        /// <summary>
        /// Sends a spend summary to the virtual key's group.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="summary">The spend summary notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendSpendSummary(int virtualKeyId, SpendSummaryNotification summary)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["VirtualKeyId"] = virtualKeyId,
                ["PeriodType"] = summary.PeriodType,
                ["TotalSpend"] = summary.TotalSpend
            }))
            {
                try
                {
                    var groupName = $"vkey-{virtualKeyId}";
                    
                    await Clients.Group(groupName).SendAsync("SpendSummary", summary);
                    
                    _metrics.MessagesSent.Add(1, 
                        new("hub", "SpendNotificationHub"), 
                        new("message_type", "spend_summary"));
                    
                    _logger.LogInformation(
                        "Sent {PeriodType} spend summary to group {GroupName}: ${TotalSpend:F2}",
                        summary.PeriodType,
                        groupName,
                        summary.TotalSpend);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1, 
                        new("hub", "SpendNotificationHub"), 
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error sending spend summary");
                    throw;
                }
            }
        }

        /// <summary>
        /// Sends an unusual spending detection notification to the virtual key's group.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="notification">The unusual spending notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendUnusualSpendingAlert(int virtualKeyId, UnusualSpendingNotification notification)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["VirtualKeyId"] = virtualKeyId,
                ["ActivityType"] = notification.ActivityType,
                ["DeviationPercentage"] = notification.DeviationPercentage
            }))
            {
                try
                {
                    var groupName = $"vkey-{virtualKeyId}";
                    
                    await Clients.Group(groupName).SendAsync("UnusualSpendingDetected", notification);
                    
                    _metrics.MessagesSent.Add(1, 
                        new("hub", "SpendNotificationHub"), 
                        new("message_type", "unusual_spending"));
                    
                    _logger.LogWarning(
                        "Sent unusual spending alert to group {GroupName}: {PatternType} - {PercentageIncrease:F0}% increase",
                        groupName,
                        notification.ActivityType,
                        notification.DeviationPercentage);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1, 
                        new("hub", "SpendNotificationHub"), 
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error sending unusual spending alert");
                    throw;
                }
            }
        }

        /// <summary>
        /// Checks budget thresholds and sends alerts if needed.
        /// </summary>
        private async Task CheckAndSendBudgetAlerts(int virtualKeyId, SpendUpdateNotification notification)
        {
            if (!notification.Budget.HasValue || !notification.BudgetPercentage.HasValue)
                return;

            var cooldownKey = $"vkey-{virtualKeyId}";
            if (!_alertCooldowns.TryGetValue(cooldownKey, out var cooldown))
            {
                cooldown = new AlertCooldown();
                _alertCooldowns.TryAdd(cooldownKey, cooldown);
            }

            var percentage = notification.BudgetPercentage.Value;
            var remaining = notification.Budget.Value - notification.TotalSpend;

            foreach (var threshold in BudgetThresholds)
            {
                var thresholdPercentage = threshold * 100;
                
                if (percentage >= thresholdPercentage && !cooldown.HasAlertBeenSent(thresholdPercentage))
                {
                    var severity = threshold switch
                    {
                        >= 1.0m => "critical",
                        >= 0.9m => "warning",
                        _ => "info"
                    };

                    var message = threshold switch
                    {
                        >= 1.0m => "Budget limit reached! Further requests may be blocked.",
                        >= 0.9m => $"Warning: {thresholdPercentage:F0}% of budget used. Only ${remaining:F2} remaining.",
                        >= 0.8m => $"Alert: {thresholdPercentage:F0}% of budget used. ${remaining:F2} remaining.",
                        _ => $"Info: {thresholdPercentage:F0}% of budget used. ${remaining:F2} remaining."
                    };

                    var alert = new BudgetAlertNotification
                    {
                        PercentageUsed = (double)percentage,
                        CurrentSpend = notification.TotalSpend,
                        BudgetLimit = notification.Budget ?? 0,
                        Severity = severity,
                        Message = message,
                        BudgetPeriodEnd = DateTime.UtcNow.AddDays(30) // Simplified - should get from config
                    };

                    await SendBudgetAlert(virtualKeyId, alert);
                    cooldown.MarkAlertSent(thresholdPercentage);
                }
            }
        }

        /// <summary>
        /// Tracks alert cooldowns to prevent spam.
        /// </summary>
        private class AlertCooldown
        {
            private readonly ConcurrentDictionary<decimal, DateTime> _lastAlertTimes = new();
            private readonly TimeSpan _cooldownPeriod = TimeSpan.FromHours(1);

            public bool HasAlertBeenSent(decimal threshold)
            {
                if (_lastAlertTimes.TryGetValue(threshold, out var lastSent))
                {
                    return DateTime.UtcNow - lastSent < _cooldownPeriod;
                }
                return false;
            }

            public void MarkAlertSent(decimal threshold)
            {
                _lastAlertTimes[threshold] = DateTime.UtcNow;
            }

            public void Reset()
            {
                _lastAlertTimes.Clear();
            }
        }
    }
}