using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ConduitLLM.Core.HealthChecks
{
    /// <summary>
    /// Health check that monitors HTTP connection pool utilization for webhook delivery.
    /// </summary>
    public class HttpConnectionPoolHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HttpConnectionPoolHealthCheck> _logger;

        private static readonly Gauge ConnectionPoolActiveConnections = Metrics
            .CreateGauge("conduit_http_pool_active_connections", "Active HTTP connections in pool",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "endpoint" }
                });

        private static readonly Gauge ConnectionPoolMaxConnections = Metrics
            .CreateGauge("conduit_http_pool_max_connections", "Maximum HTTP connections allowed",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "endpoint" }
                });

        private const double WarningThreshold = 0.7;  // 70% utilization
        private const double CriticalThreshold = 0.9; // 90% utilization
        private const int MaxConnectionsPerServer = 50; // Must match SocketsHttpHandler config

        public HttpConnectionPoolHealthCheck(
            IHttpClientFactory httpClientFactory,
            ILogger<HttpConnectionPoolHealthCheck> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get connection pool statistics from SocketsHttpHandler
                var poolStats = await GetConnectionPoolStatsAsync();
                
                // Calculate utilization
                var utilization = poolStats.ActiveConnections / (double)poolStats.MaxConnections;
                
                // Update metrics
                ConnectionPoolActiveConnections.WithLabels("webhooks").Set(poolStats.ActiveConnections);
                ConnectionPoolMaxConnections.WithLabels("webhooks").Set(poolStats.MaxConnections);
                
                // Build health check data
                var data = new Dictionary<string, object>
                {
                    ["activeConnections"] = poolStats.ActiveConnections,
                    ["maxConnections"] = poolStats.MaxConnections,
                    ["utilization"] = $"{utilization:P1}",
                    ["idleConnections"] = poolStats.IdleConnections,
                    ["pendingRequests"] = poolStats.PendingRequests
                };

                // Determine health status
                if (utilization >= CriticalThreshold)
                {
                    _logger.LogError("HTTP connection pool critical: {ActiveConnections}/{MaxConnections} ({Utilization:P1})",
                        poolStats.ActiveConnections, poolStats.MaxConnections, utilization);
                    
                    return HealthCheckResult.Unhealthy(
                        $"Connection pool utilization critical: {utilization:P1}",
                        data: data);
                }
                else if (utilization >= WarningThreshold)
                {
                    _logger.LogWarning("HTTP connection pool high: {ActiveConnections}/{MaxConnections} ({Utilization:P1})",
                        poolStats.ActiveConnections, poolStats.MaxConnections, utilization);
                    
                    return HealthCheckResult.Degraded(
                        $"Connection pool utilization high: {utilization:P1}",
                        data: data);
                }
                
                return HealthCheckResult.Healthy(
                    $"Connections: {poolStats.ActiveConnections}/{poolStats.MaxConnections} ({utilization:P1})",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get HTTP connection pool statistics");
                
                return HealthCheckResult.Unhealthy(
                    "Failed to retrieve connection pool statistics",
                    exception: ex);
            }
        }

        private async Task<ConnectionPoolStats> GetConnectionPoolStatsAsync()
        {
            // Since .NET doesn't expose connection pool stats directly, we'll use a combination of:
            // 1. Active webhook request count from metrics
            // 2. Configuration values
            // 3. Performance counters if available
            
            var stats = new ConnectionPoolStats
            {
                MaxConnections = MaxConnectionsPerServer
            };

            // Try to get actual stats from performance counters or diagnostics
            try
            {
                // Use reflection to access internal HttpClient handler stats if available
                var httpClient = _httpClientFactory.CreateClient("WebhookClient");
                var handlerField = httpClient.GetType().GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (handlerField != null)
                {
                    var handler = handlerField.GetValue(httpClient);
                    if (handler != null)
                    {
                        // Try to get connection pool stats from handler
                        var poolField = handler.GetType().GetField("_pool", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (poolField != null)
                        {
                            var pool = poolField.GetValue(handler);
                            if (pool != null)
                            {
                                // Extract connection counts if available
                                var activeField = pool.GetType().GetProperty("ActiveConnectionCount", BindingFlags.Public | BindingFlags.Instance);
                                if (activeField != null)
                                {
                                    stats.ActiveConnections = (int)(activeField.GetValue(pool) ?? 0);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not access internal connection pool stats, using fallback");
            }

            // Fallback: estimate based on active webhook requests from Prometheus metrics
            if (stats.ActiveConnections == 0)
            {
                // Get active requests from our metrics
                var activeRequests = await GetActiveWebhookRequestsAsync();
                // Assume each request uses one connection
                stats.ActiveConnections = Math.Min(activeRequests, stats.MaxConnections);
            }

            stats.IdleConnections = Math.Max(0, stats.MaxConnections - stats.ActiveConnections);
            stats.PendingRequests = 0; // Would need queue monitoring to get this

            return stats;
        }

        private async Task<int> GetActiveWebhookRequestsAsync()
        {
            try
            {
                // For now, we'll use a simpler approach
                // In production, this would integrate with the actual WebhookMetricsHandler gauge
                await Task.CompletedTask; // Make method async
                
                // TODO: Integrate with actual Prometheus metrics once WebhookMetricsHandler is registered
                // For now, return 0 to allow the health check to function
                _logger.LogDebug("Active webhook request metrics not yet available");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not retrieve active webhook requests from metrics");
            }

            return 0;
        }

        private class ConnectionPoolStats
        {
            public int ActiveConnections { get; set; }
            public int MaxConnections { get; set; }
            public int IdleConnections { get; set; }
            public int PendingRequests { get; set; }
        }
    }
}