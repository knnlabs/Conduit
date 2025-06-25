using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Manages alerts and notifications for image generation operations.
    /// </summary>
    public class ImageGenerationAlertingService : IImageGenerationAlertingService
    {
        private readonly ILogger<ImageGenerationAlertingService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ImageGenerationAlertingOptions _options;
        
        private readonly ConcurrentDictionary<string, ImageGenerationAlertRule> _alertRules = new();
        private readonly ConcurrentDictionary<string, NotificationChannel> _notificationChannels = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastAlertTimes = new();
        private readonly ConcurrentDictionary<string, ImageGenerationAlert> _activeAlerts = new();
        private readonly List<ImageGenerationAlert> _alertHistory = new();
        
        private readonly SemaphoreSlim _evaluationSemaphore = new(1);
        private readonly object _historyLock = new();

        public ImageGenerationAlertingService(
            ILogger<ImageGenerationAlertingService> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<ImageGenerationAlertingOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _options = options?.Value ?? new ImageGenerationAlertingOptions();
            
            // Load default alert rules
            LoadDefaultAlertRules();
        }

        public Task<string> RegisterAlertRuleAsync(ImageGenerationAlertRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            
            if (string.IsNullOrEmpty(rule.Id))
                rule.Id = Guid.NewGuid().ToString();
            
            _alertRules[rule.Id] = rule;
            
            _logger.LogInformation(
                "Registered alert rule: {RuleName} ({RuleId}) for metric {MetricType}",
                rule.Name, rule.Id, rule.MetricType);
            
            return Task.FromResult(rule.Id);
        }

        public Task UpdateAlertRuleAsync(string ruleId, ImageGenerationAlertRule rule)
        {
            if (string.IsNullOrEmpty(ruleId))
                throw new ArgumentException("Rule ID cannot be empty", nameof(ruleId));
            
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            
            if (!_alertRules.ContainsKey(ruleId))
                throw new InvalidOperationException($"Alert rule {ruleId} not found");
            
            rule.Id = ruleId;
            _alertRules[ruleId] = rule;
            
            _logger.LogInformation("Updated alert rule: {RuleId}", ruleId);
            
            return Task.CompletedTask;
        }

        public Task DeleteAlertRuleAsync(string ruleId)
        {
            if (_alertRules.TryRemove(ruleId, out var rule))
            {
                _logger.LogInformation("Deleted alert rule: {RuleName} ({RuleId})", rule.Name, ruleId);
            }
            
            return Task.CompletedTask;
        }

        public Task<List<ImageGenerationAlertRule>> GetActiveRulesAsync()
        {
            var activeRules = _alertRules.Values
                .Where(r => r.IsEnabled)
                .ToList();
            
            return Task.FromResult(activeRules);
        }

        public async Task EvaluateMetricsAsync(ImageGenerationMetricsSnapshot metrics, CancellationToken cancellationToken = default)
        {
            await _evaluationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var activeRules = await GetActiveRulesAsync();
                
                foreach (var rule in activeRules)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    try
                    {
                        await EvaluateRuleAsync(rule, metrics, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error evaluating alert rule {RuleId}", rule.Id);
                    }
                }
                
                // Check for resolved alerts
                await CheckResolvedAlertsAsync(metrics);
            }
            finally
            {
                _evaluationSemaphore.Release();
            }
        }

        public Task<List<ImageGenerationAlert>> GetAlertHistoryAsync(DateTime startTime, DateTime endTime, AlertSeverity? severity = null)
        {
            lock (_historyLock)
            {
                var filtered = _alertHistory
                    .Where(a => a.TriggeredAt >= startTime && a.TriggeredAt <= endTime)
                    .Where(a => severity == null || a.Rule.Severity == severity)
                    .OrderByDescending(a => a.TriggeredAt)
                    .ToList();
                
                return Task.FromResult(filtered);
            }
        }

        public Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, string? notes = null)
        {
            if (_activeAlerts.TryGetValue(alertId, out var alert))
            {
                alert.State = AlertState.Acknowledged;
                alert.AcknowledgedBy = acknowledgedBy;
                alert.AcknowledgedAt = DateTime.UtcNow;
                alert.AcknowledgmentNotes = notes;
                
                _logger.LogInformation(
                    "Alert {AlertId} acknowledged by {User}",
                    alertId, acknowledgedBy);
            }
            
            lock (_historyLock)
            {
                var historicalAlert = _alertHistory.FirstOrDefault(a => a.Id == alertId);
                if (historicalAlert != null)
                {
                    historicalAlert.State = AlertState.Acknowledged;
                    historicalAlert.AcknowledgedBy = acknowledgedBy;
                    historicalAlert.AcknowledgedAt = DateTime.UtcNow;
                    historicalAlert.AcknowledgmentNotes = notes;
                }
            }
            
            return Task.CompletedTask;
        }

        public async Task<AlertTestResult> TestAlertRuleAsync(ImageGenerationAlertRule rule)
        {
            var result = new AlertTestResult
            {
                Success = true,
                Message = "Alert rule test completed"
            };
            
            try
            {
                // Create test metrics
                var testMetrics = CreateTestMetrics(rule.MetricType);
                var metricValue = ExtractMetricValue(rule.MetricType, testMetrics, rule.Condition);
                
                result.SimulatedMetricValue = metricValue;
                result.WouldTrigger = EvaluateCondition(rule.Condition, metricValue);
                
                // Test notification channels
                foreach (var channelId in rule.NotificationChannelIds)
                {
                    if (_notificationChannels.TryGetValue(channelId, out var channel))
                    {
                        var notificationTest = await TestNotificationChannelAsync(channelId);
                        result.NotificationTests.Add(notificationTest);
                    }
                }
                
                if (result.WouldTrigger)
                {
                    result.Message = $"Alert would trigger: {rule.Name} (value: {metricValue:F2})";
                }
                else
                {
                    result.Message = $"Alert would not trigger: {rule.Name} (value: {metricValue:F2})";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Test failed: {ex.Message}";
            }
            
            return result;
        }

        public Task<List<ImageGenerationAlert>> GetActiveAlertsAsync()
        {
            var activeAlerts = _activeAlerts.Values
                .Where(a => a.State == AlertState.Active)
                .OrderByDescending(a => a.Rule.Severity)
                .ThenByDescending(a => a.TriggeredAt)
                .ToList();
            
            return Task.FromResult(activeAlerts);
        }

        public Task<string> RegisterNotificationChannelAsync(NotificationChannel channel)
        {
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));
            
            if (string.IsNullOrEmpty(channel.Id))
                channel.Id = Guid.NewGuid().ToString();
            
            _notificationChannels[channel.Id] = channel;
            
            _logger.LogInformation(
                "Registered notification channel: {ChannelName} ({ChannelId}) of type {ChannelType}",
                channel.Name, channel.Id, channel.Type);
            
            return Task.FromResult(channel.Id);
        }

        public async Task<NotificationTestResult> TestNotificationChannelAsync(string channelId)
        {
            if (!_notificationChannels.TryGetValue(channelId, out var channel))
            {
                return new NotificationTestResult
                {
                    ChannelId = channelId,
                    Success = false,
                    ErrorMessage = "Channel not found"
                };
            }
            
            var result = new NotificationTestResult
            {
                ChannelId = channelId,
                ChannelName = channel.Name,
                ChannelType = channel.Type,
                Success = true
            };
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                switch (channel.Type)
                {
                    case NotificationChannelType.Webhook:
                        await TestWebhookChannelAsync(channel);
                        break;
                        
                    case NotificationChannelType.Slack:
                        await TestSlackChannelAsync(channel);
                        break;
                        
                    case NotificationChannelType.Teams:
                        await TestTeamsChannelAsync(channel);
                        break;
                        
                    case NotificationChannelType.Email:
                        // Email testing would require SMTP configuration
                        _logger.LogInformation("Email channel test would be performed");
                        break;
                        
                    case NotificationChannelType.PagerDuty:
                        await TestPagerDutyChannelAsync(channel);
                        break;
                        
                    default:
                        result.Success = false;
                        result.ErrorMessage = $"Unsupported channel type: {channel.Type}";
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            
            return result;
        }

        private async Task EvaluateRuleAsync(
            ImageGenerationAlertRule rule,
            ImageGenerationMetricsSnapshot metrics,
            CancellationToken cancellationToken)
        {
            // Check cooldown period
            if (_lastAlertTimes.TryGetValue(rule.Id, out var lastAlert))
            {
                if (DateTime.UtcNow - lastAlert < rule.CooldownPeriod)
                {
                    return; // Still in cooldown
                }
            }
            
            // Extract metric value
            var metricValue = ExtractMetricValue(rule.MetricType, metrics, rule.Condition);
            
            // Evaluate condition
            if (!EvaluateCondition(rule.Condition, metricValue))
            {
                return; // Condition not met
            }
            
            // Create alert
            var alert = new ImageGenerationAlert
            {
                Rule = rule,
                MetricValue = metricValue,
                Message = FormatAlertMessage(rule, metricValue),
                Details = new Dictionary<string, object>
                {
                    ["metric_type"] = rule.MetricType.ToString(),
                    ["threshold"] = rule.Condition.Threshold,
                    ["actual_value"] = metricValue,
                    ["timestamp"] = metrics.Timestamp
                }
            };
            
            // Add provider/model details if specified
            if (!string.IsNullOrEmpty(rule.Condition.Provider))
            {
                alert.Details["provider"] = rule.Condition.Provider;
            }
            
            if (!string.IsNullOrEmpty(rule.Condition.Model))
            {
                alert.Details["model"] = rule.Condition.Model;
            }
            
            // Add to active alerts
            _activeAlerts[alert.Id] = alert;
            
            // Add to history
            lock (_historyLock)
            {
                _alertHistory.Add(alert);
                
                // Trim old history
                if (_alertHistory.Count > _options.MaxHistorySize)
                {
                    _alertHistory.RemoveAt(0);
                }
            }
            
            // Update last alert time
            _lastAlertTimes[rule.Id] = DateTime.UtcNow;
            rule.LastTriggeredAt = DateTime.UtcNow;
            
            // Send notifications
            await SendNotificationsAsync(alert, cancellationToken);
            
            _logger.LogWarning(
                "Alert triggered: {AlertName} - {Message}",
                rule.Name, alert.Message);
        }

        private double ExtractMetricValue(
            ImageGenerationMetricType metricType,
            ImageGenerationMetricsSnapshot metrics,
            AlertCondition condition)
        {
            switch (metricType)
            {
                case ImageGenerationMetricType.ErrorRate:
                    return 100 - metrics.SuccessRate; // Convert to error rate percentage
                    
                case ImageGenerationMetricType.ResponseTime:
                    return metrics.AverageResponseTimeMs;
                    
                case ImageGenerationMetricType.P95ResponseTime:
                    return metrics.P95ResponseTimeMs;
                    
                case ImageGenerationMetricType.ProviderAvailability:
                    if (!string.IsNullOrEmpty(condition.Provider) && 
                        metrics.ProviderStatuses.TryGetValue(condition.Provider, out var status))
                    {
                        return status.IsHealthy ? 100.0 : 0.0;
                    }
                    // Overall provider availability
                    var healthyProviders = metrics.ProviderStatuses.Values.Count(p => p.IsHealthy);
                    var totalProviders = Math.Max(1, metrics.ProviderStatuses.Count);
                    return (healthyProviders / (double)totalProviders) * 100;
                    
                case ImageGenerationMetricType.ProviderHealthScore:
                    if (!string.IsNullOrEmpty(condition.Provider) && 
                        metrics.ProviderStatuses.TryGetValue(condition.Provider, out var providerStatus))
                    {
                        return providerStatus.HealthScore * 100;
                    }
                    return 0;
                    
                case ImageGenerationMetricType.QueueDepth:
                    return metrics.QueueMetrics.TotalDepth;
                    
                case ImageGenerationMetricType.QueueWaitTime:
                    return metrics.QueueMetrics.MaxWaitTimeMs;
                    
                case ImageGenerationMetricType.GenerationRate:
                    return metrics.GenerationsPerMinute;
                    
                case ImageGenerationMetricType.CostRate:
                    return (double)metrics.TotalCostLastHour;
                    
                case ImageGenerationMetricType.CostPerImage:
                    return metrics.TotalImagesLastHour > 0 
                        ? (double)(metrics.TotalCostLastHour / metrics.TotalImagesLastHour)
                        : 0;
                    
                case ImageGenerationMetricType.ResourceUtilization:
                    return Math.Max(
                        metrics.ResourceMetrics.CpuUsagePercent,
                        metrics.ResourceMetrics.MemoryUsagePercent);
                    
                case ImageGenerationMetricType.ConsecutiveFailures:
                    if (!string.IsNullOrEmpty(condition.Provider) && 
                        metrics.ProviderStatuses.TryGetValue(condition.Provider, out var failureStatus))
                    {
                        return failureStatus.ConsecutiveFailures;
                    }
                    return 0;
                    
                default:
                    return 0;
            }
        }

        private bool EvaluateCondition(AlertCondition condition, double value)
        {
            return condition.Operator switch
            {
                ComparisonOperator.GreaterThan => value > condition.Threshold,
                ComparisonOperator.LessThan => value < condition.Threshold,
                ComparisonOperator.Equals => Math.Abs(value - condition.Threshold) < 0.001,
                ComparisonOperator.NotEquals => Math.Abs(value - condition.Threshold) >= 0.001,
                ComparisonOperator.GreaterThanOrEqual => value >= condition.Threshold,
                ComparisonOperator.LessThanOrEqual => value <= condition.Threshold,
                _ => false
            };
        }

        private string FormatAlertMessage(ImageGenerationAlertRule rule, double metricValue)
        {
            var unit = GetMetricUnit(rule.MetricType);
            var formattedValue = FormatMetricValue(metricValue, rule.MetricType);
            var formattedThreshold = FormatMetricValue(rule.Condition.Threshold, rule.MetricType);
            
            var message = $"{rule.Name}: {rule.MetricType} is {formattedValue}{unit} " +
                         $"(threshold: {rule.Condition.Operator} {formattedThreshold}{unit})";
            
            if (!string.IsNullOrEmpty(rule.Condition.Provider))
            {
                message = $"[{rule.Condition.Provider}] {message}";
            }
            
            return message;
        }

        private string GetMetricUnit(ImageGenerationMetricType metricType)
        {
            return metricType switch
            {
                ImageGenerationMetricType.ErrorRate => "%",
                ImageGenerationMetricType.ResponseTime => "ms",
                ImageGenerationMetricType.P95ResponseTime => "ms",
                ImageGenerationMetricType.ProviderAvailability => "%",
                ImageGenerationMetricType.ProviderHealthScore => "%",
                ImageGenerationMetricType.QueueWaitTime => "ms",
                ImageGenerationMetricType.GenerationRate => "/min",
                ImageGenerationMetricType.CostRate => "$/hr",
                ImageGenerationMetricType.CostPerImage => "$/img",
                ImageGenerationMetricType.ResourceUtilization => "%",
                _ => ""
            };
        }

        private string FormatMetricValue(double value, ImageGenerationMetricType metricType)
        {
            return metricType switch
            {
                ImageGenerationMetricType.CostRate or ImageGenerationMetricType.CostPerImage => value.ToString("F2"),
                ImageGenerationMetricType.ErrorRate or ImageGenerationMetricType.ProviderAvailability or 
                ImageGenerationMetricType.ProviderHealthScore or ImageGenerationMetricType.ResourceUtilization => value.ToString("F1"),
                _ => value.ToString("F0")
            };
        }

        private async Task SendNotificationsAsync(
            ImageGenerationAlert alert,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task<NotificationResult>>();
            
            foreach (var channelId in alert.Rule.NotificationChannelIds)
            {
                if (_notificationChannels.TryGetValue(channelId, out var channel))
                {
                    // Check severity filter
                    if (channel.SeverityFilter.Any() && 
                        !channel.SeverityFilter.Contains(alert.Rule.Severity))
                    {
                        continue;
                    }
                    
                    tasks.Add(SendNotificationAsync(channel, alert, cancellationToken));
                }
            }
            
            var results = await Task.WhenAll(tasks);
            alert.NotificationResults.AddRange(results);
        }

        private async Task<NotificationResult> SendNotificationAsync(
            NotificationChannel channel,
            ImageGenerationAlert alert,
            CancellationToken cancellationToken)
        {
            var result = new NotificationResult
            {
                ChannelId = channel.Id,
                SentAt = DateTime.UtcNow
            };
            
            try
            {
                switch (channel.Type)
                {
                    case NotificationChannelType.Webhook:
                        await SendWebhookNotificationAsync(channel, alert, cancellationToken);
                        result.Success = true;
                        break;
                        
                    case NotificationChannelType.Slack:
                        await SendSlackNotificationAsync(channel, alert, cancellationToken);
                        result.Success = true;
                        break;
                        
                    case NotificationChannelType.Teams:
                        await SendTeamsNotificationAsync(channel, alert, cancellationToken);
                        result.Success = true;
                        break;
                        
                    case NotificationChannelType.PagerDuty:
                        await SendPagerDutyNotificationAsync(channel, alert, cancellationToken);
                        result.Success = true;
                        break;
                        
                    case NotificationChannelType.Email:
                        _logger.LogInformation(
                            "Email notification would be sent to {Target} for alert {AlertId}",
                            channel.Target, alert.Id);
                        result.Success = true;
                        break;
                        
                    default:
                        result.Success = false;
                        result.Error = $"Unsupported channel type: {channel.Type}";
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                _logger.LogError(ex,
                    "Failed to send {ChannelType} notification for alert {AlertId}",
                    channel.Type, alert.Id);
            }
            
            return result;
        }

        private async Task SendWebhookNotificationAsync(
            NotificationChannel channel,
            ImageGenerationAlert alert,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                alert_id = alert.Id,
                rule_name = alert.Rule.Name,
                severity = alert.Rule.Severity.ToString(),
                metric_type = alert.Rule.MetricType.ToString(),
                metric_value = alert.MetricValue,
                threshold = alert.Rule.Condition.Threshold,
                message = alert.Message,
                triggered_at = alert.TriggeredAt,
                details = alert.Details
            };
            
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync(channel.Target, content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task SendSlackNotificationAsync(
            NotificationChannel channel,
            ImageGenerationAlert alert,
            CancellationToken cancellationToken)
        {
            var color = alert.Rule.Severity switch
            {
                AlertSeverity.Critical => "danger",
                AlertSeverity.Error => "warning",
                AlertSeverity.Warning => "warning",
                _ => "good"
            };
            
            var fields = new List<object>
            {
                new { title = "Metric", value = alert.Rule.MetricType.ToString(), @short = true },
                new { title = "Value", value = FormatMetricValue(alert.MetricValue, alert.Rule.MetricType) + GetMetricUnit(alert.Rule.MetricType), @short = true },
                new { title = "Threshold", value = FormatMetricValue(alert.Rule.Condition.Threshold, alert.Rule.MetricType) + GetMetricUnit(alert.Rule.MetricType), @short = true },
                new { title = "Time", value = alert.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true }
            };
            
            if (alert.Details.ContainsKey("provider"))
            {
                fields.Add(new { title = "Provider", value = alert.Details["provider"], @short = true });
            }
            
            var payload = new
            {
                attachments = new[]
                {
                    new
                    {
                        color,
                        title = $"{alert.Rule.Severity}: {alert.Rule.Name}",
                        text = alert.Message,
                        fields,
                        footer = "Conduit Image Generation Monitoring",
                        ts = ((DateTimeOffset)alert.TriggeredAt).ToUnixTimeSeconds()
                    }
                }
            };
            
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            await httpClient.PostAsync(channel.Target, content, cancellationToken);
        }

        private async Task SendTeamsNotificationAsync(
            NotificationChannel channel,
            ImageGenerationAlert alert,
            CancellationToken cancellationToken)
        {
            var themeColor = alert.Rule.Severity switch
            {
                AlertSeverity.Critical => "FF0000",
                AlertSeverity.Error => "FF8C00",
                AlertSeverity.Warning => "FFD700",
                _ => "00FF00"
            };
            
            var facts = new List<object>
            {
                new { name = "Metric", value = alert.Rule.MetricType.ToString() },
                new { name = "Current Value", value = FormatMetricValue(alert.MetricValue, alert.Rule.MetricType) + GetMetricUnit(alert.Rule.MetricType) },
                new { name = "Threshold", value = $"{alert.Rule.Condition.Operator} {FormatMetricValue(alert.Rule.Condition.Threshold, alert.Rule.MetricType)}{GetMetricUnit(alert.Rule.MetricType)}" },
                new { name = "Triggered At", value = alert.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss UTC") }
            };
            
            if (alert.Details.ContainsKey("provider"))
            {
                facts.Add(new { name = "Provider", value = alert.Details["provider"] });
            }
            
            var payload = new
            {
                @type = "MessageCard",
                @context = "https://schema.org/extensions",
                summary = alert.Message,
                themeColor,
                sections = new[]
                {
                    new
                    {
                        activityTitle = alert.Rule.Name,
                        activitySubtitle = $"Severity: {alert.Rule.Severity}",
                        facts,
                        markdown = true
                    }
                },
                potentialAction = new[]
                {
                    new
                    {
                        @type = "OpenUri",
                        name = "View Dashboard",
                        targets = new[]
                        {
                            new { os = "default", uri = channel.Configuration.GetValueOrDefault("dashboardUrl", "https://dashboard.example.com") }
                        }
                    }
                }
            };
            
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            await httpClient.PostAsync(channel.Target, content, cancellationToken);
        }

        private async Task SendPagerDutyNotificationAsync(
            NotificationChannel channel,
            ImageGenerationAlert alert,
            CancellationToken cancellationToken)
        {
            var severity = alert.Rule.Severity switch
            {
                AlertSeverity.Critical => "critical",
                AlertSeverity.Error => "error",
                AlertSeverity.Warning => "warning",
                _ => "info"
            };
            
            var payload = new
            {
                routing_key = channel.Target,
                event_action = "trigger",
                dedup_key = $"conduit-img-{alert.Rule.Id}-{alert.TriggeredAt:yyyyMMddHHmmss}",
                payload = new
                {
                    summary = alert.Message,
                    source = "conduit-image-generation",
                    severity,
                    timestamp = alert.TriggeredAt.ToString("O"),
                    custom_details = alert.Details
                }
            };
            
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync(
                "https://events.pagerduty.com/v2/enqueue",
                content,
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
        }

        private async Task CheckResolvedAlertsAsync(ImageGenerationMetricsSnapshot metrics)
        {
            var resolvedAlerts = new List<string>();
            
            foreach (var alert in _activeAlerts.Values.Where(a => a.State == AlertState.Active))
            {
                var currentValue = ExtractMetricValue(alert.Rule.MetricType, metrics, alert.Rule.Condition);
                
                // Check if condition is no longer met
                if (!EvaluateCondition(alert.Rule.Condition, currentValue))
                {
                    alert.State = AlertState.Resolved;
                    resolvedAlerts.Add(alert.Id);
                    
                    _logger.LogInformation(
                        "Alert resolved: {AlertName} - Current value: {Value}",
                        alert.Rule.Name, currentValue);
                }
            }
            
            // Remove resolved alerts from active list
            foreach (var alertId in resolvedAlerts)
            {
                _activeAlerts.TryRemove(alertId, out _);
            }
        }

        private ImageGenerationMetricsSnapshot CreateTestMetrics(ImageGenerationMetricType metricType)
        {
            var metrics = new ImageGenerationMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                ActiveGenerations = 5,
                GenerationsPerMinute = 25.5,
                AverageResponseTimeMs = 15000,
                P95ResponseTimeMs = 35000,
                SuccessRate = 98.5,
                TotalCostLastHour = 125.50m,
                TotalImagesLastHour = 250,
                ProviderStatuses = new Dictionary<string, ProviderStatus>
                {
                    ["OpenAI"] = new ProviderStatus 
                    { 
                        IsHealthy = true, 
                        HealthScore = 0.95, 
                        ActiveRequests = 3,
                        AverageResponseTimeMs = 12000
                    },
                    ["MiniMax"] = new ProviderStatus 
                    { 
                        IsHealthy = true, 
                        HealthScore = 0.88, 
                        ActiveRequests = 2,
                        AverageResponseTimeMs = 18000
                    },
                    ["Replicate"] = new ProviderStatus 
                    { 
                        IsHealthy = false, 
                        HealthScore = 0.2, 
                        ConsecutiveFailures = 5,
                        LastError = "Connection timeout"
                    }
                },
                QueueMetrics = new QueueMetrics
                {
                    TotalDepth = 15,
                    MaxWaitTimeMs = 5000,
                    AverageWaitTimeMs = 2500
                },
                ResourceMetrics = new ResourceMetrics
                {
                    CpuUsagePercent = 65.5,
                    MemoryUsagePercent = 78.2,
                    MemoryUsageMb = 3200
                }
            };
            
            // Adjust metrics based on what we're testing
            switch (metricType)
            {
                case ImageGenerationMetricType.ErrorRate:
                    metrics.SuccessRate = 94.0; // 6% error rate
                    break;
                case ImageGenerationMetricType.P95ResponseTime:
                    metrics.P95ResponseTimeMs = 55000; // 55 seconds
                    break;
                case ImageGenerationMetricType.QueueDepth:
                    metrics.QueueMetrics.TotalDepth = 150;
                    break;
                case ImageGenerationMetricType.CostRate:
                    metrics.TotalCostLastHour = 250.00m;
                    break;
            }
            
            return metrics;
        }

        private async Task TestWebhookChannelAsync(NotificationChannel channel)
        {
            var testPayload = new { test = true, timestamp = DateTime.UtcNow, channel = channel.Name };
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(testPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync(channel.Target, content);
            response.EnsureSuccessStatusCode();
        }

        private async Task TestSlackChannelAsync(NotificationChannel channel)
        {
            var testPayload = new { text = $"Test notification from Conduit Image Generation Monitoring - {channel.Name}" };
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(testPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync(channel.Target, content);
            response.EnsureSuccessStatusCode();
        }

        private async Task TestTeamsChannelAsync(NotificationChannel channel)
        {
            var testPayload = new
            {
                @type = "MessageCard",
                @context = "https://schema.org/extensions",
                summary = "Test notification",
                text = $"Test notification from Conduit Image Generation Monitoring - {channel.Name}"
            };
            
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(testPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync(channel.Target, content);
            response.EnsureSuccessStatusCode();
        }

        private async Task TestPagerDutyChannelAsync(NotificationChannel channel)
        {
            var testPayload = new
            {
                routing_key = channel.Target,
                event_action = "trigger",
                dedup_key = $"conduit-test-{Guid.NewGuid()}",
                payload = new
                {
                    summary = $"Test alert from Conduit - {channel.Name}",
                    source = "conduit-image-generation",
                    severity = "info"
                }
            };
            
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(testPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync(
                "https://events.pagerduty.com/v2/enqueue",
                content);
            
            response.EnsureSuccessStatusCode();
        }

        private void LoadDefaultAlertRules()
        {
            // High error rate alert
            _ = RegisterAlertRuleAsync(new ImageGenerationAlertRule
            {
                Name = "High Image Generation Error Rate",
                Description = "Alert when error rate exceeds 5%",
                MetricType = ImageGenerationMetricType.ErrorRate,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 5.0,
                    TimeWindow = TimeSpan.FromMinutes(5),
                    MinimumOccurrences = 2
                },
                Severity = AlertSeverity.Error,
                IsEnabled = true
            }).Result;
            
            // P95 response time alert
            _ = RegisterAlertRuleAsync(new ImageGenerationAlertRule
            {
                Name = "P95 Response Time SLA Violation",
                Description = "Alert when P95 response time exceeds 45 seconds",
                MetricType = ImageGenerationMetricType.P95ResponseTime,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 45000,
                    TimeWindow = TimeSpan.FromMinutes(10)
                },
                Severity = AlertSeverity.Warning,
                IsEnabled = true
            }).Result;
            
            // Provider down alert
            _ = RegisterAlertRuleAsync(new ImageGenerationAlertRule
            {
                Name = "Provider Availability Critical",
                Description = "Alert when provider availability drops below 50%",
                MetricType = ImageGenerationMetricType.ProviderAvailability,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.LessThan,
                    Threshold = 50.0,
                    TimeWindow = TimeSpan.FromMinutes(2)
                },
                Severity = AlertSeverity.Critical,
                IsEnabled = true
            }).Result;
            
            // Queue depth alert
            _ = RegisterAlertRuleAsync(new ImageGenerationAlertRule
            {
                Name = "High Queue Depth",
                Description = "Alert when queue depth exceeds 100 items",
                MetricType = ImageGenerationMetricType.QueueDepth,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 100,
                    TimeWindow = TimeSpan.FromMinutes(5)
                },
                Severity = AlertSeverity.Warning,
                IsEnabled = true
            }).Result;
            
            // Cost rate alert
            _ = RegisterAlertRuleAsync(new ImageGenerationAlertRule
            {
                Name = "High Cost Rate",
                Description = "Alert when hourly cost exceeds $200",
                MetricType = ImageGenerationMetricType.CostRate,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 200.0,
                    TimeWindow = TimeSpan.FromMinutes(15)
                },
                Severity = AlertSeverity.Warning,
                IsEnabled = true
            }).Result;
            
            // Resource utilization alert
            _ = RegisterAlertRuleAsync(new ImageGenerationAlertRule
            {
                Name = "High Resource Utilization",
                Description = "Alert when CPU or memory usage exceeds 85%",
                MetricType = ImageGenerationMetricType.ResourceUtilization,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 85.0,
                    TimeWindow = TimeSpan.FromMinutes(5)
                },
                Severity = AlertSeverity.Error,
                IsEnabled = true
            }).Result;
        }
    }

    /// <summary>
    /// Configuration options for image generation alerting.
    /// </summary>
    public class ImageGenerationAlertingOptions
    {
        public int MaxHistorySize { get; set; } = 1000;
        public TimeSpan DefaultCooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromMinutes(1);
    }
}