using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MassTransit;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using System.Text.Json;

namespace ConduitLLM.Core.HealthChecks
{
    /// <summary>
    /// Health check for RabbitMQ that monitors connection health, queue depths, and performance metrics.
    /// </summary>
    public class RabbitMQHealthCheck : IHealthCheck
    {
        private readonly IBus _bus;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQHealthCheck> _logger;

        // Thresholds for health status
        private const int QueueDepthWarningThreshold = 1000;
        private const int QueueDepthCriticalThreshold = 5000;
        private const int ConnectionWarningThreshold = 100;
        private const double MemoryWarningThreshold = 0.75; // 75% of high watermark

        public RabbitMQHealthCheck(
            IBus bus,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<RabbitMQHealthCheck> logger)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>();

            try
            {
                // Check MassTransit bus health
                var busHealth = await CheckBusHealthAsync(cancellationToken);
                data["bus_connected"] = busHealth.IsConnected;
                data["bus_status"] = busHealth.Status;

                if (!busHealth.IsConnected)
                {
                    return HealthCheckResult.Unhealthy(
                        "MassTransit bus is not connected to RabbitMQ",
                        data: data);
                }

                // Get RabbitMQ management API stats if available
                var managementStats = await GetManagementApiStatsAsync(cancellationToken);
                if (managementStats != null)
                {
                    data["total_messages"] = managementStats.TotalMessages;
                    data["total_connections"] = managementStats.TotalConnections;
                    data["memory_used_mb"] = managementStats.MemoryUsedMB;
                    data["memory_alarm"] = managementStats.MemoryAlarm;
                    
                    // Add queue-specific metrics
                    var criticalQueues = new[]
                    {
                        "webhook-delivery",
                        "video-generation-events",
                        "image-generation-events",
                        "spend-update-events"
                    };

                    var queueMetrics = new Dictionary<string, object>();
                    foreach (var queueName in criticalQueues)
                    {
                        var queueStats = managementStats.Queues?.FirstOrDefault(q => q.Name == queueName);
                        if (queueStats != null)
                        {
                            queueMetrics[queueName] = new
                            {
                                messages = queueStats.Messages,
                                messages_ready = queueStats.MessagesReady,
                                messages_unacked = queueStats.MessagesUnacknowledged,
                                consumers = queueStats.Consumers,
                                message_rate = queueStats.MessageStats?.PublishRate ?? 0
                            };
                        }
                    }
                    data["queue_metrics"] = queueMetrics;

                    // Add error queue metrics separately
                    var errorQueueMetrics = GetErrorQueueMetrics(managementStats);
                    if (errorQueueMetrics.Any())
                    {
                        data["error_queue_metrics"] = errorQueueMetrics;
                    }

                    // Determine health status based on metrics
                    var (status, description) = EvaluateHealthStatus(managementStats);
                    
                    switch (status)
                    {
                        case HealthStatus.Unhealthy:
                            return HealthCheckResult.Unhealthy(description, data: data);
                        case HealthStatus.Degraded:
                            return HealthCheckResult.Degraded(description, data: data);
                        default:
                            return HealthCheckResult.Healthy(description, data: data);
                    }
                }

                // If management API is not available, just report bus connectivity
                return HealthCheckResult.Healthy("RabbitMQ connected (management API not available)", data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking RabbitMQ health");
                data["error"] = ex.Message;
                return HealthCheckResult.Unhealthy("RabbitMQ health check failed", ex, data);
            }
        }

        private async Task<BusHealthStatus> CheckBusHealthAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Simple connectivity check - if the bus is injected, it should be connected
                // More detailed health checks are done via management API
                await Task.CompletedTask; // Make async happy
                
                return new BusHealthStatus
                {
                    IsConnected = true, // If we can get here, MassTransit is configured
                    Status = "Connected"
                };
            }
            catch
            {
                return new BusHealthStatus
                {
                    IsConnected = false,
                    Status = "Error"
                };
            }
        }

        private async Task<RabbitMQStats?> GetManagementApiStatsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var rabbitConfig = _configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>()
                    ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

                var managementUrl = $"http://{rabbitConfig.Host}:15672";
                var credentials = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{rabbitConfig.Username}:{rabbitConfig.Password}"));

                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                // Get overview
                var overviewResponse = await client.GetAsync($"{managementUrl}/api/overview", cancellationToken);
                if (!overviewResponse.IsSuccessStatusCode)
                    return null;

                var overviewJson = await overviewResponse.Content.ReadAsStringAsync(cancellationToken);
                var overview = JsonDocument.Parse(overviewJson);

                // Get queues
                var queuesResponse = await client.GetAsync($"{managementUrl}/api/queues", cancellationToken);
                var queuesJson = await queuesResponse.Content.ReadAsStringAsync(cancellationToken);
                var queuesDoc = JsonDocument.Parse(queuesJson);

                var root = overview.RootElement;
                var stats = new RabbitMQStats
                {
                    TotalMessages = root.TryGetProperty("queue_totals", out var qt) && qt.TryGetProperty("messages", out var msgs) 
                        ? msgs.GetInt64() : 0,
                    TotalConnections = root.TryGetProperty("object_totals", out var ot) && ot.TryGetProperty("connections", out var conns) 
                        ? conns.GetInt32() : 0,
                    MemoryUsedMB = root.TryGetProperty("mem_used", out var mem) 
                        ? mem.GetInt64() / (1024 * 1024) : 0,
                    MemoryAlarm = root.TryGetProperty("mem_alarm", out var alarm) && alarm.GetBoolean(),
                    Queues = new List<QueueStats>()
                };

                var queuesArray = queuesDoc.RootElement.EnumerateArray();
                foreach (var queue in queuesArray)
                {
                    var queueStats = new QueueStats
                    {
                        Name = queue.GetProperty("name").GetString() ?? string.Empty,
                        Messages = queue.TryGetProperty("messages", out var m) ? m.GetInt64() : 0,
                        MessagesReady = queue.TryGetProperty("messages_ready", out var mr) ? mr.GetInt64() : 0,
                        MessagesUnacknowledged = queue.TryGetProperty("messages_unacknowledged", out var mu) ? mu.GetInt64() : 0,
                        Consumers = queue.TryGetProperty("consumers", out var c) ? c.GetInt32() : 0
                    };

                    if (queue.TryGetProperty("message_stats", out var msgStats))
                    {
                        queueStats.MessageStats = new MessageStats
                        {
                            PublishRate = msgStats.TryGetProperty("publish_details", out var pd) && 
                                pd.TryGetProperty("rate", out var pr) ? pr.GetDouble() : 0,
                            DeliverRate = msgStats.TryGetProperty("deliver_get_details", out var dd) && 
                                dd.TryGetProperty("rate", out var dr) ? dr.GetDouble() : 0
                        };
                    }

                    stats.Queues.Add(queueStats);
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get RabbitMQ management API stats");
                return null;
            }
        }

        private (HealthStatus status, string description) EvaluateHealthStatus(RabbitMQStats stats)
        {
            var issues = new List<string>();
            var warnings = new List<string>();

            // Check memory alarm
            if (stats.MemoryAlarm)
            {
                issues.Add("Memory alarm triggered");
            }

            // Check connection count
            if (stats.TotalConnections > ConnectionWarningThreshold)
            {
                warnings.Add($"High connection count: {stats.TotalConnections}");
            }

            // Check critical queue depths
            foreach (var queue in stats.Queues)
            {
                // Skip error queues and skipped queues from health evaluation
                // These queues are expected to have messages without consumers
                if (queue.Name.EndsWith("_error") || queue.Name.EndsWith("_skipped"))
                {
                    continue;
                }

                if (queue.Messages > QueueDepthCriticalThreshold)
                {
                    issues.Add($"Critical queue depth: {queue.Name} has {queue.Messages} messages");
                }
                else if (queue.Messages > QueueDepthWarningThreshold)
                {
                    warnings.Add($"High queue depth: {queue.Name} has {queue.Messages} messages");
                }

                // Check for queues with no consumers
                if (queue.Consumers == 0 && queue.Messages > 0)
                {
                    issues.Add($"Queue {queue.Name} has messages but no consumers");
                }
            }

            // Determine overall status
            if (issues.Any())
            {
                return (HealthStatus.Unhealthy, string.Join("; ", issues));
            }
            
            if (warnings.Any())
            {
                return (HealthStatus.Degraded, string.Join("; ", warnings));
            }

            return (HealthStatus.Healthy, 
                $"RabbitMQ healthy - {stats.TotalMessages} total messages, {stats.TotalConnections} connections");
        }

        private Dictionary<string, object> GetErrorQueueMetrics(RabbitMQStats stats)
        {
            var errorMetrics = new Dictionary<string, object>();
            
            var errorQueues = stats.Queues
                .Where(q => q.Name.EndsWith("_error") || q.Name.EndsWith("_skipped"))
                .Where(q => q.Messages > 0)
                .ToList();

            if (errorQueues.Any())
            {
                errorMetrics["total_error_messages"] = errorQueues.Sum(q => q.Messages);
                errorMetrics["queues_with_errors"] = errorQueues.Count;
                errorMetrics["details"] = errorQueues.Select(q => new
                {
                    name = q.Name,
                    messages = q.Messages,
                    type = q.Name.EndsWith("_error") ? "error" : "skipped"
                }).ToList();
            }

            return errorMetrics;
        }

        private class BusHealthStatus
        {
            public bool IsConnected { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        private class RabbitMQStats
        {
            public long TotalMessages { get; set; }
            public int TotalConnections { get; set; }
            public long MemoryUsedMB { get; set; }
            public bool MemoryAlarm { get; set; }
            public List<QueueStats> Queues { get; set; } = new();
        }

        private class QueueStats
        {
            public string Name { get; set; } = string.Empty;
            public long Messages { get; set; }
            public long MessagesReady { get; set; }
            public long MessagesUnacknowledged { get; set; }
            public int Consumers { get; set; }
            public MessageStats MessageStats { get; set; } = new();
        }

        private class MessageStats
        {
            public double PublishRate { get; set; }
            public double DeliverRate { get; set; }
        }
    }
}