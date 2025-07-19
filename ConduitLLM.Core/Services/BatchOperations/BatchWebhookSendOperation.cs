using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services.BatchOperations
{
    /// <summary>
    /// Batch operation for sending webhooks to multiple endpoints
    /// </summary>
    public class BatchWebhookSendOperation : IBatchWebhookSendOperation
    {
        private readonly ILogger<BatchWebhookSendOperation> _logger;
        private readonly IBatchOperationService _batchOperationService;
        private readonly IWebhookDeliveryService _webhookDeliveryService;
        private readonly IHttpClientFactory _httpClientFactory;

        public BatchWebhookSendOperation(
            ILogger<BatchWebhookSendOperation> logger,
            IBatchOperationService batchOperationService,
            IWebhookDeliveryService webhookDeliveryService,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchOperationService = batchOperationService ?? throw new ArgumentNullException(nameof(batchOperationService));
            _webhookDeliveryService = webhookDeliveryService ?? throw new ArgumentNullException(nameof(webhookDeliveryService));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        /// <summary>
        /// Execute batch webhook send operation
        /// </summary>
        public async Task<BatchOperationResult> ExecuteAsync(
            List<WebhookSendItem> webhooks,
            int virtualKeyId,
            CancellationToken cancellationToken = default)
        {
            var options = new BatchOperationOptions
            {
                VirtualKeyId = virtualKeyId,
                MaxDegreeOfParallelism = 20, // Higher parallelism for HTTP operations
                ContinueOnError = true,
                EnableCheckpointing = true,
                CheckpointInterval = 100,
                ItemTimeout = TimeSpan.FromSeconds(30),
                Metadata = new Dictionary<string, object>
                {
                    ["webhookType"] = "batch_notification",
                    ["source"] = "batch_operation"
                }
            };

            return await _batchOperationService.StartBatchOperationAsync(
                "webhook_send",
                webhooks,
                ProcessWebhookSendAsync,
                options,
                cancellationToken);
        }

        private async Task<BatchItemResult> ProcessWebhookSendAsync(
            WebhookSendItem item,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var httpClient = _httpClientFactory.CreateClient("webhook");
            
            try
            {
                // Prepare webhook payload
                var payload = new
                {
                    id = Guid.NewGuid().ToString(),
                    timestamp = DateTime.UtcNow,
                    type = item.EventType,
                    data = item.Payload,
                    metadata = new
                    {
                        virtualKeyId = item.VirtualKeyId,
                        batchOperation = true,
                        source = "conduit"
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Add headers
                var request = new HttpRequestMessage(HttpMethod.Post, item.WebhookUrl)
                {
                    Content = content
                };

                if (item.Headers != null)
                {
                    foreach (var header in item.Headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                // Add signature if secret provided
                if (!string.IsNullOrEmpty(item.Secret))
                {
                    var signature = ComputeSignature(json, item.Secret);
                    request.Headers.Add("X-Webhook-Signature", signature);
                }

                // Send webhook
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                var response = await httpClient.SendAsync(request, cts.Token);

                // Notify delivery status
                if (response.IsSuccessStatusCode)
                {
                    await _webhookDeliveryService.NotifyDeliverySuccessAsync(
                        item.WebhookUrl,
                        (int)response.StatusCode,
                        stopwatch.Elapsed);

                    return new BatchItemResult
                    {
                        Success = true,
                        ItemIdentifier = item.WebhookUrl,
                        Duration = stopwatch.Elapsed,
                        Data = new
                        {
                            StatusCode = (int)response.StatusCode,
                            ResponseTime = stopwatch.ElapsedMilliseconds
                        }
                    };
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    
                    await _webhookDeliveryService.NotifyDeliveryFailureAsync(
                        item.WebhookUrl,
                        (int)response.StatusCode,
                        responseBody,
                        isRetryable: response.StatusCode != System.Net.HttpStatusCode.BadRequest);

                    return new BatchItemResult
                    {
                        Success = false,
                        ItemIdentifier = item.WebhookUrl,
                        Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                        Duration = stopwatch.Elapsed
                    };
                }
            }
            catch (TaskCanceledException)
            {
                await _webhookDeliveryService.NotifyDeliveryFailureAsync(
                    item.WebhookUrl,
                    0,
                    "Request timeout",
                    isRetryable: true);

                return new BatchItemResult
                {
                    Success = false,
                    ItemIdentifier = item.WebhookUrl,
                    Error = "Request timeout",
                    Duration = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to send webhook to {WebhookUrl}", 
                    item.WebhookUrl);

                await _webhookDeliveryService.NotifyDeliveryFailureAsync(
                    item.WebhookUrl,
                    0,
                    ex.Message,
                    isRetryable: true);

                return new BatchItemResult
                {
                    Success = false,
                    ItemIdentifier = item.WebhookUrl,
                    Error = ex.Message,
                    Duration = stopwatch.Elapsed
                };
            }
        }

        private string ComputeSignature(string payload, string secret)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(hash);
        }
    }
}