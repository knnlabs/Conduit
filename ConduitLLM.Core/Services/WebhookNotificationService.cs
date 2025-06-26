using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for sending webhook notifications to external endpoints.
    /// </summary>
    public class WebhookNotificationService : IWebhookNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebhookNotificationService> _logger;

        public WebhookNotificationService(
            HttpClient httpClient,
            ILogger<WebhookNotificationService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Set reasonable timeout for webhook calls
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <inheritdoc/>
        public async Task<bool> SendTaskCompletionWebhookAsync(
            string webhookUrl,
            object payload,
            Dictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default)
        {
            return await SendWebhookAsync(webhookUrl, payload, headers, "completion", null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> SendTaskProgressWebhookAsync(
            string webhookUrl,
            object payload,
            Dictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default)
        {
            return await SendWebhookAsync(webhookUrl, payload, headers, "progress", null, cancellationToken);
        }

        /// <summary>
        /// Sends a webhook with custom timeout support.
        /// </summary>
        public async Task<bool> SendWebhookAsync(
            string webhookUrl,
            object payload,
            Dictionary<string, string>? headers = null,
            TimeSpan? customTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return await SendWebhookAsync(webhookUrl, payload, headers, "custom", customTimeout, cancellationToken);
        }

        private async Task<bool> SendWebhookAsync(
            string webhookUrl,
            object payload,
            Dictionary<string, string>? headers,
            string webhookType,
            TimeSpan? customTimeout,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Sending {WebhookType} webhook to {WebhookUrl} with timeout {Timeout}s", 
                    webhookType, webhookUrl, customTimeout?.TotalSeconds ?? _httpClient.Timeout.TotalSeconds);

                using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl);
                
                // Add custom headers if provided
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                // Add standard webhook headers
                request.Headers.TryAddWithoutValidation("X-Webhook-Type", webhookType);
                request.Headers.TryAddWithoutValidation("X-Webhook-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

                // Add content
                request.Content = JsonContent.Create(payload);

                // Apply custom timeout if specified
                using var cts = customTimeout.HasValue 
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                if (customTimeout.HasValue)
                {
                    cts.CancelAfter(customTimeout.Value);
                }

                // Send the webhook
                using var response = await _httpClient.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully sent {WebhookType} webhook to {WebhookUrl} with status {StatusCode}",
                        webhookType, webhookUrl, response.StatusCode);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to send {WebhookType} webhook to {WebhookUrl}. Status: {StatusCode}, Reason: {ReasonPhrase}",
                        webhookType, webhookUrl, response.StatusCode, response.ReasonPhrase);
                    return false;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || customTimeout.HasValue)
            {
                var timeoutDuration = customTimeout?.TotalSeconds ?? _httpClient.Timeout.TotalSeconds;
                _logger.LogWarning("Webhook request to {WebhookUrl} timed out after {Timeout}s", 
                    webhookUrl, timeoutDuration);
                return false;
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Webhook request to {WebhookUrl} was cancelled", webhookUrl);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error sending {WebhookType} webhook to {WebhookUrl}. Error: {ErrorMessage}", 
                    webhookType, webhookUrl, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending {WebhookType} webhook to {WebhookUrl}", 
                    webhookType, webhookUrl);
                return false;
            }
        }
    }
}