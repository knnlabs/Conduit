using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConduitLLM.Admin.Hubs;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Alerts;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service that listens for cache monitoring alerts and sends SignalR notifications
    /// </summary>
    public class CacheAlertNotificationService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Hubs.AdminNotificationService _notificationService;
        private readonly ILogger<CacheAlertNotificationService> _logger;
        private ICacheMonitoringService? _monitoringService;
        private ConduitLLM.Core.Interfaces.ICacheStatisticsCollector? _statisticsCollector;
        private readonly Dictionary<string, DateTime> _alertCooldowns = new();
        private readonly SemaphoreSlim _cooldownLock = new(1, 1);

        public CacheAlertNotificationService(
            IServiceProvider serviceProvider,
            Hubs.AdminNotificationService notificationService,
            ILogger<CacheAlertNotificationService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Get the services from DI
            using var scope = _serviceProvider.CreateScope();
            
            // Subscribe to monitoring service alerts (new system)
            _monitoringService = scope.ServiceProvider.GetService<ICacheMonitoringService>();
            if (_monitoringService != null)
            {
                _monitoringService.CacheAlertTriggered += OnMonitoringServiceAlertTriggered;
                _logger.LogInformation("Subscribed to cache monitoring service alerts");
            }
            
            // Subscribe to statistics collector alerts (existing system)
            _statisticsCollector = scope.ServiceProvider.GetService<ConduitLLM.Core.Interfaces.ICacheStatisticsCollector>();
            if (_statisticsCollector != null)
            {
                _statisticsCollector.AlertTriggered += OnStatisticsCollectorAlertTriggered;
                _logger.LogInformation("Subscribed to cache statistics collector alerts");
            }

            if (_monitoringService == null && _statisticsCollector == null)
            {
                _logger.LogWarning("Neither cache monitoring service nor statistics collector available, cache alerts will not be sent");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_monitoringService != null)
            {
                _monitoringService.CacheAlertTriggered -= OnMonitoringServiceAlertTriggered;
            }
            
            if (_statisticsCollector != null)
            {
                _statisticsCollector.AlertTriggered -= OnStatisticsCollectorAlertTriggered;
            }

            _logger.LogInformation("Cache alert notification service stopped");
            return Task.CompletedTask;
        }

        private async void OnMonitoringServiceAlertTriggered(object? sender, Core.Services.CacheAlertEventArgs e)
        {
            // Handle alerts from the new monitoring service
            try
            {
                // Check cooldown
                if (!await ShouldSendAlert(e))
                {
                    _logger.LogDebug("Cache alert suppressed due to cooldown: {AlertType}", e.AlertType);
                    return;
                }

                // Map to cache alert type
                var alertType = CacheAlertDefinitions.ParseAlertType(e.AlertType);
                if (alertType.HasValue)
                {
                    var definition = CacheAlertDefinitions.GetDefinition(alertType.Value);
                    if (definition.NotificationEnabled)
                    {
                        await SendCacheAlertNotification(e, definition);
                    }
                }
                else
                {
                    // Send generic cache alert
                    await SendGenericCacheAlert(e);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing monitoring service alert notification");
            }
        }

        private async void OnStatisticsCollectorAlertTriggered(object? sender, ConduitLLM.Core.Models.CacheAlertEventArgs e)
        {
            try
            {
                // The event from ICacheStatisticsCollector uses Core.Models types
                if (e.Alert == null)
                {
                    _logger.LogWarning("Received cache alert event with null alert");
                    return;
                }

                // Convert to our monitoring service format
                var alert = new Core.Services.CacheAlertEventArgs
                {
                    AlertType = e.Alert.AlertType.ToString(),
                    Message = e.Alert.Message,
                    Severity = MapSeverity(e.Alert.Severity),
                    Region = e.Alert.Region.ToString(),
                    Details = new Dictionary<string, object>
                    {
                        ["currentValue"] = e.Alert.CurrentValue,
                        ["thresholdValue"] = e.Alert.ThresholdValue,
                        ["isNew"] = e.IsNew,
                        ["isResolved"] = e.IsResolved
                    },
                    Timestamp = e.Alert.TriggeredAt
                };

                // Check cooldown
                if (!await ShouldSendAlert(alert))
                {
                    _logger.LogDebug("Cache alert suppressed due to cooldown: {AlertType}", alert.AlertType);
                    return;
                }

                // Map to cache alert type
                var alertType = CacheAlertDefinitions.ParseAlertType(alert.AlertType);
                if (alertType.HasValue)
                {
                    var definition = CacheAlertDefinitions.GetDefinition(alertType.Value);
                    if (definition.NotificationEnabled)
                    {
                        await SendCacheAlertNotification(alert, definition);
                    }
                }
                else
                {
                    // Send generic cache alert
                    await SendGenericCacheAlert(alert);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cache alert notification");
            }
        }

        private string MapSeverity(AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Critical => "critical",
                AlertSeverity.Error => "error",
                AlertSeverity.Warning => "warning",
                AlertSeverity.Info => "info",
                _ => "warning"
            };
        }

        private async Task<bool> ShouldSendAlert(Core.Services.CacheAlertEventArgs alert)
        {
            var alertKey = $"{alert.AlertType}:{alert.Region ?? "global"}";
            
            await _cooldownLock.WaitAsync();
            try
            {
                if (_alertCooldowns.TryGetValue(alertKey, out var lastSent))
                {
                    var alertType = CacheAlertDefinitions.ParseAlertType(alert.AlertType);
                    var cooldownPeriod = alertType.HasValue 
                        ? CacheAlertDefinitions.GetDefinition(alertType.Value).CooldownPeriod
                        : TimeSpan.FromMinutes(5);

                    if (DateTime.UtcNow - lastSent < cooldownPeriod)
                    {
                        return false;
                    }
                }

                _alertCooldowns[alertKey] = DateTime.UtcNow;
                return true;
            }
            finally
            {
                _cooldownLock.Release();
            }
        }

        private async Task SendCacheAlertNotification(Core.Services.CacheAlertEventArgs alert, CacheAlertDefinition definition)
        {
            try
            {
                // Map severity
                var priority = definition.DefaultSeverity switch
                {
                    CacheAlertSeverity.Critical => NotificationPriority.Critical,
                    CacheAlertSeverity.Error => NotificationPriority.High,
                    CacheAlertSeverity.Warning => NotificationPriority.Medium,
                    _ => NotificationPriority.Low
                };

                // Build enhanced message
                var message = $"Cache Alert: {definition.Name}";
                if (!string.IsNullOrEmpty(alert.Region))
                {
                    message += $" (Region: {alert.Region})";
                }
                message += $" - {alert.Message}";

                // Create notification with cache-specific details
                var notification = new
                {
                    AlertType = alert.AlertType,
                    Title = definition.Name,
                    Message = message,
                    Description = definition.Description,
                    Severity = alert.Severity,
                    Region = alert.Region,
                    Timestamp = alert.Timestamp,
                    Details = alert.Details,
                    RecommendedActions = definition.RecommendedActions,
                    Priority = priority
                };

                // Send as system announcement with cache category
                await _notificationService.NotifySystemAnnouncement(
                    message, 
                    priority, 
                    "cache-alert");

                // Also send detailed notification through a custom event
                await SendDetailedCacheAlert(notification);

                _logger.LogInformation(
                    "Sent cache alert notification: {AlertType} - {Message} (Priority: {Priority})",
                    alert.AlertType, alert.Message, priority);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending cache alert notification");
            }
        }

        private async Task SendGenericCacheAlert(Core.Services.CacheAlertEventArgs alert)
        {
            try
            {
                var priority = alert.Severity switch
                {
                    "critical" => NotificationPriority.Critical,
                    "error" => NotificationPriority.High,
                    "warning" => NotificationPriority.Medium,
                    _ => NotificationPriority.Low
                };

                var message = $"Cache Alert: {alert.AlertType}";
                if (!string.IsNullOrEmpty(alert.Region))
                {
                    message += $" (Region: {alert.Region})";
                }
                message += $" - {alert.Message}";

                await _notificationService.NotifySystemAnnouncement(
                    message,
                    priority,
                    "cache-alert");

                _logger.LogInformation(
                    "Sent generic cache alert notification: {AlertType} - {Message}",
                    alert.AlertType, alert.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending generic cache alert");
            }
        }

        private async Task SendDetailedCacheAlert(object notification)
        {
            try
            {
                // This would require adding a new method to AdminNotificationHub
                // For now, we'll log that this would be sent
                _logger.LogDebug("Would send detailed cache alert: {@Notification}", notification);
                
                // In a real implementation, you would add this to AdminNotificationHub:
                // await _hubContext.Clients.Group("admin").SendAsync("CacheAlert", notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending detailed cache alert");
            }
        }

        public void Dispose()
        {
            _cooldownLock?.Dispose();
        }
    }
}