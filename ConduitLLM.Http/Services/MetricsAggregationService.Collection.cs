using System.Diagnostics;
using StackExchange.Redis;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.Metrics;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Http.Services
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
                _logger.LogError(ex, "Error collecting infrastructure metrics");
            }
        }

        /// <summary>
        /// Collect business-related metrics
        /// </summary>
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

                // Cost by provider - dynamically get from database
                // TODO: This should be data-driven from database configuration
                // Provider cost metrics should be collected based on enabled providers
                // not a hardcoded list. For now, leaving empty as metrics collection
                // should be refactored to use actual provider repository data.
                snapshot.Business.Costs.CostByProvider = new Dictionary<string, decimal>();

                // Model usage (top 5)
                // In production, this would query from database or Prometheus
                snapshot.Business.ModelUsage = new List<ModelUsageStats>
                {
                    new ModelUsageStats
                    {
                        ModelName = "gpt-4-turbo",
                        ProviderType = ProviderType.OpenAI,
                        RequestsPerMinute = (int)(GetMetricValue("conduit_model_requests_total{model=\"gpt-4-turbo\"}") / 60.0),
                        TokensPerMinute = (long)GetMetricValue("conduit_model_tokens_total{model=\"gpt-4-turbo\"}"),
                        AverageResponseTime = GetMetricValue("conduit_model_response_time_seconds{model=\"gpt-4-turbo\"}") * 1000,
                        ErrorRate = 0
                    }
                };

                // Top virtual keys by spend
                var virtualKeyRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeyRepository>();
                var allKeys = await virtualKeyRepo.GetAllAsync();
                // Note: Spend tracking is now at the group level
                snapshot.Business.TopVirtualKeys = allKeys
                    .Where(k => k.IsEnabled)
                    .Take(5)
                    .Select(k => new VirtualKeyStats
                    {
                        KeyId = k.Id.ToString(),
                        KeyName = k.KeyName ?? "Unnamed",
                        RequestsPerMinute = (int)(GetMetricValue($"conduit_virtualkey_requests_total{{virtual_key_id=\"{k.Id}\"}}") / 60.0),
                        TotalSpend = 0, // Spend is tracked at group level
                        BudgetUtilization = 0, // Budget is tracked at group level
                        IsOverBudget = false // Budget is tracked at group level
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
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
                _logger.LogError(ex, "Error collecting system metrics");
            }
        }

        /// <summary>
        /// Collect provider health metrics
        /// </summary>
        private async void CollectProviderHealth(MetricsSnapshot snapshot)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var providerService = scope.ServiceProvider.GetRequiredService<IProviderService>();
                var providers = await providerService.GetAllProvidersAsync();
                
                snapshot.ProviderHealth = providers.Select(p => new ProviderHealthStatus
                {
                    ProviderType = p.ProviderType,
                    Status = GetMetricValue($"conduit_provider_health{{provider=\"{p.Id}\"}}") > 0 ? "healthy" : "unhealthy",
                    IsEnabled = p.IsEnabled,
                    ErrorRate = GetMetricValue($"conduit_provider_errors_total{{provider=\"{p.Id}\"}}") / 
                               GetMetricValue($"conduit_model_requests_total{{provider=\"{p.Id}\"}}") * 100,
                    AverageLatency = GetMetricValue($"conduit_provider_latency_seconds{{provider=\"{p.Id}\"}}") * 1000,
                    AvailableModels = (int)GetMetricValue($"conduit_models_active_count{{provider=\"{p.Id}\"}}")
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting provider health");
            }
        }
    }
}