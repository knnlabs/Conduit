using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.Metrics;
using ConduitLLM.Http.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Metrics analysis and alert methods for MetricsAggregationService
    /// </summary>
    public partial class MetricsAggregationService
    {
        /// <summary>
        /// Send targeted updates to specific subscriber groups
        /// </summary>
        private async Task SendTargetedUpdates(MetricsSnapshot snapshot, CancellationToken cancellationToken)
        {
            // Send specific metric updates to subscribed groups
            await _hubContext.Clients.Group("metrics-http")
                .SendAsync("HttpMetricsUpdate", snapshot.Http, cancellationToken);
            
            await _hubContext.Clients.Group("metrics-infrastructure")
                .SendAsync("InfrastructureMetricsUpdate", snapshot.Infrastructure, cancellationToken);
            
            await _hubContext.Clients.Group("metrics-business")
                .SendAsync("BusinessMetricsUpdate", snapshot.Business, cancellationToken);
            
            await _hubContext.Clients.Group("metrics-providers")
                .SendAsync("ProviderHealthUpdate", snapshot.ProviderHealth, cancellationToken);
        }

        /// <summary>
        /// Check metrics against thresholds and send alerts
        /// </summary>
        private async Task CheckAndSendAlerts(MetricsSnapshot snapshot, CancellationToken cancellationToken)
        {
            var alerts = new List<MetricAlert>();

            // Check error rate
            if (snapshot.Http.ErrorRate > ErrorRateThreshold)
            {
                alerts.Add(new MetricAlert
                {
                    Id = "http-error-rate",
                    Severity = "critical",
                    MetricName = "HTTP Error Rate",
                    Message = $"HTTP error rate ({snapshot.Http.ErrorRate:F1}%) exceeds threshold ({ErrorRateThreshold}%)",
                    CurrentValue = snapshot.Http.ErrorRate,
                    Threshold = ErrorRateThreshold,
                    TriggeredAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            // Check response times
            if (snapshot.Http.ResponseTimes.P95 > ResponseTimeThreshold)
            {
                alerts.Add(new MetricAlert
                {
                    Id = "response-time-p95",
                    Severity = "warning",
                    MetricName = "Response Time P95",
                    Message = $"95th percentile response time ({snapshot.Http.ResponseTimes.P95:F0}ms) exceeds threshold ({ResponseTimeThreshold}ms)",
                    CurrentValue = snapshot.Http.ResponseTimes.P95,
                    Threshold = ResponseTimeThreshold,
                    TriggeredAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            // Check system resources
            if (snapshot.System.CpuUsagePercent > CpuUsageThreshold)
            {
                alerts.Add(new MetricAlert
                {
                    Id = "cpu-usage",
                    Severity = "warning",
                    MetricName = "CPU Usage",
                    Message = $"CPU usage ({snapshot.System.CpuUsagePercent:F1}%) exceeds threshold ({CpuUsageThreshold}%)",
                    CurrentValue = snapshot.System.CpuUsagePercent,
                    Threshold = CpuUsageThreshold,
                    TriggeredAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            if (alerts.Count() > 0)
            {
                await _hubContext.Clients.Group("metrics-subscribers")
                    .SendAsync("MetricAlerts", alerts, cancellationToken);
            }
        }

        /// <summary>
        /// Store historical metrics data with cleanup
        /// </summary>
        private void StoreHistoricalData(MetricsSnapshot snapshot)
        {
            lock (_dataLock)
            {
                // Store key metrics for historical analysis (keep last 24 hours)
                var cutoff = DateTime.UtcNow.AddHours(-24);
                
                StoreMetricPoint("http_requests_per_second", snapshot.Http.RequestsPerSecond, snapshot.Timestamp);
                StoreMetricPoint("http_error_rate", snapshot.Http.ErrorRate, snapshot.Timestamp);
                StoreMetricPoint("http_response_time_p95", snapshot.Http.ResponseTimes.P95, snapshot.Timestamp);
                StoreMetricPoint("cpu_usage_percent", snapshot.System.CpuUsagePercent, snapshot.Timestamp);
                StoreMetricPoint("memory_usage_mb", snapshot.System.MemoryUsageMB, snapshot.Timestamp);
                StoreMetricPoint("active_connections", snapshot.Http.ActiveRequests, snapshot.Timestamp);
                StoreMetricPoint("cost_per_minute", (double)snapshot.Business.Costs.TotalCostPerMinute, snapshot.Timestamp);
                
                // Clean up old data
                foreach (var series in _historicalData.Values)
                {
                    series.DataPoints.RemoveAll(p => p.Timestamp < cutoff);
                }
            }
        }

        /// <summary>
        /// Store a single metric data point
        /// </summary>
        private void StoreMetricPoint(string metricName, double value, DateTime timestamp)
        {
            if (!_historicalData.TryGetValue(metricName, out var series))
            {
                series = new MetricsSeries
                {
                    MetricName = metricName,
                    Label = metricName,
                    DataPoints = new List<MetricsDataPoint>()
                };
                _historicalData[metricName] = series;
            }

            series.DataPoints.Add(new MetricsDataPoint
            {
                Timestamp = timestamp,
                Value = value
            });
        }
    }
}