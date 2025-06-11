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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Manages audio operation alerts and notifications.
    /// </summary>
    public class AudioAlertingService : IAudioAlertingService
    {
        private readonly ILogger<AudioAlertingService> _logger;
        private readonly AudioAlertingOptions _options;
        private readonly ConcurrentDictionary<string, AudioAlertRule> _alertRules = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastAlertTimes = new();
        private readonly List<TriggeredAlert> _alertHistory = new();
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _evaluationSemaphore = new(1);
        private readonly object _historyLock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioAlertingService"/> class.
        /// </summary>
        public AudioAlertingService(
            ILogger<AudioAlertingService> logger,
            IOptions<AudioAlertingOptions> options,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClientFactory?.CreateClient("AlertingService") ?? throw new ArgumentNullException(nameof(httpClientFactory));

            // Load default alert rules
            LoadDefaultRules();
        }

        /// <inheritdoc />
        public Task<string> RegisterAlertRuleAsync(AudioAlertRule rule)
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

        /// <inheritdoc />
        public Task UpdateAlertRuleAsync(string ruleId, AudioAlertRule rule)
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

        /// <inheritdoc />
        public Task DeleteAlertRuleAsync(string ruleId)
        {
            if (_alertRules.TryRemove(ruleId, out var rule))
            {
                _logger.LogInformation("Deleted alert rule: {RuleName} ({RuleId})", rule.Name, ruleId);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<List<AudioAlertRule>> GetActiveRulesAsync()
        {
            var activeRules = _alertRules.Values
                .Where(r => r.IsEnabled)
                .ToList();

            return Task.FromResult(activeRules);
        }

        /// <inheritdoc />
        public async Task EvaluateMetricsAsync(
            AudioMetricsSnapshot metrics,
            CancellationToken cancellationToken = default)
        {
            await _evaluationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var activeRules = await GetActiveRulesAsync();

                foreach (var rule in activeRules)
                {
                    try
                    {
                        await EvaluateRuleAsync(rule, metrics, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error evaluating alert rule {RuleId}", rule.Id);
                    }
                }
            }
            finally
            {
                _evaluationSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public Task<List<TriggeredAlert>> GetAlertHistoryAsync(
            DateTime startTime,
            DateTime endTime,
            AlertSeverity? severity = null)
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

        /// <inheritdoc />
        public Task AcknowledgeAlertAsync(
            string alertId,
            string acknowledgedBy,
            string? notes = null)
        {
            lock (_historyLock)
            {
                var alert = _alertHistory.FirstOrDefault(a => a.Id == alertId);
                if (alert != null)
                {
                    alert.State = AlertState.Acknowledged;
                    alert.AcknowledgedBy = acknowledgedBy;
                    alert.AcknowledgedAt = DateTime.UtcNow;
                    alert.AcknowledgmentNotes = notes;

                    _logger.LogInformation(
                        "Alert {AlertId} acknowledged by {User}",
                        alertId, acknowledgedBy);
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<AlertTestResult> TestAlertRuleAsync(AudioAlertRule rule)
        {
            var result = new AlertTestResult
            {
                Success = true,
                Message = "Alert rule test completed"
            };

            try
            {
                // Simulate metric value
                var testMetrics = CreateTestMetrics(rule.MetricType);
                var metricValue = ExtractMetricValue(rule.MetricType, testMetrics);

                result.SimulatedMetricValue = metricValue;
                result.WouldTrigger = EvaluateCondition(rule.Condition, metricValue);

                // Test notification channels
                foreach (var channel in rule.NotificationChannels)
                {
                    var notificationTest = await TestNotificationChannelAsync(channel);
                    result.NotificationTests.Add(notificationTest);
                }

                if (result.WouldTrigger)
                {
                    result.Message = $"Alert would trigger: {rule.Name} (value: {metricValue})";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Test failed: {ex.Message}";
            }

            return result;
        }

        private async Task EvaluateRuleAsync(
            AudioAlertRule rule,
            AudioMetricsSnapshot metrics,
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
            var metricValue = ExtractMetricValue(rule.MetricType, metrics);

            // Evaluate condition
            if (!EvaluateCondition(rule.Condition, metricValue))
            {
                return; // Condition not met
            }

            // Create triggered alert
            var alert = new TriggeredAlert
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
                },
                State = AlertState.Active
            };

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

            // Send notifications
            await SendNotificationsAsync(alert, cancellationToken);

            _logger.LogWarning(
                "Alert triggered: {AlertName} - {Message}",
                rule.Name, alert.Message);
        }

        private double ExtractMetricValue(AudioMetricType metricType, AudioMetricsSnapshot metrics)
        {
            return metricType switch
            {
                AudioMetricType.ErrorRate => metrics.CurrentErrorRate,
                AudioMetricType.Latency => 0, // Would need historical data
                AudioMetricType.ProviderAvailability => metrics.ProviderHealth.Count(p => p.Value) / (double)Math.Max(1, metrics.ProviderHealth.Count),
                AudioMetricType.CacheHitRate => 0, // Would need cache metrics
                AudioMetricType.ActiveSessions => metrics.ActiveRealtimeSessions,
                AudioMetricType.RequestRate => metrics.RequestsPerSecond,
                AudioMetricType.CostRate => 0, // Would need cost data
                AudioMetricType.ConnectionPoolUtilization => metrics.Resources.ActiveConnections / 100.0,
                AudioMetricType.QueueLength => 0, // Would need queue metrics
                _ => 0
            };
        }

        private bool EvaluateCondition(AlertCondition condition, double value)
        {
            var result = condition.Operator switch
            {
                ComparisonOperator.GreaterThan => value > condition.Threshold,
                ComparisonOperator.LessThan => value < condition.Threshold,
                ComparisonOperator.Equals => Math.Abs(value - condition.Threshold) < 0.001,
                ComparisonOperator.NotEquals => Math.Abs(value - condition.Threshold) >= 0.001,
                ComparisonOperator.GreaterThanOrEqual => value >= condition.Threshold,
                ComparisonOperator.LessThanOrEqual => value <= condition.Threshold,
                _ => false
            };

            return result;
        }

        private string FormatAlertMessage(AudioAlertRule rule, double metricValue)
        {
            return $"{rule.Name}: {rule.MetricType} is {metricValue:F2} " +
                   $"(threshold: {rule.Condition.Operator} {rule.Condition.Threshold:F2})";
        }

        private async Task SendNotificationsAsync(
            TriggeredAlert alert,
            CancellationToken cancellationToken)
        {
            var tasks = alert.Rule.NotificationChannels
                .Select(channel => SendNotificationAsync(channel, alert, cancellationToken))
                .ToList();

            await Task.WhenAll(tasks);
        }

        private async Task SendNotificationAsync(
            NotificationChannel channel,
            TriggeredAlert alert,
            CancellationToken cancellationToken)
        {
            try
            {
                switch (channel.Type)
                {
                    case NotificationChannelType.Email:
                        await SendEmailNotificationAsync(channel, alert, cancellationToken);
                        break;

                    case NotificationChannelType.Webhook:
                        await SendWebhookNotificationAsync(channel, alert, cancellationToken);
                        break;

                    case NotificationChannelType.Slack:
                        await SendSlackNotificationAsync(channel, alert, cancellationToken);
                        break;

                    case NotificationChannelType.Teams:
                        await SendTeamsNotificationAsync(channel, alert, cancellationToken);
                        break;

                    default:
                        _logger.LogWarning("Unsupported notification channel type: {Type}", channel.Type);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send {ChannelType} notification for alert {AlertId}",
                    channel.Type, alert.Id);
            }
        }

        private async Task SendWebhookNotificationAsync(
            NotificationChannel channel,
            TriggeredAlert alert,
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

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(channel.Target, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Webhook notification failed: {StatusCode} - {Reason}",
                    response.StatusCode, response.ReasonPhrase);
            }
        }

        private async Task SendEmailNotificationAsync(
            NotificationChannel channel,
            TriggeredAlert alert,
            CancellationToken cancellationToken)
        {
            // In production, this would integrate with an email service
            _logger.LogInformation(
                "Email notification would be sent to {Target} for alert {AlertId}",
                channel.Target, alert.Id);

            await Task.CompletedTask;
        }

        private async Task SendSlackNotificationAsync(
            NotificationChannel channel,
            TriggeredAlert alert,
            CancellationToken cancellationToken)
        {
            var color = alert.Rule.Severity switch
            {
                AlertSeverity.Critical => "danger",
                AlertSeverity.Error => "warning",
                AlertSeverity.Warning => "warning",
                _ => "good"
            };

            var payload = new
            {
                attachments = new[]
                {
                    new
                    {
                        color,
                        title = $"{alert.Rule.Severity}: {alert.Rule.Name}",
                        text = alert.Message,
                        fields = new[]
                        {
                            new { title = "Metric", value = alert.Rule.MetricType.ToString(), @short = true },
                            new { title = "Value", value = alert.MetricValue.ToString("F2"), @short = true },
                            new { title = "Threshold", value = alert.Rule.Condition.Threshold.ToString("F2"), @short = true },
                            new { title = "Time", value = alert.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true }
                        },
                        footer = "Conduit Audio Alerting",
                        ts = ((DateTimeOffset)alert.TriggeredAt).ToUnixTimeSeconds()
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await _httpClient.PostAsync(channel.Target, content, cancellationToken);
        }

        private async Task SendTeamsNotificationAsync(
            NotificationChannel channel,
            TriggeredAlert alert,
            CancellationToken cancellationToken)
        {
            var themeColor = alert.Rule.Severity switch
            {
                AlertSeverity.Critical => "FF0000",
                AlertSeverity.Error => "FF8C00",
                AlertSeverity.Warning => "FFD700",
                _ => "00FF00"
            };

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
                        facts = new[]
                        {
                            new { name = "Metric", value = alert.Rule.MetricType.ToString() },
                            new { name = "Current Value", value = alert.MetricValue.ToString("F2") },
                            new { name = "Threshold", value = $"{alert.Rule.Condition.Operator} {alert.Rule.Condition.Threshold:F2}" },
                            new { name = "Triggered At", value = alert.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss UTC") }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await _httpClient.PostAsync(channel.Target, content, cancellationToken);
        }

        private async Task<NotificationTestResult> TestNotificationChannelAsync(NotificationChannel channel)
        {
            var result = new NotificationTestResult
            {
                ChannelType = channel.Type,
                Success = true
            };

            try
            {
                // Test connectivity
                switch (channel.Type)
                {
                    case NotificationChannelType.Webhook:
                    case NotificationChannelType.Slack:
                    case NotificationChannelType.Teams:
                        var testPayload = new { test = true, timestamp = DateTime.UtcNow };
                        var json = JsonSerializer.Serialize(testPayload);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await _httpClient.PostAsync(channel.Target, content);
                        result.Success = response.IsSuccessStatusCode;
                        if (!result.Success)
                        {
                            result.ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                        }
                        break;

                    case NotificationChannelType.Email:
                        // Would test SMTP connectivity
                        result.Success = true;
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

            return result;
        }

        private AudioMetricsSnapshot CreateTestMetrics(AudioMetricType metricType)
        {
            return new AudioMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                ActiveTranscriptions = 5,
                ActiveTtsOperations = 3,
                ActiveRealtimeSessions = 10,
                RequestsPerSecond = 25.5,
                CurrentErrorRate = 0.02,
                ProviderHealth = new Dictionary<string, bool>
                {
                    ["openai"] = true,
                    ["elevenlabs"] = true,
                    ["deepgram"] = false
                },
                Resources = new SystemResources
                {
                    CpuUsagePercent = 45.2,
                    MemoryUsageMb = 2048,
                    ActiveConnections = 75,
                    CacheSizeMb = 512
                }
            };
        }

        private void LoadDefaultRules()
        {
            // High error rate alert
            _ = RegisterAlertRuleAsync(new AudioAlertRule
            {
                Name = "High Error Rate",
                Description = "Alert when error rate exceeds 5%",
                MetricType = AudioMetricType.ErrorRate,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 0.05,
                    TimeWindow = TimeSpan.FromMinutes(5),
                    MinimumOccurrences = 2
                },
                Severity = AlertSeverity.Error,
                IsEnabled = true
            }).Result;

            // Provider down alert
            _ = RegisterAlertRuleAsync(new AudioAlertRule
            {
                Name = "Provider Availability Low",
                Description = "Alert when provider availability drops below 50%",
                MetricType = AudioMetricType.ProviderAvailability,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.LessThan,
                    Threshold = 0.5,
                    TimeWindow = TimeSpan.FromMinutes(2)
                },
                Severity = AlertSeverity.Critical,
                IsEnabled = true
            }).Result;

            // High request rate alert
            _ = RegisterAlertRuleAsync(new AudioAlertRule
            {
                Name = "High Request Rate",
                Description = "Alert when request rate exceeds 100 RPS",
                MetricType = AudioMetricType.RequestRate,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 100,
                    TimeWindow = TimeSpan.FromMinutes(1)
                },
                Severity = AlertSeverity.Warning,
                IsEnabled = true
            }).Result;
        }
    }

    /// <summary>
    /// Options for audio alerting service.
    /// </summary>
    public class AudioAlertingOptions
    {
        /// <summary>
        /// Gets or sets the maximum alert history size.
        /// </summary>
        public int MaxHistorySize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the default cooldown period.
        /// </summary>
        public TimeSpan DefaultCooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the evaluation interval.
        /// </summary>
        public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromMinutes(1);
    }
}
