using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Http.DTOs.HealthMonitoring;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for sending alert notifications through various channels
    /// </summary>
    public interface IAlertNotificationService
    {
        Task SendAlertAsync(HealthAlert alert, CancellationToken cancellationToken = default);
        Task SendBatchAlertsAsync(IEnumerable<HealthAlert> alerts, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implementation of alert notification service
    /// </summary>
    public class AlertNotificationService : IAlertNotificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AlertNotificationService> _logger;
        private readonly AlertNotificationOptions _options;
        private readonly IAlertNotificationChannel[] _channels;

        public AlertNotificationService(
            IHttpClientFactory httpClientFactory,
            ILogger<AlertNotificationService> logger,
            IOptions<AlertNotificationOptions> options,
            IEnumerable<IAlertNotificationChannel> channels)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _options = options.Value;
            _channels = channels.ToArray();
        }

        /// <summary>
        /// Send a single alert through all configured channels
        /// </summary>
        public async Task SendAlertAsync(HealthAlert alert, CancellationToken cancellationToken = default)
        {
            if (!ShouldSendAlert(alert))
            {
                _logger.LogDebug("Alert {AlertId} filtered out by severity threshold", alert.Id);
                return;
            }

            var tasks = _channels
                .Where(c => c.IsEnabled && c.SupportsAlertType(alert.Type))
                .Select(channel => SendAlertToChannelAsync(channel, alert, cancellationToken));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Send multiple alerts in batch
        /// </summary>
        public async Task SendBatchAlertsAsync(IEnumerable<HealthAlert> alerts, CancellationToken cancellationToken = default)
        {
            var filteredAlerts = alerts.Where(ShouldSendAlert).ToList();
            
            if (!filteredAlerts.Any())
            {
                _logger.LogDebug("All alerts filtered out by severity threshold");
                return;
            }

            var tasks = _channels
                .Where(c => c.IsEnabled)
                .Select(channel => channel.SupportsBatchSending
                    ? channel.SendBatchAsync(filteredAlerts, cancellationToken)
                    : SendAlertsIndividuallyAsync(channel, filteredAlerts, cancellationToken));

            await Task.WhenAll(tasks);
        }

        private async Task SendAlertToChannelAsync(IAlertNotificationChannel channel, HealthAlert alert, CancellationToken cancellationToken)
        {
            try
            {
                await channel.SendAsync(alert, cancellationToken);
                _logger.LogInformation("Alert {AlertId} sent successfully via {Channel}", 
                    alert.Id, channel.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send alert {AlertId} via {Channel}", 
                    alert.Id, channel.Name);
            }
        }

        private async Task SendAlertsIndividuallyAsync(IAlertNotificationChannel channel, List<HealthAlert> alerts, CancellationToken cancellationToken)
        {
            foreach (var alert in alerts)
            {
                if (channel.SupportsAlertType(alert.Type))
                {
                    await SendAlertToChannelAsync(channel, alert, cancellationToken);
                }
            }
        }

        private bool ShouldSendAlert(HealthAlert alert)
        {
            return alert.Severity >= _options.MinimumSeverity;
        }
    }

    /// <summary>
    /// Interface for alert notification channels
    /// </summary>
    public interface IAlertNotificationChannel
    {
        string Name { get; }
        bool IsEnabled { get; }
        bool SupportsBatchSending { get; }
        bool SupportsAlertType(AlertType alertType);
        Task SendAsync(HealthAlert alert, CancellationToken cancellationToken = default);
        Task SendBatchAsync(IEnumerable<HealthAlert> alerts, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Webhook notification channel
    /// </summary>
    public class WebhookNotificationChannel : IAlertNotificationChannel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebhookNotificationChannel> _logger;
        private readonly WebhookNotificationOptions _options;

        public string Name => "Webhook";
        public bool IsEnabled => _options.Enabled && !string.IsNullOrEmpty(_options.Url);
        public bool SupportsBatchSending => true;

        public WebhookNotificationChannel(
            IHttpClientFactory httpClientFactory,
            ILogger<WebhookNotificationChannel> logger,
            IOptions<WebhookNotificationOptions> options)
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
            var payload = new
            {
                type = "health_alert",
                alert = alert,
                timestamp = DateTime.UtcNow,
                source = "ConduitLLM"
            };

            await SendWebhookAsync(payload, cancellationToken);
        }

        public async Task SendBatchAsync(IEnumerable<HealthAlert> alerts, CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                type = "health_alert_batch",
                alerts = alerts,
                count = alerts.Count(),
                timestamp = DateTime.UtcNow,
                source = "ConduitLLM"
            };

            await SendWebhookAsync(payload, cancellationToken);
        }

        private async Task SendWebhookAsync(object payload, CancellationToken cancellationToken)
        {
            using var httpClient = _httpClientFactory.CreateClient("AlertWebhook");
            httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add custom headers if configured
            if (_options.Headers != null)
            {
                foreach (var header in _options.Headers)
                {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            var response = await httpClient.PostAsync(_options.Url, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Webhook failed with status {StatusCode}: {Response}", 
                    response.StatusCode, responseBody);
                throw new HttpRequestException($"Webhook failed with status {response.StatusCode}");
            }
        }
    }

    /// <summary>
    /// Email notification channel
    /// </summary>
    public class EmailNotificationChannel : IAlertNotificationChannel
    {
        private readonly ILogger<EmailNotificationChannel> _logger;
        private readonly EmailNotificationOptions _options;

        public string Name => "Email";
        public bool IsEnabled => _options.Enabled && !string.IsNullOrEmpty(_options.SmtpServer);
        public bool SupportsBatchSending => true;

        public EmailNotificationChannel(
            ILogger<EmailNotificationChannel> logger,
            IOptions<EmailNotificationOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public bool SupportsAlertType(AlertType alertType)
        {
            return _options.AlertTypes?.Contains(alertType) ?? true;
        }

        public async Task SendAsync(HealthAlert alert, CancellationToken cancellationToken = default)
        {
            var subject = $"[{alert.Severity}] ConduitLLM Alert: {alert.Title}";
            var body = FormatAlertEmail(alert);

            await SendEmailAsync(subject, body, cancellationToken);
        }

        public async Task SendBatchAsync(IEnumerable<HealthAlert> alerts, CancellationToken cancellationToken = default)
        {
            var alertsList = alerts.ToList();
            var subject = $"ConduitLLM Alert Summary ({alertsList.Count} alerts)";
            var body = FormatBatchAlertEmail(alertsList);

            await SendEmailAsync(subject, body, cancellationToken);
        }

        private async Task SendEmailAsync(string subject, string body, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new SmtpClient(_options.SmtpServer, _options.SmtpPort)
                {
                    EnableSsl = _options.UseSsl,
                    Timeout = _options.TimeoutSeconds * 1000
                };

                if (!string.IsNullOrEmpty(_options.Username))
                {
                    client.Credentials = new System.Net.NetworkCredential(_options.Username, _options.Password);
                }

                var message = new MailMessage
                {
                    From = new MailAddress(_options.FromAddress, _options.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                foreach (var recipient in _options.Recipients)
                {
                    message.To.Add(recipient);
                }

                await client.SendMailAsync(message, cancellationToken);
                _logger.LogInformation("Email notification sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notification");
                throw;
            }
        }

        private string FormatAlertEmail(HealthAlert alert)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body style='font-family: Arial, sans-serif;'>");
            sb.AppendLine($"<h2 style='color: {GetSeverityColor(alert.Severity)}'>Health Alert: {alert.Title}</h2>");
            sb.AppendLine($"<p><strong>Component:</strong> {alert.Component}</p>");
            sb.AppendLine($"<p><strong>Severity:</strong> {alert.Severity}</p>");
            sb.AppendLine($"<p><strong>Type:</strong> {alert.Type}</p>");
            sb.AppendLine($"<p><strong>Time:</strong> {alert.TriggeredAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
            sb.AppendLine($"<p><strong>Message:</strong> {alert.Message}</p>");

            if (alert.SuggestedActions.Any())
            {
                sb.AppendLine("<h3>Suggested Actions:</h3>");
                sb.AppendLine("<ul>");
                foreach (var action in alert.SuggestedActions)
                {
                    sb.AppendLine($"<li>{action}</li>");
                }
                sb.AppendLine("</ul>");
            }

            if (alert.Context.Any())
            {
                sb.AppendLine("<h3>Additional Context:</h3>");
                sb.AppendLine("<pre style='background-color: #f5f5f5; padding: 10px;'>");
                sb.AppendLine(JsonSerializer.Serialize(alert.Context, new JsonSerializerOptions { WriteIndented = true }));
                sb.AppendLine("</pre>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private string FormatBatchAlertEmail(List<HealthAlert> alerts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body style='font-family: Arial, sans-serif;'>");
            sb.AppendLine($"<h2>ConduitLLM Alert Summary</h2>");
            sb.AppendLine($"<p>Total alerts: {alerts.Count}</p>");

            var groupedBySeverity = alerts.GroupBy(a => a.Severity).OrderByDescending(g => g.Key);
            foreach (var group in groupedBySeverity)
            {
                sb.AppendLine($"<h3 style='color: {GetSeverityColor(group.Key)}'>{group.Key}: {group.Count()} alerts</h3>");
                sb.AppendLine("<ul>");
                foreach (var alert in group)
                {
                    sb.AppendLine($"<li><strong>{alert.Component}</strong>: {alert.Title} ({alert.TriggeredAt:HH:mm:ss})</li>");
                }
                sb.AppendLine("</ul>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

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
    /// Options for alert notifications
    /// </summary>
    public class AlertNotificationOptions
    {
        public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.Warning;
        public bool EnableBatching { get; set; } = true;
        public int BatchIntervalSeconds { get; set; } = 300; // 5 minutes
        public int MaxBatchSize { get; set; } = 50;
    }

    /// <summary>
    /// Options for webhook notifications
    /// </summary>
    public class WebhookNotificationOptions
    {
        public bool Enabled { get; set; }
        public string Url { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public Dictionary<string, string>? Headers { get; set; }
        public List<AlertType>? AlertTypes { get; set; }
    }

    /// <summary>
    /// Options for email notifications
    /// </summary>
    public class EmailNotificationOptions
    {
        public bool Enabled { get; set; }
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "ConduitLLM Alerts";
        public List<string> Recipients { get; set; } = new();
        public int TimeoutSeconds { get; set; } = 30;
        public List<AlertType>? AlertTypes { get; set; }
    }
}