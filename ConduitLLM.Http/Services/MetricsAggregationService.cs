using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Prometheus;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Http.DTOs.Metrics;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Repositories;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service that aggregates metrics from various sources and provides them to the dashboard.
    /// </summary>
    public class MetricsAggregationService : BackgroundService, IMetricsAggregationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MetricsAggregationService> _logger;
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(5);
        private readonly Dictionary<string, MetricsSeries> _historicalData = new();
        private readonly object _dataLock = new();
        private MetricsSnapshot? _lastSnapshot;

        // Alert thresholds
        private const double ErrorRateThreshold = 5.0; // 5% error rate
        private const double ResponseTimeThreshold = 5000; // 5 seconds
        private const double CpuUsageThreshold = 80.0; // 80% CPU
        private const double MemoryUsageThreshold = 85.0; // 85% memory
        private const int QueueDepthThreshold = 1000; // 1000 messages

        public MetricsAggregationService(
            IServiceProvider serviceProvider,
            ILogger<MetricsAggregationService> logger,
            IHubContext<MetricsHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Metrics aggregation service starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var snapshot = await CollectMetricsSnapshotAsync();
                    _lastSnapshot = snapshot;
                    
                    // Store historical data
                    StoreHistoricalData(snapshot);
                    
                    // Broadcast to all subscribers
                    await _hubContext.Clients.Group("metrics-subscribers")
                        .SendAsync("MetricsSnapshot", snapshot, stoppingToken);
                    
                    // Send targeted updates
                    await SendTargetedUpdates(snapshot, stoppingToken);
                    
                    // Check for alerts
                    await CheckAndSendAlerts(snapshot, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in metrics aggregation");
                }

                await Task.Delay(_updateInterval, stoppingToken);
            }

            _logger.LogInformation("Metrics aggregation service stopped");
        }

        public async Task<MetricsSnapshot> GetCurrentSnapshotAsync()
        {
            if (_lastSnapshot != null && (DateTime.UtcNow - _lastSnapshot.Timestamp).TotalSeconds < 10)
            {
                return _lastSnapshot;
            }

            return await CollectMetricsSnapshotAsync();
        }

        private async Task<MetricsSnapshot> CollectMetricsSnapshotAsync()
        {
            var snapshot = new MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow
            };

            var tasks = new[]
            {
                Task.Run(() => CollectHttpMetrics(snapshot)),
                Task.Run(() => CollectInfrastructureMetrics(snapshot)),
                Task.Run(() => CollectBusinessMetrics(snapshot)),
                Task.Run(() => CollectSystemMetrics(snapshot)),
                Task.Run(() => CollectProviderHealth(snapshot))
            };

            await Task.WhenAll(tasks);

            return snapshot;
        }

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
                _logger.LogError(ex, "Error collecting HTTP metrics");
            }
        }

        private async void CollectInfrastructureMetrics(MetricsSnapshot snapshot)
        {
            try
            {
                // Database metrics
                snapshot.Infrastructure.Database = new DatabaseMetrics
                {
                    ActiveConnections = (int)GetMetricValue("conduit_database_connections_active"),
                    AvailableConnections = (int)GetMetricValue("conduit_database_connections_available"),
                    AverageQueryDuration = GetMetricValue("conduit_database_query_duration_seconds_sum") * 1000,
                    ErrorsPerMinute = (int)(GetMetricValue("conduit_database_errors_total") / 60.0)
                };
                
                snapshot.Infrastructure.Database.PoolUtilization = 
                    snapshot.Infrastructure.Database.ActiveConnections / 
                    (double)(snapshot.Infrastructure.Database.ActiveConnections + snapshot.Infrastructure.Database.AvailableConnections) * 100;

                // Redis metrics
                using var scope = _serviceProvider.CreateScope();
                var redis = scope.ServiceProvider.GetService<IConnectionMultiplexer>();
                if (redis != null && redis.IsConnected)
                {
                    snapshot.Infrastructure.Redis = new RedisMetrics
                    {
                        MemoryUsageMB = GetMetricValue("conduit_redis_memory_used_bytes") / 1024 / 1024,
                        KeyCount = (long)GetMetricValue("conduit_redis_keys_count"),
                        ConnectedClients = (int)GetMetricValue("conduit_redis_connected_clients"),
                        IsConnected = true,
                        OperationsPerSecond = GetMetricValue("conduit_redis_operation_duration_seconds_count") / 60.0,
                        AverageLatency = GetMetricValue("conduit_redis_operation_duration_seconds_sum") * 1000
                    };

                    var hits = GetMetricValue("conduit_redis_cache_hits_total");
                    var misses = GetMetricValue("conduit_redis_cache_misses_total");
                    var total = hits + misses;
                    snapshot.Infrastructure.Redis.HitRate = total > 0 ? (hits / total) * 100 : 0;
                }

                // RabbitMQ metrics
                snapshot.Infrastructure.RabbitMQ = new RabbitMQMetrics
                {
                    IsConnected = GetMetricValue("conduit_rabbitmq_connection_state") > 0,
                    MessagesPublishedPerMinute = (int)(GetMetricValue("conduit_rabbitmq_published_messages_total") / 60.0),
                    MessagesConsumedPerMinute = (int)(GetMetricValue("conduit_rabbitmq_consumed_messages_total") / 60.0)
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
                _logger.LogError(ex, "Error collecting infrastructure metrics");
            }
        }

        private async void CollectBusinessMetrics(MetricsSnapshot snapshot)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                
                // Active virtual keys
                snapshot.Business.ActiveVirtualKeys = (int)GetMetricValue("conduit_virtualkeys_active_count");
                
                // Total requests
                snapshot.Business.TotalRequestsPerMinute = (int)(GetMetricValue("conduit_virtualkey_requests_total") / 60.0);
                
                // Cost metrics
                snapshot.Business.Costs = new CostMetrics
                {
                    TotalCostPerMinute = (decimal)GetMetricValue("conduit_cost_rate_dollars_per_minute"),
                    AverageCostPerRequest = (decimal)GetMetricValue("conduit_cost_per_request_dollars_sum")
                };

                // Cost by provider
                var providers = new[] { "openai", "anthropic", "google", "minimax", "replicate" };
                foreach (var provider in providers)
                {
                    var cost = GetMetricValue($"conduit_cost_rate_dollars_per_minute{{provider=\"{provider}\"}}");
                    if (cost > 0)
                    {
                        snapshot.Business.Costs.CostByProvider[provider] = (decimal)cost;
                    }
                }

                // Model usage (top 5)
                // In production, this would query from database or Prometheus
                snapshot.Business.ModelUsage = new List<ModelUsageStats>
                {
                    new ModelUsageStats
                    {
                        ModelName = "gpt-4-turbo",
                        Provider = "openai",
                        RequestsPerMinute = (int)(GetMetricValue("conduit_model_requests_total{model=\"gpt-4-turbo\"}") / 60.0),
                        TokensPerMinute = (long)GetMetricValue("conduit_model_tokens_total{model=\"gpt-4-turbo\"}"),
                        AverageResponseTime = GetMetricValue("conduit_model_response_time_seconds{model=\"gpt-4-turbo\"}") * 1000,
                        ErrorRate = 0
                    }
                };

                // Top virtual keys by spend
                var virtualKeyRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeyRepository>();
                var allKeys = await virtualKeyRepo.GetAllAsync();
                snapshot.Business.TopVirtualKeys = allKeys
                    .Where(k => k.IsEnabled)
                    .OrderByDescending(k => k.CurrentSpend)
                    .Take(5)
                    .Select(k => new VirtualKeyStats
                    {
                        KeyId = k.Id.ToString(),
                        KeyName = k.KeyName ?? "Unnamed",
                        RequestsPerMinute = (int)(GetMetricValue($"conduit_virtualkey_requests_total{{virtual_key_id=\"{k.Id}\"}}") / 60.0),
                        TotalSpend = k.CurrentSpend,
                        BudgetUtilization = k.MaxBudget.HasValue && k.MaxBudget > 0 
                            ? (double)(k.CurrentSpend / k.MaxBudget.Value) * 100 
                            : 0,
                        IsOverBudget = k.MaxBudget.HasValue && k.CurrentSpend >= k.MaxBudget.Value
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting business metrics");
            }
        }

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
                _logger.LogError(ex, "Error collecting system metrics");
            }
        }

        private async void CollectProviderHealth(MetricsSnapshot snapshot)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var providerService = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.IProviderCredentialService>();
                var providers = await providerService.GetAllCredentialsAsync();
                
                snapshot.ProviderHealth = providers.Select(p => new ProviderHealthStatus
                {
                    ProviderName = p.ProviderName,
                    Status = GetMetricValue($"conduit_provider_health{{provider=\"{p.ProviderName}\"}}") > 0 ? "healthy" : "unhealthy",
                    IsEnabled = p.IsEnabled,
                    ErrorRate = GetMetricValue($"conduit_provider_errors_total{{provider=\"{p.ProviderName}\"}}") / 
                               GetMetricValue($"conduit_model_requests_total{{provider=\"{p.ProviderName}\"}}") * 100,
                    AverageLatency = GetMetricValue($"conduit_provider_latency_seconds{{provider=\"{p.ProviderName}\"}}") * 1000,
                    AvailableModels = (int)GetMetricValue($"conduit_models_active_count{{provider=\"{p.ProviderName}\"}}")
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting provider health");
            }
        }

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

            if (alerts.Any())
            {
                await _hubContext.Clients.Group("metrics-subscribers")
                    .SendAsync("MetricAlerts", alerts, cancellationToken);
            }
        }

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

        public async Task<HistoricalMetricsResponse> GetHistoricalMetricsAsync(HistoricalMetricsRequest request)
        {
            await Task.CompletedTask; // Make async

            lock (_dataLock)
            {
                var response = new HistoricalMetricsResponse
                {
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Interval = request.Interval,
                    Series = new List<MetricsSeries>()
                };

                foreach (var metricName in request.MetricNames)
                {
                    if (_historicalData.TryGetValue(metricName, out var series))
                    {
                        var filteredSeries = new MetricsSeries
                        {
                            MetricName = series.MetricName,
                            Label = series.Label,
                            DataPoints = series.DataPoints
                                .Where(p => p.Timestamp >= request.StartTime && p.Timestamp <= request.EndTime)
                                .ToList()
                        };

                        if (filteredSeries.DataPoints.Any())
                        {
                            response.Series.Add(filteredSeries);
                        }
                    }
                }

                return response;
            }
        }

        public async Task<List<MetricAlert>> GetActiveAlertsAsync()
        {
            var snapshot = await GetCurrentSnapshotAsync();
            var alerts = new List<MetricAlert>();

            // Check various thresholds and generate alerts
            if (snapshot.Http.ErrorRate > ErrorRateThreshold)
            {
                alerts.Add(new MetricAlert
                {
                    Id = "http-error-rate",
                    Severity = "critical",
                    MetricName = "HTTP Error Rate",
                    Message = $"HTTP error rate exceeds threshold",
                    CurrentValue = snapshot.Http.ErrorRate,
                    Threshold = ErrorRateThreshold,
                    TriggeredAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            return alerts;
        }

        public async Task<List<ProviderHealthStatus>> CheckProviderHealthAsync(string? providerName)
        {
            var snapshot = await GetCurrentSnapshotAsync();
            
            if (string.IsNullOrEmpty(providerName))
            {
                return snapshot.ProviderHealth;
            }

            return snapshot.ProviderHealth
                .Where(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<List<VirtualKeyStats>> GetTopVirtualKeysAsync(string metric, int count)
        {
            var snapshot = await GetCurrentSnapshotAsync();
            
            return metric.ToLower() switch
            {
                "requests" => snapshot.Business.TopVirtualKeys
                    .OrderByDescending(k => k.RequestsPerMinute)
                    .Take(count)
                    .ToList(),
                "spend" => snapshot.Business.TopVirtualKeys
                    .OrderByDescending(k => k.TotalSpend)
                    .Take(count)
                    .ToList(),
                "budget" => snapshot.Business.TopVirtualKeys
                    .OrderByDescending(k => k.BudgetUtilization)
                    .Take(count)
                    .ToList(),
                _ => snapshot.Business.TopVirtualKeys.Take(count).ToList()
            };
        }

        private double GetMetricValue(string metricName)
        {
            // This is a simplified implementation
            // In production, you would query Prometheus or use the Prometheus .NET client
            // For now, return mock values
            return Random.Shared.NextDouble() * 100;
        }

        private double ParseMetricValue(string? line)
        {
            if (string.IsNullOrEmpty(line))
                return 0;

            var parts = line.Split(' ');
            if (parts.Length >= 2 && double.TryParse(parts[1], out var value))
                return value;

            return 0;
        }
    }
}