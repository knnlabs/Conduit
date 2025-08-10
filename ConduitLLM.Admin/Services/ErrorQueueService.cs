using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Models.ErrorQueue;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing and monitoring error queues.
    /// </summary>
    public class ErrorQueueService : IErrorQueueService
    {
        private readonly IRabbitMQManagementClient _rabbitClient;
        private readonly ILogger<ErrorQueueService> _logger;
        private readonly IMemoryCache _cache;
        
        // Thresholds from Phase 1
        private const long WarningThreshold = 100;
        private const long CriticalThreshold = 1000;
        private const int CacheSeconds = 30;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorQueueService"/> class.
        /// </summary>
        /// <param name="rabbitClient">RabbitMQ management client.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="cache">Memory cache.</param>
        public ErrorQueueService(
            IRabbitMQManagementClient rabbitClient,
            ILogger<ErrorQueueService> logger,
            IMemoryCache cache)
        {
            _rabbitClient = rabbitClient ?? throw new ArgumentNullException(nameof(rabbitClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <inheritdoc/>
        public async Task<ErrorQueueListResponse> GetErrorQueuesAsync(
            bool includeEmpty = false,
            int? minMessages = null,
            string? queueNameFilter = null,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = $"error_queues_{includeEmpty}_{minMessages}_{queueNameFilter}";
            
            if (_cache.TryGetValue<ErrorQueueListResponse>(cacheKey, out var cached))
            {
                return cached!;
            }

            var allQueues = await _rabbitClient.GetQueuesAsync(cancellationToken);
            
            var errorQueues = allQueues
                .Where(q => q.Name.EndsWith("_error") || q.Name.EndsWith("_skipped"))
                .Where(q => includeEmpty || q.Messages > 0)
                .Where(q => minMessages == null || q.Messages >= minMessages)
                .Where(q => string.IsNullOrEmpty(queueNameFilter) || 
                           q.Name.Contains(queueNameFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var queueInfos = new List<ErrorQueueInfo>();
            var criticalQueues = new List<string>();
            var warningQueues = new List<string>();

            foreach (var queue in errorQueues)
            {
                var status = GetQueueStatus(queue.Messages);
                var queueInfo = new ErrorQueueInfo
                {
                    QueueName = queue.Name,
                    OriginalQueue = GetOriginalQueueName(queue.Name),
                    MessageCount = queue.Messages,
                    MessageBytes = queue.MessageBytes,
                    ConsumerCount = queue.Consumers,
                    MessageRate = queue.MessageStats?.PublishRate ?? 0,
                    Status = status
                };

                // Get message samples to determine timestamps
                if (queue.Messages > 0)
                {
                    var messages = await _rabbitClient.GetMessagesAsync(queue.Name, 1, cancellationToken);
                    var firstMessage = messages.FirstOrDefault();
                    if (firstMessage?.Properties.Timestamp != null)
                    {
                        queueInfo = queueInfo with
                        {
                            OldestMessageTimestamp = DateTimeOffset.FromUnixTimeSeconds(firstMessage.Properties.Timestamp.Value).UtcDateTime
                        };
                    }

                    // For newest, we'd need to get the last message which is expensive
                    // So we'll use the current time if messages are being added
                    if (queue.MessageStats?.PublishRate > 0)
                    {
                        queueInfo = queueInfo with { NewestMessageTimestamp = DateTime.UtcNow };
                    }
                }

                queueInfos.Add(queueInfo);

                if (status == "critical")
                    criticalQueues.Add(queue.Name);
                else if (status == "warning")
                    warningQueues.Add(queue.Name);
            }

            var response = new ErrorQueueListResponse
            {
                Queues = queueInfos.OrderByDescending(q => q.MessageCount).ToList(),
                Summary = new ErrorQueueSummary
                {
                    TotalQueues = queueInfos.Count,
                    TotalMessages = queueInfos.Sum(q => q.MessageCount),
                    TotalBytes = queueInfos.Sum(q => q.MessageBytes),
                    CriticalQueues = criticalQueues,
                    WarningQueues = warningQueues
                },
                Timestamp = DateTime.UtcNow
            };

            _cache.Set(cacheKey, response, TimeSpan.FromSeconds(CacheSeconds));
            
            return response;
        }

        /// <inheritdoc/>
        public async Task<ErrorMessageListResponse> GetErrorMessagesAsync(
            string queueName,
            int page = 1,
            int pageSize = 20,
            bool includeHeaders = true,
            bool includeBody = true,
            CancellationToken cancellationToken = default)
        {
            // Get total count first
            var queues = await _rabbitClient.GetQueuesAsync(cancellationToken);
            var queue = queues.FirstOrDefault(q => q.Name == queueName);
            
            if (queue == null)
            {
                return new ErrorMessageListResponse
                {
                    QueueName = queueName,
                    Messages = new List<ErrorMessage>(),
                    Page = page,
                    PageSize = pageSize,
                    TotalMessages = 0,
                    TotalPages = 0
                };
            }

            var totalMessages = queue.Messages;
            var totalPages = (int)Math.Ceiling((double)totalMessages / pageSize);

            // RabbitMQ doesn't support pagination directly, so we need to get more messages
            // and then paginate in memory (not ideal for large queues)
            var messagesToRetrieve = Math.Min(page * pageSize, 1000); // Limit to 1000 messages
            var rabbitMessages = await _rabbitClient.GetMessagesAsync(queueName, messagesToRetrieve, cancellationToken);
            
            var messages = rabbitMessages
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => ConvertToErrorMessage(m, includeHeaders, includeBody))
                .ToList();

            return new ErrorMessageListResponse
            {
                QueueName = queueName,
                Messages = messages,
                Page = page,
                PageSize = pageSize,
                TotalMessages = totalMessages,
                TotalPages = totalPages
            };
        }

        /// <inheritdoc/>
        public async Task<ErrorMessageDetail?> GetErrorMessageAsync(
            string queueName,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            var message = await _rabbitClient.GetMessageAsync(queueName, messageId, cancellationToken);
            
            if (message == null)
                return null;

            var errorMessage = ConvertToErrorMessage(message, true, true);
            
            return new ErrorMessageDetail
            {
                MessageId = errorMessage.MessageId,
                CorrelationId = errorMessage.CorrelationId,
                Timestamp = errorMessage.Timestamp,
                MessageType = errorMessage.MessageType,
                Headers = errorMessage.Headers,
                Body = errorMessage.Body,
                Error = errorMessage.Error,
                RetryCount = errorMessage.RetryCount,
                Context = ExtractContext(message),
                FullException = ExtractFullException(message)
            };
        }

        /// <inheritdoc/>
        public async Task<ErrorQueueStatistics> GetStatisticsAsync(
            DateTime since,
            string groupBy = "hour",
            CancellationToken cancellationToken = default)
        {
            var queues = await _rabbitClient.GetQueuesAsync(cancellationToken);
            var errorQueues = queues
                .Where(q => q.Name.EndsWith("_error") || q.Name.EndsWith("_skipped"))
                .ToList();

            // In a real implementation, we'd need to store historical data
            // For now, we'll provide current snapshot data
            var statistics = new ErrorQueueStatistics
            {
                Since = since,
                Until = DateTime.UtcNow,
                GroupBy = groupBy,
                TotalErrors = errorQueues.Sum(q => q.Messages),
                AverageMessageAgeHours = CalculateAverageMessageAge(errorQueues),
                ErrorRateTrends = GenerateErrorRateTrends(errorQueues, since, groupBy),
                TopFailingMessageTypes = await GetTopFailingMessageTypes(errorQueues, cancellationToken),
                QueueGrowthPatterns = GenerateQueueGrowthPatterns(errorQueues)
            };

            return statistics;
        }

        /// <inheritdoc/>
        public async Task<ErrorQueueHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            var queues = await GetErrorQueuesAsync(true, null, null, cancellationToken);
            
            var healthyCounts = queues.Queues.Count(q => q.Status == "ok");
            var warningCounts = queues.Queues.Count(q => q.Status == "warning");
            var criticalCounts = queues.Queues.Count(q => q.Status == "critical");
            
            var issues = new List<HealthIssue>();
            
            foreach (var queue in queues.Queues.Where(q => q.Status != "ok"))
            {
                issues.Add(new HealthIssue
                {
                    Severity = queue.Status == "critical" ? "critical" : "warning",
                    QueueName = queue.QueueName,
                    Description = queue.Status == "critical" 
                        ? $"Queue has {queue.MessageCount} messages (exceeds critical threshold of {CriticalThreshold})"
                        : $"Queue has {queue.MessageCount} messages (exceeds warning threshold of {WarningThreshold})",
                    SuggestedAction = queue.Status == "critical"
                        ? "Investigate immediately and consider manual intervention"
                        : "Monitor closely and investigate root cause"
                });
            }

            var overallStatus = criticalCounts > 0 ? "unhealthy" : 
                               warningCounts > 0 ? "degraded" : "healthy";
            
            var healthScore = 100 - (criticalCounts * 20) - (warningCounts * 10);
            healthScore = Math.Max(0, healthScore);

            return new ErrorQueueHealth
            {
                Status = overallStatus,
                Timestamp = DateTime.UtcNow,
                StatusCounts = new HealthStatusCounts
                {
                    Healthy = healthyCounts,
                    Warning = warningCounts,
                    Critical = criticalCounts
                },
                Issues = issues,
                HealthScore = healthScore
            };
        }

        private string GetQueueStatus(long messageCount)
        {
            if (messageCount >= CriticalThreshold)
                return "critical";
            if (messageCount >= WarningThreshold)
                return "warning";
            return "ok";
        }

        private string GetOriginalQueueName(string errorQueueName)
        {
            if (errorQueueName.EndsWith("_error"))
                return errorQueueName[..^6];
            if (errorQueueName.EndsWith("_skipped"))
                return errorQueueName[..^8];
            return errorQueueName;
        }

        private ErrorMessage ConvertToErrorMessage(RabbitMQMessage message, bool includeHeaders, bool includeBody)
        {
            var errorMessage = new ErrorMessage
            {
                MessageId = message.Properties.MessageId ?? Guid.NewGuid().ToString(),
                CorrelationId = message.Properties.CorrelationId ?? string.Empty,
                Timestamp = message.Properties.Timestamp != null 
                    ? DateTimeOffset.FromUnixTimeSeconds(message.Properties.Timestamp.Value).UtcDateTime
                    : DateTime.UtcNow,
                MessageType = message.Properties.Type ?? "Unknown",
                RetryCount = GetRetryCount(message.Properties.Headers)
            };

            if (includeHeaders)
            {
                errorMessage = errorMessage with { Headers = SanitizeHeaders(message.Properties.Headers) };
            }

            if (includeBody)
            {
                try
                {
                    var body = JsonSerializer.Deserialize<dynamic>(message.Payload);
                    errorMessage = errorMessage with { Body = body };
                }
                catch
                {
                    errorMessage = errorMessage with { Body = message.Payload };
                }
            }

            errorMessage = errorMessage with { Error = ExtractErrorDetails(message) };

            return errorMessage;
        }

        private Dictionary<string, object> SanitizeHeaders(Dictionary<string, object> headers)
        {
            var sanitized = new Dictionary<string, object>();
            var sensitiveKeys = new[] { "password", "token", "key", "secret", "credential" };

            foreach (var (key, value) in headers)
            {
                if (sensitiveKeys.Any(sk => key.Contains(sk, StringComparison.OrdinalIgnoreCase)))
                {
                    sanitized[key] = "***REDACTED***";
                }
                else
                {
                    sanitized[key] = value;
                }
            }

            return sanitized;
        }

        private int GetRetryCount(Dictionary<string, object> headers)
        {
            if (headers.TryGetValue("MT-Redelivery-Count", out var count))
            {
                if (int.TryParse(count.ToString(), out var retryCount))
                    return retryCount;
            }
            return 0;
        }

        private ErrorDetails ExtractErrorDetails(RabbitMQMessage message)
        {
            var headers = message.Properties.Headers;
            
            return new ErrorDetails
            {
                ExceptionType = headers.TryGetValue("MT-Fault-ExceptionType", out var exType) 
                    ? exType.ToString() ?? "Unknown" : "Unknown",
                Message = headers.TryGetValue("MT-Fault-Message", out var exMsg) 
                    ? exMsg.ToString() ?? "No error message" : "No error message",
                StackTrace = headers.TryGetValue("MT-Fault-StackTrace", out var stack) 
                    ? stack.ToString() : null,
                FailedAt = headers.TryGetValue("MT-Fault-Timestamp", out var timestamp) && 
                          DateTime.TryParse(timestamp.ToString(), out var failedAt)
                    ? failedAt : DateTime.UtcNow
            };
        }

        private Dictionary<string, object> ExtractContext(RabbitMQMessage message)
        {
            var context = new Dictionary<string, object>();
            
            if (message.Properties.Headers.TryGetValue("MT-Host-MachineName", out var machine))
                context["MachineName"] = machine;
            
            if (message.Properties.Headers.TryGetValue("MT-Host-ProcessName", out var process))
                context["ProcessName"] = process;
            
            if (message.Properties.Headers.TryGetValue("MT-Host-ProcessId", out var pid))
                context["ProcessId"] = pid;

            context["Exchange"] = message.Exchange;
            context["RoutingKey"] = message.RoutingKey;
            
            return context;
        }

        private string? ExtractFullException(RabbitMQMessage message)
        {
            if (message.Properties.Headers.TryGetValue("MT-Fault-StackTrace", out var stack))
            {
                return stack.ToString();
            }
            return null;
        }

        private double CalculateAverageMessageAge(List<QueueInfo> errorQueues)
        {
            // In a real implementation, we'd sample messages to calculate age
            // For now, return a placeholder
            return 24.5;
        }

        private List<ErrorRateTrend> GenerateErrorRateTrends(List<QueueInfo> errorQueues, DateTime since, string groupBy)
        {
            // In a real implementation, we'd need historical data
            // For now, generate sample trends
            var trends = new List<ErrorRateTrend>();
            var currentRate = errorQueues.Sum(q => q.MessageStats?.PublishRate ?? 0);
            
            trends.Add(new ErrorRateTrend
            {
                Period = DateTime.UtcNow.AddHours(-1),
                ErrorCount = (int)(currentRate * 60),
                ErrorsPerMinute = currentRate
            });

            return trends;
        }

        private async Task<List<FailingMessageType>> GetTopFailingMessageTypes(
            List<QueueInfo> errorQueues, 
            CancellationToken cancellationToken)
        {
            var messageTypes = new Dictionary<string, int>();
            
            foreach (var queue in errorQueues.Where(q => q.Messages > 0).Take(5))
            {
                var messages = await _rabbitClient.GetMessagesAsync(queue.Name, 10, cancellationToken);
                foreach (var message in messages)
                {
                    var type = message.Properties.Type ?? "Unknown";
                    messageTypes[type] = messageTypes.GetValueOrDefault(type, 0) + 1;
                }
            }

            var total = messageTypes.Values.Sum();
            
            return messageTypes
                .OrderByDescending(mt => mt.Value)
                .Take(5)
                .Select(mt => new FailingMessageType
                {
                    MessageType = mt.Key,
                    FailureCount = mt.Value,
                    Percentage = total > 0 ? (mt.Value * 100.0 / total) : 0,
                    MostCommonError = "Service unavailable" // Placeholder
                })
                .ToList();
        }

        private List<QueueGrowthPattern> GenerateQueueGrowthPatterns(List<QueueInfo> errorQueues)
        {
            return errorQueues
                .Where(q => q.Messages > 0)
                .Select(q => new QueueGrowthPattern
                {
                    QueueName = q.Name,
                    GrowthRate = q.MessageStats?.PublishRate ?? 0 * 60, // Messages per hour
                    Trend = DetermineTrend(q.MessageStats?.PublishRate ?? 0),
                    CurrentCount = q.Messages
                })
                .OrderByDescending(p => p.GrowthRate)
                .ToList();
        }

        private string DetermineTrend(double rate)
        {
            if (rate > 1) return "increasing";
            if (rate < -1) return "decreasing";
            return "stable";
        }
    }
}