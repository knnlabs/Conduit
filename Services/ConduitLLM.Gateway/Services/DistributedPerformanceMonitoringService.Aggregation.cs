using System.Text.Json;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Core.Extensions;
using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services
{
    public partial class DistributedPerformanceMonitoringService
    {
        private async Task AggregateMetricsAsync()
        {
            try
            {
                var metrics = await GetAggregatedMetricsAsync();

                // Store aggregated metrics snapshot
                var key = $"{MetricsPrefix}_snapshot_{DateTime.UtcNow:yyyyMMddHHmmss}";
                await _database.StringSetAsync(key, JsonSerializer.Serialize(metrics), TimeSpan.FromHours(24));

                _logger.LogDebug("Aggregated performance metrics: {RequestsPerSecond} req/s, {ErrorRate}% errors, {AvgResponse}ms avg response",
                    metrics.RequestsPerSecond, metrics.ErrorRatePercent, metrics.AverageResponseTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating performance metrics");
            }
        }

        public async Task<PerformanceMetrics> GetAggregatedMetricsAsync()
        {
            await _aggregationSemaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var windowStart = now.AddSeconds(-_options.MetricsWindowSeconds);

                // Get recent requests from stream
                var requestEntries = await _database.StreamRangeAsync(RequestsStreamKey,
                    $"{windowStart.Ticks}", "+", _options.MaxMetricsRetention);

                var recentRequests = requestEntries
                    .Select(entry => JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Values[0].Value.ToString()))
                    .Where(r => r != null && long.Parse(r.GetValueOrDefault("Timestamp", "0")?.ToString() ?? "0") > windowStart.Ticks)
                    .ToList();

                var requestCount = recentRequests.Count;
                var successCount = recentRequests.Count(r => r != null && bool.Parse(r.GetValueOrDefault("IsSuccess", "false")?.ToString() ?? "false"));
                var errorRate = requestCount > 0 ? ((double)(requestCount - successCount) / requestCount) * 100 : 0;

                var responseTimes = recentRequests
                    .Where(r => r != null)
                    .Select(r => double.Parse(r!.GetValueOrDefault("ResponseTimeMs", "0")?.ToString() ?? "0"))
                    .OrderBy(t => t)
                    .ToList();

                var metrics = new PerformanceMetrics
                {
                    RequestsPerSecond = requestCount / (double)_options.MetricsWindowSeconds,
                    ErrorRatePercent = errorRate,
                    ActiveRequests = 0 // Would need separate tracking
                };

                if (responseTimes.Count > 0)
                {
                    metrics.AverageResponseTimeMs = responseTimes.Average();
                    metrics.P95ResponseTimeMs = GetPercentile(responseTimes, 0.95);
                    metrics.P99ResponseTimeMs = GetPercentile(responseTimes, 0.99);
                }

                return metrics;
            }
            finally
            {
                _aggregationSemaphore.Release();
            }
        }

        public async Task<Dictionary<string, ConduitLLM.Configuration.DTOs.HealthMonitoring.EndpointMetrics>> GetAggregatedEndpointMetricsAsync()
        {
            var pattern = $"{EndpointMetricsPrefix}:*";
            var server = _database.Multiplexer.GetPrimaryServer();
            var keys = server.Keys(pattern: pattern);

            var endpointMetrics = new Dictionary<string, ConduitLLM.Configuration.DTOs.HealthMonitoring.EndpointMetrics>();

            foreach (var key in keys)
            {
                var hash = await _database.HashGetAllAsync(key);
                if (hash.Length == 0) continue;

                var hashDict = hash.ToDictionary(x => x.Name, x => x.Value);

                var endpoint = hashDict.GetValueOrDefault("endpoint", "").ToString();
                var totalRequests = (int)hashDict.GetValueOrDefault("total_requests", 0);
                var successfulRequests = (int)hashDict.GetValueOrDefault("successful_requests", 0);
                var totalResponseTime = (double)hashDict.GetValueOrDefault("total_response_time", 0);
                var maxResponseTime = (double)hashDict.GetValueOrDefault("max_response_time", 0);
                var minResponseTime = (double)hashDict.GetValueOrDefault("min_response_time", 0);
                var lastUpdatedTicks = (long)hashDict.GetValueOrDefault("last_updated", 0);

                endpointMetrics[endpoint ?? ""] = new ConduitLLM.Configuration.DTOs.HealthMonitoring.EndpointMetrics
                {
                    Endpoint = endpoint ?? "",
                    TotalRequests = totalRequests,
                    SuccessfulRequests = successfulRequests,
                    TotalResponseTime = totalResponseTime,
                    MaxResponseTime = maxResponseTime,
                    MinResponseTime = minResponseTime,
                    LastUpdated = new DateTime(lastUpdatedTicks)
                };
            }

            return endpointMetrics;
        }

        private static double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0) return 0;

            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }
    }
}
