using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.Metrics;
using ConduitLLM.Configuration.Interfaces;
using MetricsCollectionInstrumentation = ConduitLLM.Gateway.Metrics.MetricsCollectionInstrumentation;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Metrics collection methods for MetricsAggregationService
    /// </summary>
    public partial class MetricsAggregationService
    {
        /// <summary>
        /// Collect HTTP-related metrics
        /// </summary>
        private void CollectHttpMetrics(MetricsSnapshot snapshot)
        {
            try
            {
                // In a production environment, you would query Prometheus or use proper metrics collection
                // For now, we'll use the static metric values available from the Prometheus counters/gauges
                // Note: Direct access to metric values is limited in prometheus-net for security reasons
                
                // Calculate request rate (simplified - in production use proper time windows)
                var totalRequests = GetMetricValue("conduit_http_requests_total");
                snapshot.Http.RequestsPerSecond = totalRequests / 60.0; // Approximate

                // Get active requests
                var activeRequestsMetric = GetMetricValue("conduit_http_requests_active");
                snapshot.Http.ActiveRequests = (int)activeRequestsMetric;

                // Get response time percentiles from summary
                var p50 = GetMetricValue("conduit_http_request_duration_summary{quantile=\"0.5\"}");
                var p90 = GetMetricValue("conduit_http_request_duration_summary{quantile=\"0.9\"}");
                var p95 = GetMetricValue("conduit_http_request_duration_summary{quantile=\"0.95\"}");
                var p99 = GetMetricValue("conduit_http_request_duration_summary{quantile=\"0.99\"}");

                snapshot.Http.ResponseTimes = new ResponseTimePercentiles
                {
                    P50 = p50 * 1000, // Convert to milliseconds
                    P90 = p90 * 1000,
                    P95 = p95 * 1000,
                    P99 = p99 * 1000
                };

                // Calculate error rate
                var totalRequestsCount = GetMetricValue("conduit_http_requests_total");
                var errorRequests = GetMetricValue("conduit_http_requests_total{status_code=~\"5..\"}");
                snapshot.Http.ErrorRate = totalRequestsCount > 0 ? (errorRequests / totalRequestsCount) * 100 : 0;

                // Get rate limit hits
                var rateLimitHits = GetMetricValue("conduit_rate_limit_exceeded_total");
                snapshot.Http.RateLimitHitsPerMinute = (int)(rateLimitHits / 60.0);

                // Get endpoint request rates (top 5)
                // In production, this would query Prometheus properly
                snapshot.Http.EndpointRequestRates = new Dictionary<string, double>
                {
                    { "/v1/chat/completions", GetMetricValue("conduit_http_requests_total{endpoint=\"/v1/chat/completions\"}") / 60.0 },
                    { "/v1/embeddings", GetMetricValue("conduit_http_requests_total{endpoint=\"/v1/embeddings\"}") / 60.0 },
                    { "/v1/models", GetMetricValue("conduit_http_requests_total{endpoint=\"/v1/models\"}") / 60.0 },
                    { "/v1/images/generations", GetMetricValue("conduit_http_requests_total{endpoint=\"/v1/images/generations\"}") / 60.0 }
                };

                // Status code distribution
                snapshot.Http.StatusCodeCounts = new Dictionary<int, int>
                {
                    { 200, (int)GetMetricValue("conduit_http_requests_total{status_code=\"200\"}") },
                    { 400, (int)GetMetricValue("conduit_http_requests_total{status_code=\"400\"}") },
                    { 401, (int)GetMetricValue("conduit_http_requests_total{status_code=\"401\"}") },
                    { 429, (int)GetMetricValue("conduit_http_requests_total{status_code=\"429\"}") },
                    { 500, (int)GetMetricValue("conduit_http_requests_total{status_code=\"500\"}") }
                };
            }
            catch (Exception ex)
            {
                MetricsCollectionInstrumentation.RecordFailure("metrics_aggregation/http");
                _logger.LogError(ex, "Error collecting HTTP metrics");
            }
        }

        /// <summary>
        /// Collect infrastructure-related metrics
        /// </summary>
        private void CollectInfrastructureMetrics(MetricsSnapshot snapshot)
        {
            try
            {
                // Infrastructure metrics are collected through external monitoring tools
                // Setting default values for now
                snapshot.Infrastructure.Database = new DatabaseMetrics
                {
                    ActiveConnections = 0,
                    AvailableConnections = 0,
                    AverageQueryDuration = 0,
                    ErrorsPerMinute = 0,
                    PoolUtilization = 0
                };

                // Check Redis connectivity only
                using var scope = _serviceProvider.CreateScope();
                var redis = scope.ServiceProvider.GetService<IConnectionMultiplexer>();
                snapshot.Infrastructure.Redis = new RedisMetrics
                {
                    IsConnected = redis != null && redis.IsConnected,
                    MemoryUsageMB = 0,
                    KeyCount = 0,
                    ConnectedClients = 0,
                    OperationsPerSecond = 0,
                    AverageLatency = 0,
                    HitRate = 0
                };

                // RabbitMQ basic connectivity
                snapshot.Infrastructure.RabbitMQ = new RabbitMQMetrics
                {
                    IsConnected = true, // Assumed true if the service is running
                    MessagesPublishedPerMinute = 0,
                    MessagesConsumedPerMinute = 0
                };

                // SignalR metrics
                snapshot.Infrastructure.SignalR = new SignalRMetrics
                {
                    ActiveConnections = (int)GetMetricValue("signalr_connections_active"),
                    MessagesSentPerMinute = (int)(GetMetricValue("signalr_messages_sent_total") / 60.0),
                    MessagesReceivedPerMinute = (int)(GetMetricValue("signalr_messages_received_total") / 60.0),
                    HubInvocationsPerMinute = (int)(GetMetricValue("signalr_hub_method_invocations_total") / 60.0),
                    ReconnectionsPerMinute = (int)(GetMetricValue("signalr_reconnection_attempts_total") / 60.0),
                    AverageMessageProcessingTime = GetMetricValue("signalr_message_processing_duration_seconds_sum") * 1000
                };
            }
            catch (Exception ex)
            {
                MetricsCollectionInstrumentation.RecordFailure("metrics_aggregation/infrastructure");
                _logger.LogError(ex, "Error collecting infrastructure metrics");
            }
        }

        /// <summary>
        /// Collect business-related metrics
        /// </summary>
        private async Task CollectBusinessMetricsAsync(MetricsSnapshot snapshot)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitDbContext>>();
                await using var context = await dbContextFactory.CreateDbContextAsync();
                var windowEnd = DateTime.UtcNow;
                var windowStart = windowEnd.AddMinutes(-1);

                var virtualKeyRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeyRepository>();
                snapshot.Business.ActiveVirtualKeys = await virtualKeyRepo.CountActiveAsync();

                var recentRequests = context.RequestLogs
                    .AsNoTracking()
                    .Where(log => log.Timestamp >= windowStart && log.Timestamp < windowEnd);
                snapshot.Business.TotalRequestsPerMinute = await recentRequests.CountAsync();

                // Costs remain attributable even if a provider is disabled after serving the
                // request, so usage rows (rather than the current provider list) are authoritative.
                var billedRequests = context.RequestLogs
                    .AsNoTracking()
                    .Where(log => (log.BilledAtUtc ?? log.Timestamp) >= windowStart &&
                                  (log.BilledAtUtc ?? log.Timestamp) < windowEnd);
                var costSummary = await billedRequests
                    .GroupBy(_ => 1)
                    .Select(group => new
                    {
                        Total = group.Sum(log => log.Cost),
                        RequestCount = group.Count()
                    })
                    .SingleOrDefaultAsync();
                var costsByProvider = await billedRequests
                    .Where(log => log.Cost != 0)
                    .GroupBy(log => log.ProviderType ?? "unknown")
                    .Select(group => new
                    {
                        Provider = group.Key,
                        Cost = group.Sum(log => log.Cost)
                    })
                    .ToListAsync();

                snapshot.Business.Costs = new CostMetrics
                {
                    TotalCostPerMinute = costSummary?.Total ?? 0,
                    AverageCostPerRequest = costSummary is { RequestCount: > 0 }
                        ? costSummary.Total / costSummary.RequestCount
                        : 0,
                    CostByProvider = costsByProvider
                        .GroupBy(row => row.Provider, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Sum(row => row.Cost),
                            StringComparer.OrdinalIgnoreCase)
                };

                var modelUsage = await recentRequests
                    .GroupBy(log => new { log.ModelName, Provider = log.ProviderType ?? "unknown" })
                    .Select(group => new
                    {
                        group.Key.ModelName,
                        group.Key.Provider,
                        RequestCount = group.Count(),
                        TokenCount = group.Sum(log => (long)log.InputTokens + log.OutputTokens),
                        AverageResponseTime = group.Average(log => log.ResponseTimeMs),
                        ErrorCount = group.Count(log => log.StatusCode >= 400)
                    })
                    .OrderByDescending(row => row.RequestCount)
                    .ThenBy(row => row.ModelName)
                    .Take(5)
                    .ToListAsync();

                snapshot.Business.ModelUsage = modelUsage
                    .Select(row => new ModelUsageStats
                    {
                        ModelName = row.ModelName,
                        ProviderType = ParseProviderType(row.Provider),
                        RequestsPerMinute = row.RequestCount,
                        TokensPerMinute = row.TokenCount,
                        AverageResponseTime = row.AverageResponseTime,
                        ErrorRate = row.RequestCount > 0
                            ? row.ErrorCount * 100.0 / row.RequestCount
                            : 0
                    })
                    .ToList();

                // Top virtual keys by spend
                // Use optimized query that filters and limits at database level
                var topKeys = await virtualKeyRepo.GetTopEnabledAsync(5);
                var topKeyIds = topKeys.Select(key => key.Id).ToList();
                var requestCountsByKey = topKeyIds.Count == 0
                    ? new Dictionary<int, int>()
                    : await recentRequests
                        .Where(log => topKeyIds.Contains(log.VirtualKeyId))
                        .GroupBy(log => log.VirtualKeyId)
                        .Select(group => new { VirtualKeyId = group.Key, Count = group.Count() })
                        .ToDictionaryAsync(row => row.VirtualKeyId, row => row.Count);
                // Note: Spend tracking is now at the group level
                snapshot.Business.TopVirtualKeys = topKeys
                    .Select(k => new VirtualKeyStats
                    {
                        KeyId = k.Id.ToString(),
                        KeyName = k.KeyName ?? "Unnamed",
                        RequestsPerMinute = requestCountsByKey.GetValueOrDefault(k.Id),
                        TotalSpend = 0, // Spend is tracked at group level
                        BudgetUtilization = 0, // Budget is tracked at group level
                        IsOverBudget = false // Budget is tracked at group level
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                MetricsCollectionInstrumentation.RecordFailure("metrics_aggregation/business");
                _logger.LogError(ex, "Error collecting business metrics");
            }
        }

        /// <summary>
        /// Collect system-related metrics
        /// </summary>
        private void CollectSystemMetrics(MetricsSnapshot snapshot)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                
                snapshot.System = new SystemMetrics
                {
                    CpuUsagePercent = GetMetricValue("conduit_process_cpu_usage_percent"),
                    MemoryUsageMB = GetMetricValue("conduit_process_memory_bytes") / 1024 / 1024,
                    ThreadCount = (int)GetMetricValue("conduit_process_thread_count"),
                    HandleCount = (int)GetMetricValue("conduit_process_handle_count"),
                    GcMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                    Uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime()
                };
            }
            catch (Exception ex)
            {
                MetricsCollectionInstrumentation.RecordFailure("metrics_aggregation/system");
                _logger.LogError(ex, "Error collecting system metrics");
            }
        }

        private static ProviderType ParseProviderType(string provider)
        {
            return Enum.TryParse<ProviderType>(provider, ignoreCase: true, out var parsed)
                ? parsed
                : ProviderType.Unknown;
        }

    }
}
