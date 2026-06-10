using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Distributed alert management service with Redis-based storage and deduplication
    /// </summary>
    public class DistributedAlertManagementService : IDistributedAlertManagementService, IHostedService, IDisposable
    {
        private readonly IDatabase _database;
        private readonly ILogger<DistributedAlertManagementService> _logger;
        private readonly IHubContext<HealthMonitoringHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        
        public string InstanceId { get; }
        
        // Redis keys
        private const string ActiveAlertsKey = "active_alerts";
        private const string AlertRulesKey = "alert_rules";
        private const string AlertSuppressionsKey = "alert_suppressions";
        private const string AlertHistoryPrefix = "alert_history";
        private const string InstancesSetKey = "alert_mgmt_instances";
        private const string AlertStreamKey = "alert_events_stream";
        private const string AlertLockPrefix = "alert_lock";
        
        private Timer? _heartbeatTimer;
        private Timer? _cleanupTimer;
        private readonly Channel<HealthAlert> _alertChannel;

        public DistributedAlertManagementService(
            IConnectionMultiplexer redis,
            ILogger<DistributedAlertManagementService> logger,
            IHubContext<HealthMonitoringHub> hubContext,
            IServiceProvider serviceProvider)
        {
            _database = redis.GetDatabase();
            _logger = logger;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
            InstanceId = Environment.MachineName + "_" + Environment.ProcessId + "_" + Guid.NewGuid().ToString("N")[..8];
            // Bounded so a stalled stream consumer cannot grow memory without limit;
            // the oldest (least relevant) alerts are dropped first.
            _alertChannel = Channel.CreateBounded<HealthAlert>(
                new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest },
                dropped => _logger.LogWarning("Alert stream buffer full; dropped alert {AlertId} ({Title})", dropped.Id, dropped.Title));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await RegisterInstanceAsync();
            
            _logger.LogInformation("Distributed alert management service started with instance ID: {InstanceId}", InstanceId);

            // Start heartbeat timer (every 30 seconds)
            _heartbeatTimer = new Timer(
                async _ => await UpdateHeartbeatAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));

            // Start cleanup timer (every 5 minutes)
            _cleanupTimer = new Timer(
                async _ => await CleanupExpiredDataAsync(),
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));

            // Start alert stream processor
            _ = Task.Run(ProcessAlertStreamAsync, cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Distributed alert management service stopping...");

            _heartbeatTimer?.Change(Timeout.Infinite, 0);
            _cleanupTimer?.Change(Timeout.Infinite, 0);

            await UnregisterInstanceAsync();
        }

        public async Task RegisterInstanceAsync()
        {
            var instanceData = new
            {
                InstanceId,
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                StartedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };

            await _database.HashSetAsync($"{InstancesSetKey}:{InstanceId}", "data", JsonSerializer.Serialize(instanceData));
            await _database.KeyExpireAsync($"{InstancesSetKey}:{InstanceId}", TimeSpan.FromMinutes(2));
        }

        public async Task UnregisterInstanceAsync()
        {
            await _database.KeyDeleteAsync($"{InstancesSetKey}:{InstanceId}");
        }

        public async Task UpdateHeartbeatAsync()
        {
            try
            {
                await _database.HashSetAsync($"{InstancesSetKey}:{InstanceId}", "last_heartbeat", DateTime.UtcNow.Ticks);
                await _database.KeyExpireAsync($"{InstancesSetKey}:{InstanceId}", TimeSpan.FromMinutes(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update heartbeat for instance {InstanceId}", InstanceId);
            }
        }

        public async Task<List<HealthAlert>> GetActiveAlertsAsync()
        {
            var activeAlerts = await _database.HashGetAllAsync(ActiveAlertsKey);
            var alerts = new List<HealthAlert>();

            foreach (var item in activeAlerts)
            {
                try
                {
                    var alert = JsonSerializer.Deserialize<HealthAlert>(item.Value.ToString());
                    if (alert?.State == AlertState.Active || alert?.State == AlertState.Acknowledged)
                    {
                        alerts.Add(alert);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize alert {AlertId}", item.Name);
                }
            }

            return [
                ..alerts
                    .OrderByDescending(a => a.Severity)
                    .ThenByDescending(a => a.TriggeredAt)
            ];
        }

        public async Task TriggerAlertAsync(HealthAlert alert)
        {
            // Check if alert should be suppressed
            if (await IsAlertSuppressedAsync(alert))
            {
                _logger.LogDebug("Alert suppressed: {AlertTitle}", LoggingSanitizer.S(alert.Title));
                return;
            }

            // Generate alert fingerprint for deduplication
            var fingerprint = GenerateAlertFingerprint(alert);
            var lockKey = $"{AlertLockPrefix}:{fingerprint}";

            // Use distributed lock to prevent duplicate alerts from multiple instances
            var lockValue = Guid.NewGuid().ToString();
            var lockAcquired = await _database.StringSetAsync(lockKey, lockValue, TimeSpan.FromMinutes(5), false, When.NotExists, CommandFlags.None);

            if (!lockAcquired)
            {
                _logger.LogDebug("Alert already being processed by another instance: {AlertTitle}", LoggingSanitizer.S(alert.Title));
                return;
            }

            try
            {
                // Check for existing alert with same fingerprint
                var existingAlert = await GetExistingAlertAsync(fingerprint);
                
                if (existingAlert != null)
                {
                    // Update existing alert
                    existingAlert.OccurrenceCount++;
                    existingAlert.LastUpdated = DateTime.UtcNow;
                    existingAlert.Message = alert.Message; // Update with latest message
                    
                    await _database.HashSetAsync(ActiveAlertsKey, existingAlert.Id, JsonSerializer.Serialize(existingAlert));
                    
                    _logger.LogDebug("Updated existing alert {AlertId}, occurrence count: {Count}", 
                        existingAlert.Id, existingAlert.OccurrenceCount);
                    
                    // Add to alert stream
                    await _database.StreamAddAsync(AlertStreamKey, "updated", JsonSerializer.Serialize(existingAlert));
                }
                else
                {
                    // Create new alert
                    alert.Id = alert.Id ?? Guid.NewGuid().ToString();
                    alert.Fingerprint = fingerprint;
                    alert.TriggeredAt = DateTime.UtcNow;
                    alert.LastUpdated = DateTime.UtcNow;
                    alert.State = AlertState.Active;
                    alert.OccurrenceCount = 1;
                    
                    await _database.HashSetAsync(ActiveAlertsKey, alert.Id, JsonSerializer.Serialize(alert));
                    
                    // Add to alert stream for real-time updates
                    await _database.StreamAddAsync(AlertStreamKey, "triggered", JsonSerializer.Serialize(alert));
                    await _database.StreamTrimAsync(AlertStreamKey, 1000, false); // Keep last 1000 events
                    
                    // Send to alert channel for local processing
                    await _alertChannel.Writer.WriteAsync(alert);
                    
                    _logger.LogWarning("New alert triggered: {AlertTitle} - {AlertMessage}", 
                        alert.Title, alert.Message);
                    
                    // Send SignalR notifications
                    await SendSignalRNotificationsAsync(alert);
                    
                    // Send external notifications
                    await SendExternalNotificationsAsync(alert);
                }
            }
            finally
            {
                // Release distributed lock
                const string script = @"
                    if redis.call('GET', KEYS[1]) == ARGV[1] then
                        return redis.call('DEL', KEYS[1])
                    else
                        return 0
                    end
                ";
                await _database.ScriptEvaluateAsync(script, new RedisKey[] { lockKey }, new RedisValue[] { lockValue });
            }
        }

        public async Task<bool> AcknowledgeAlertAsync(string alertId, string user, string? notes)
        {
            var alertData = await _database.HashGetAsync(ActiveAlertsKey, alertId);
            if (!alertData.HasValue) return false;

            var alert = JsonSerializer.Deserialize<HealthAlert>(alertData.ToString());
            if (alert == null) return false;

            alert.State = AlertState.Acknowledged;
            alert.IsAcknowledged = true;
            alert.AcknowledgedBy = user;
            alert.AcknowledgedAt = DateTime.UtcNow;
            alert.LastUpdated = DateTime.UtcNow;

            await _database.HashSetAsync(ActiveAlertsKey, alertId, JsonSerializer.Serialize(alert));
            
            // Add to history
            await AddHistoryEntryAsync(alertId, "Acknowledged", user, notes);
            
            // Add to stream
            await _database.StreamAddAsync(AlertStreamKey, "acknowledged", JsonSerializer.Serialize(alert));

            _logger.LogInformation("Alert {AlertId} acknowledged by {User}", alertId, user);
            return true;
        }

        public async Task<bool> ResolveAlertAsync(string alertId, string user, string? resolution)
        {
            var alertData = await _database.HashGetAsync(ActiveAlertsKey, alertId);
            if (!alertData.HasValue) return false;

            var alert = JsonSerializer.Deserialize<HealthAlert>(alertData.ToString());
            if (alert == null) return false;

            alert.State = AlertState.Resolved;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.LastUpdated = DateTime.UtcNow;

            // Add to history
            await AddHistoryEntryAsync(alertId, "Resolved", user, resolution);

            // Remove from active alerts
            await _database.HashDeleteAsync(ActiveAlertsKey, alertId);
            
            // Add to stream
            await _database.StreamAddAsync(AlertStreamKey, "resolved", JsonSerializer.Serialize(alert));

            _logger.LogInformation("Alert {AlertId} resolved by {User}", alertId, user);
            return true;
        }

        public async Task<List<AlertHistoryEntry>> GetAlertHistoryAsync(string alertId)
        {
            var historyKey = $"{AlertHistoryPrefix}:{alertId}";
            var history = await _database.ListRangeAsync(historyKey);
            
            var entries = new List<AlertHistoryEntry>();
            foreach (var item in history)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<AlertHistoryEntry>(item.ToString());
                    if (entry != null) entries.Add(entry);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize history entry for alert {AlertId}", alertId);
                }
            }
            
            return [..entries.OrderByDescending(e => e.Timestamp)];
        }

        public async Task<AlertRule> SaveAlertRuleAsync(AlertRule rule)
        {
            rule.Id = rule.Id ?? Guid.NewGuid().ToString();
            await _database.HashSetAsync(AlertRulesKey, rule.Id, JsonSerializer.Serialize(rule));
            
            _logger.LogInformation("Alert rule {RuleId} saved: {RuleName}", rule.Id, LoggingSanitizer.S(rule.Name));
            return rule;
        }

        public async Task<bool> DeleteAlertRuleAsync(string ruleId)
        {
            var deleted = await _database.HashDeleteAsync(AlertRulesKey, ruleId);
            if (deleted)
            {
                _logger.LogInformation("Alert rule {RuleId} deleted", ruleId);
            }
            return deleted;
        }

        public async Task<List<AlertRule>> GetAlertRulesAsync()
        {
            var rules = await _database.HashGetAllAsync(AlertRulesKey);
            var alertRules = new List<AlertRule>();

            foreach (var item in rules)
            {
                try
                {
                    var rule = JsonSerializer.Deserialize<AlertRule>(item.Value.ToString());
                    if (rule != null) alertRules.Add(rule);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize alert rule {RuleId}", item.Name);
                }
            }

            return [
                ..alertRules
                    .OrderBy(r => r.Component)
                    .ThenBy(r => r.Name)
            ];
        }

        public async Task<AlertSuppression> CreateSuppressionAsync(AlertSuppression suppression)
        {
            suppression.Id = suppression.Id ?? Guid.NewGuid().ToString();
            await _database.HashSetAsync(AlertSuppressionsKey, suppression.Id, JsonSerializer.Serialize(suppression));
            
            _logger.LogInformation("Alert suppression {SuppressionId} created by {User}", 
                suppression.Id, suppression.CreatedBy);
            return suppression;
        }

        public async Task<bool> CancelSuppressionAsync(string suppressionId)
        {
            var suppressionData = await _database.HashGetAsync(AlertSuppressionsKey, suppressionId);
            if (!suppressionData.HasValue) return false;

            var suppression = JsonSerializer.Deserialize<AlertSuppression>(suppressionData.ToString());
            if (suppression == null) return false;

            suppression.IsActive = false;
            await _database.HashSetAsync(AlertSuppressionsKey, suppressionId, JsonSerializer.Serialize(suppression));
            
            _logger.LogInformation("Alert suppression {SuppressionId} cancelled", suppressionId);
            return true;
        }

        public async Task<List<AlertSuppression>> GetActiveSuppressionsAsync()
        {
            var suppressions = await _database.HashGetAllAsync(AlertSuppressionsKey);
            var activeSuppressions = new List<AlertSuppression>();
            var now = DateTime.UtcNow;

            foreach (var item in suppressions)
            {
                try
                {
                    var suppression = JsonSerializer.Deserialize<AlertSuppression>(item.Value.ToString());
                    if (suppression?.IsActive == true && suppression.StartTime <= now && suppression.EndTime > now)
                    {
                        activeSuppressions.Add(suppression);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize suppression {SuppressionId}", item.Name);
                }
            }

            return [..activeSuppressions.OrderBy(s => s.StartTime)];
        }

        public async Task<List<string>> GetActiveInstancesAsync()
        {
            var pattern = $"{InstancesSetKey}:*";
            var server = _database.Multiplexer.GetPrimaryServer();
            var keys = server.Keys(pattern: pattern);
            
            var instances = new List<string>();
            var cutoffTime = DateTime.UtcNow.AddMinutes(-1);
            
            foreach (var key in keys)
            {
                var lastHeartbeat = await _database.HashGetAsync(key, "last_heartbeat");
                if (lastHeartbeat.HasValue)
                {
                    var heartbeatTime = new DateTime((long)lastHeartbeat);
                    if (heartbeatTime > cutoffTime)
                    {
                        var instanceId = key.ToString().Split(':').Last();
                        instances.Add(instanceId);
                    }
                }
            }
            
            return instances;
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

        private async Task<HealthAlert?> GetExistingAlertAsync(string fingerprint)
        {
            var activeAlerts = await _database.HashGetAllAsync(ActiveAlertsKey);
            
            foreach (var item in activeAlerts)
            {
                try
                {
                    var alert = JsonSerializer.Deserialize<HealthAlert>(item.Value.ToString());
                    if (alert?.Fingerprint == fingerprint && 
                        (alert.State == AlertState.Active || alert.State == AlertState.Acknowledged))
                    {
                        return alert;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize alert for fingerprint check: {Fingerprint}", fingerprint);
                }
            }
            
            return null;
        }

        private static string GenerateAlertFingerprint(HealthAlert alert)
        {
            // Create a unique fingerprint based on alert characteristics
            // This allows for deduplication of similar alerts
            var fingerprintData = $"{alert.Component}:{alert.Type}:{alert.Title}";
            
            // Add normalized message (remove dynamic values like timestamps, numbers)
            var normalizedMessage = NormalizeAlertMessage(alert.Message ?? "");
            fingerprintData += $":{normalizedMessage}";
            
            // Generate a hash of the fingerprint data
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(fingerprintData));
            return Convert.ToHexString(hashBytes)[..16]; // Use first 16 characters
        }

        private static string NormalizeAlertMessage(string message)
        {
            // Remove numbers, timestamps, and other dynamic content for better deduplication
            var normalized = System.Text.RegularExpressions.Regex.Replace(message, @"\d+", "N");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", "TIMESTAMP");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[\d.]+ms", "Nms");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[\d.]+%", "N%");
            return normalized;
        }

        private async Task<bool> IsAlertSuppressedAsync(HealthAlert alert)
        {
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
            var entry = new AlertHistoryEntry
            {
                AlertId = alertId,
                Action = action,
                User = user,
                Timestamp = DateTime.UtcNow,
                Notes = notes
            };

            var historyKey = $"{AlertHistoryPrefix}:{alertId}";
            await _database.ListLeftPushAsync(historyKey, JsonSerializer.Serialize(entry));
            await _database.ListTrimAsync(historyKey, 0, 99); // Keep last 100 entries
            await _database.KeyExpireAsync(historyKey, TimeSpan.FromDays(30));
        }

        private async Task SendSignalRNotificationsAsync(HealthAlert alert)
        {
            try
            {
                // Send to severity group
                var severityGroup = $"severity:{alert.Severity}";
                await _hubContext.Clients.Group(severityGroup).SendAsync("NewAlert", alert);

                // Send to component subscribers
                var componentGroup = $"component:{alert.Component}";
                await _hubContext.Clients.Group(componentGroup).SendAsync("ComponentAlert", alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SignalR notifications for alert {AlertId}", alert.Id);
            }
        }

        private async Task SendExternalNotificationsAsync(HealthAlert alert)
        {
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
                _logger.LogError(ex, "Failed to send external notifications for alert {AlertId}", alert.Id);
            }
        }

        private async Task ProcessAlertStreamAsync()
        {
            try
            {
                // Process alerts from Redis stream for cross-instance coordination
                var lastId = "0-0";
                
                while (true)
                {
                    try
                    {
                        var entries = await _database.StreamReadAsync(AlertStreamKey, lastId, 10);
                        
                        foreach (var entry in entries)
                        {
                            lastId = entry.Id;
                            
                            // Process stream event (could be used for additional coordination)
                            var eventType = entry.Values[0].Name;
                            var alertData = entry.Values[0].Value;
                            
                            _logger.LogDebug("Processed alert stream event: {EventType}", eventType);
                        }
                        
                        if (entries.Length == 0)
                        {
                            await Task.Delay(1000); // Wait if no new entries
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing alert stream, backing off for 5s");
                        await Task.Delay(5000); // Back off on errors
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Alert stream processor stopped");
            }
        }

        private async Task CleanupExpiredDataAsync()
        {
            try
            {
                // Clean up expired suppressions
                var suppressions = await _database.HashGetAllAsync(AlertSuppressionsKey);
                var now = DateTime.UtcNow;
                
                foreach (var item in suppressions)
                {
                    try
                    {
                        var suppression = JsonSerializer.Deserialize<AlertSuppression>(item.Value.ToString());
                        if (suppression != null && suppression.EndTime < now)
                        {
                            await _database.HashDeleteAsync(AlertSuppressionsKey, item.Name);
                            _logger.LogDebug("Cleaned up expired suppression {SuppressionId}", item.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process suppression cleanup for {SuppressionId}", item.Name);
                    }
                }
                
                // Clean up old alert history
                var historyPattern = $"{AlertHistoryPrefix}:*";
                var server = _database.Multiplexer.GetPrimaryServer();
                var historyKeys = server.Keys(pattern: historyPattern);
                
                foreach (var key in historyKeys)
                {
                    var ttl = await _database.KeyTimeToLiveAsync(key);
                    if (!ttl.HasValue)
                    {
                        // Set expiry for keys that don't have one
                        await _database.KeyExpireAsync(key, TimeSpan.FromDays(30));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _alertChannel.Writer.Complete();
        }
    }
}