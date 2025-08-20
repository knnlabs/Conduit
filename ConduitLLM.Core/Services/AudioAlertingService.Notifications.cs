using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class AudioAlertingService
    {
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
    }
}