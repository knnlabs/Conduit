using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Http.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for managing health alerts and notifications
    /// </summary>
    public class AlertManagementService : IAlertManagementService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<AlertManagementService> _logger;
        private readonly IHubContext<HealthMonitoringHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, HealthAlert> _activeAlerts;
        private readonly ConcurrentDictionary<string, AlertRule> _alertRules;
        private readonly ConcurrentDictionary<string, AlertSuppression> _suppressions;
        private readonly Channel<HealthAlert> _alertChannel;

        public AlertManagementService(
            IMemoryCache cache,
            ILogger<AlertManagementService> logger,
            IHubContext<HealthMonitoringHub> hubContext,
            IServiceProvider serviceProvider)
        {
            _cache = cache;
            _logger = logger;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
            _activeAlerts = new ConcurrentDictionary<string, HealthAlert>();
            _alertRules = new ConcurrentDictionary<string, AlertRule>();
            _suppressions = new ConcurrentDictionary<string, AlertSuppression>();
            _alertChannel = Channel.CreateUnbounded<HealthAlert>();

            // Load existing data from cache
            LoadFromCache();
        }

        /// <summary>
        /// Get all active alerts
        /// </summary>
        public Task<List<HealthAlert>> GetActiveAlertsAsync()
        {
            var activeAlerts = _activeAlerts.Values
                .Where(a => a.State == AlertState.Active || a.State == AlertState.Acknowledged)
                .OrderByDescending(a => a.Severity)
                .ThenByDescending(a => a.TriggeredAt)
                .ToList();

            return Task.FromResult(activeAlerts);
        }

        /// <summary>
        /// Get real-time alert stream
        /// </summary>
        public async IAsyncEnumerable<HealthAlert> GetAlertStreamAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var alert in _alertChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return alert;
            }
        }

        /// <summary>
        /// Acknowledge an alert
        /// </summary>
        public async Task<bool> AcknowledgeAlertAsync(string alertId, string user, string? notes)
        {
            if (_activeAlerts.TryGetValue(alertId, out var alert))
            {
                alert.State = AlertState.Acknowledged;
                alert.IsAcknowledged = true;
                alert.AcknowledgedBy = user;
                alert.AcknowledgedAt = DateTime.UtcNow;
                alert.LastUpdated = DateTime.UtcNow;

                // Add to history
                await AddHistoryEntryAsync(alertId, "Acknowledged", user, notes);

                // Update cache
                SaveToCache();

                _logger.LogInformation("Alert {AlertId} acknowledged by {User}", alertId, user);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolve an alert
        /// </summary>
        public async Task<bool> ResolveAlertAsync(string alertId, string user, string? resolution)
        {
            if (_activeAlerts.TryGetValue(alertId, out var alert))
            {
                alert.State = AlertState.Resolved;
                alert.ResolvedAt = DateTime.UtcNow;
                alert.LastUpdated = DateTime.UtcNow;

                // Add to history
                await AddHistoryEntryAsync(alertId, "Resolved", user, resolution);

                // Remove from active alerts
                _activeAlerts.TryRemove(alertId, out _);

                // Update cache
                SaveToCache();

                _logger.LogInformation("Alert {AlertId} resolved by {User}", alertId, user);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get alert history
        /// </summary>
        public Task<List<AlertHistoryEntry>> GetAlertHistoryAsync(string alertId)
        {
            var history = _cache.Get<List<AlertHistoryEntry>>($"alert_history_{alertId}") ?? new List<AlertHistoryEntry>();
            return Task.FromResult(history);
        }

        /// <summary>
        /// Save or update an alert rule
        /// </summary>
        public Task<AlertRule> SaveAlertRuleAsync(AlertRule rule)
        {
            if (string.IsNullOrEmpty(rule.Id))
            {
                rule.Id = Guid.NewGuid().ToString();
            }

            _alertRules[rule.Id] = rule;
            SaveToCache();

            _logger.LogInformation("Alert rule {RuleId} saved: {RuleName}", rule.Id, rule.Name);
            return Task.FromResult(rule);
        }

        /// <summary>
        /// Delete an alert rule
        /// </summary>
        public Task<bool> DeleteAlertRuleAsync(string ruleId)
        {
            var removed = _alertRules.TryRemove(ruleId, out _);
            if (removed)
            {
                SaveToCache();
                _logger.LogInformation("Alert rule {RuleId} deleted", ruleId);
            }
            return Task.FromResult(removed);
        }

        /// <summary>
        /// Get all alert rules
        /// </summary>
        public Task<List<AlertRule>> GetAlertRulesAsync()
        {
            var rules = _alertRules.Values
                .OrderBy(r => r.Component)
                .ThenBy(r => r.Name)
                .ToList();
            return Task.FromResult(rules);
        }

        /// <summary>
        /// Create alert suppression
        /// </summary>
        public Task<AlertSuppression> CreateSuppressionAsync(AlertSuppression suppression)
        {
            if (string.IsNullOrEmpty(suppression.Id))
            {
                suppression.Id = Guid.NewGuid().ToString();
            }

            _suppressions[suppression.Id] = suppression;
            SaveToCache();

            _logger.LogInformation("Alert suppression {SuppressionId} created by {User}", 
                suppression.Id, suppression.CreatedBy);
            return Task.FromResult(suppression);
        }

        /// <summary>
        /// Cancel alert suppression
        /// </summary>
        public Task<bool> CancelSuppressionAsync(string suppressionId)
        {
            if (_suppressions.TryGetValue(suppressionId, out var suppression))
            {
                suppression.IsActive = false;
                SaveToCache();
                _logger.LogInformation("Alert suppression {SuppressionId} cancelled", suppressionId);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// Get active suppressions
        /// </summary>
        public Task<List<AlertSuppression>> GetActiveSuppressionsAsync()
        {
            var now = DateTime.UtcNow;
            var activeSuppressions = _suppressions.Values
                .Where(s => s.IsActive && s.StartTime <= now && s.EndTime > now)
                .OrderBy(s => s.StartTime)
                .ToList();
            return Task.FromResult(activeSuppressions);
        }

        /// <summary>
        /// Trigger a new alert
        /// </summary>
        public async Task TriggerAlertAsync(HealthAlert alert)
        {
            // Check if alert should be suppressed
            if (await IsAlertSuppressedAsync(alert))
            {
                _logger.LogDebug("Alert suppressed: {AlertTitle}", alert.Title);
                return;
            }

            // Check for duplicate alerts
            var existingAlert = _activeAlerts.Values
                .FirstOrDefault(a => a.Component == alert.Component && 
                                   a.Type == alert.Type && 
                                   a.Title == alert.Title &&
                                   a.State != AlertState.Resolved);

            if (existingAlert != null)
            {
                // Update occurrence count
                existingAlert.OccurrenceCount++;
                existingAlert.LastUpdated = DateTime.UtcNow;
                existingAlert.Message = alert.Message; // Update with latest message
                
                _logger.LogDebug("Updated existing alert {AlertId}, occurrence count: {Count}", 
                    existingAlert.Id, existingAlert.OccurrenceCount);
            }
            else
            {
                // Add new alert
                _activeAlerts[alert.Id] = alert;
                
                // Send to alert channel for real-time streaming
                await _alertChannel.Writer.WriteAsync(alert);

                // Send SignalR notification based on severity
                var severityGroup = $"severity:{alert.Severity}";
                await _hubContext.Clients.Group(severityGroup).SendAsync("NewAlert", alert);

                // Send to component subscribers
                var componentGroup = $"component:{alert.Component}";
                await _hubContext.Clients.Group(componentGroup).SendAsync("ComponentAlert", alert);

                _logger.LogWarning("New alert triggered: {AlertTitle} - {AlertMessage}", 
                    alert.Title, alert.Message);
                
                // Send notification through configured channels
                try
                {
                    // Use service provider to get optional services
                    var batchingService = _serviceProvider.GetService<AlertBatchingService>();
                    if (batchingService != null)
                    {
                        // Queue for batched delivery
                        batchingService.QueueAlert(alert);
                    }
                    else
                    {
                        // Send immediately if no batching service
                        var notificationService = _serviceProvider.GetService<IAlertNotificationService>();
                        if (notificationService != null)
                        {
                            await notificationService.SendAlertAsync(alert);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send alert notification for {AlertId}", alert.Id);
                }
            }

            SaveToCache();
        }

        private async Task<bool> IsAlertSuppressedAsync(HealthAlert alert)
        {
            var now = DateTime.UtcNow;
            var activeSuppressions = await GetActiveSuppressionsAsync();

            foreach (var suppression in activeSuppressions)
            {
                // Simple pattern matching - could be enhanced with regex
                if (suppression.AlertPattern == "*" ||
                    alert.Component.Contains(suppression.AlertPattern, StringComparison.OrdinalIgnoreCase) ||
                    alert.Title.Contains(suppression.AlertPattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task AddHistoryEntryAsync(string alertId, string action, string? user, string? notes)
        {
            var history = await GetAlertHistoryAsync(alertId);
            history.Add(new AlertHistoryEntry
            {
                AlertId = alertId,
                Action = action,
                User = user,
                Timestamp = DateTime.UtcNow,
                Notes = notes
            });

            _cache.Set($"alert_history_{alertId}", history, TimeSpan.FromDays(30));
        }

        private void LoadFromCache()
        {
            var activeAlerts = _cache.Get<List<HealthAlert>>("active_alerts");
            if (activeAlerts != null)
            {
                foreach (var alert in activeAlerts)
                {
                    _activeAlerts[alert.Id] = alert;
                }
            }

            var rules = _cache.Get<List<AlertRule>>("alert_rules");
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    _alertRules[rule.Id] = rule;
                }
            }

            var suppressions = _cache.Get<List<AlertSuppression>>("alert_suppressions");
            if (suppressions != null)
            {
                foreach (var suppression in suppressions)
                {
                    _suppressions[suppression.Id] = suppression;
                }
            }
        }

        private void SaveToCache()
        {
            _cache.Set("active_alerts", _activeAlerts.Values.ToList(), TimeSpan.FromDays(7));
            _cache.Set("alert_rules", _alertRules.Values.ToList(), TimeSpan.FromDays(30));
            _cache.Set("alert_suppressions", _suppressions.Values.ToList(), TimeSpan.FromDays(30));
        }
    }
}