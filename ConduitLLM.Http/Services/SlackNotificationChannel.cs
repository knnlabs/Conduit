using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Slack notification channel for health alerts
    /// </summary>
    public class SlackNotificationChannel : IAlertNotificationChannel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SlackNotificationChannel> _logger;
        private readonly SlackNotificationOptions _options;

        public string Name => "Slack";
        public bool IsEnabled => _options.Enabled && !string.IsNullOrEmpty(_options.WebhookUrl);
        public bool SupportsBatchSending => true;

        public SlackNotificationChannel(
            IHttpClientFactory httpClientFactory,
            ILogger<SlackNotificationChannel> logger,
            IOptions<SlackNotificationOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _options = options.Value;
        }

        public bool SupportsAlertType(AlertType alertType)
        {
            return _options.AlertTypes?.Contains(alertType) ?? true;
        }

        public async Task SendAsync(HealthAlert alert, CancellationToken cancellationToken = default)
        {
            var attachments = new List<object>
            {
                new
                {
                    color = GetSeverityColor(alert.Severity),
                    fields = new[]
                    {
                        new { title = "Component", value = alert.Component, @short = true },
                        new { title = "Severity", value = alert.Severity.ToString(), @short = true },
                        new { title = "Type", value = alert.Type.ToString(), @short = true },
                        new { title = "Time", value = alert.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss") + " UTC", @short = true }
                    },
                    text = alert.Message,
                    footer = "ConduitLLM Health Monitor",
                    ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };

            // Add suggested actions if any
            if (alert.SuggestedActions.Count() > 0)
            {
                attachments.Add(new
                {
                    color = "warning",
                    fields = new[]
                    {
                        new 
                        { 
                            title = "Suggested Actions", 
                            value = string.Join("\n", alert.SuggestedActions.Select(a => $"• {a}")),
                            @short = false
                        }
                    },
                    footer = "Action Required",
                    ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }

            var payload = new
            {
                text = $"*{GetSeverityEmoji(alert.Severity)} {alert.Title}*",
                attachments = attachments.ToArray()
            };

            await SendToSlackAsync(payload, cancellationToken);
        }

        public async Task SendBatchAsync(IEnumerable<HealthAlert> alerts, CancellationToken cancellationToken = default)
        {
            var alertsList = alerts.ToList();
            var groupedBySeverity = alertsList.GroupBy(a => a.Severity).OrderByDescending(g => g.Key);
            
            var blocks = new List<object>
            {
                new
                {
                    type = "header",
                    text = new
                    {
                        type = "plain_text",
                        text = $"Health Alert Summary ({alertsList.Count} alerts)"
                    }
                }
            };

            foreach (var group in groupedBySeverity)
            {
                blocks.Add(new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $"*{GetSeverityEmoji(group.Key)} {group.Key}* ({group.Count()} alerts)"
                    }
                });

                var alertTexts = group.Take(5).Select(a => 
                    $"• *{a.Component}*: {a.Title} ({a.TriggeredAt:HH:mm:ss})"
                ).ToList();

                if (group.Count() > 5)
                {
                    alertTexts.Add($"_...and {group.Count() - 5} more_");
                }

                blocks.Add(new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = string.Join("\n", alertTexts)
                    }
                });

                blocks.Add(new { type = "divider" });
            }

            var payload = new
            {
                blocks = blocks.ToArray()
            };

            await SendToSlackAsync(payload, cancellationToken);
        }

        private async Task SendToSlackAsync(object payload, CancellationToken cancellationToken)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient("SlackWebhook");
                httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(_options.WebhookUrl, content, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Slack webhook failed with status {StatusCode}: {Response}", 
                        response.StatusCode, responseBody);
                    throw new HttpRequestException($"Slack webhook failed with status {response.StatusCode}");
                }

                _logger.LogInformation("Alert sent to Slack successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Slack notification");
                throw;
            }
        }

        private string GetSeverityEmoji(AlertSeverity severity) => severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Error => "🟠",
            AlertSeverity.Warning => "🟡",
            AlertSeverity.Info => "🔵",
            _ => "⚪"
        };

        private string GetSeverityColor(AlertSeverity severity) => severity switch
        {
            AlertSeverity.Critical => "#dc3545",
            AlertSeverity.Error => "#fd7e14",
            AlertSeverity.Warning => "#ffc107",
            AlertSeverity.Info => "#0dcaf0",
            _ => "#6c757d"
        };
    }

    /// <summary>
    /// Options for Slack notifications
    /// </summary>
    public class SlackNotificationOptions
    {
        public bool Enabled { get; set; }
        public string WebhookUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public List<AlertType>? AlertTypes { get; set; }
        public string? Channel { get; set; }
        public string? Username { get; set; } = "ConduitLLM Health Monitor";
        public string? IconEmoji { get; set; } = ":robot_face:";
    }
}