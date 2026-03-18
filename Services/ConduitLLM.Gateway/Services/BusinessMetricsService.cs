using Microsoft.EntityFrameworkCore;
using Prometheus;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service for tracking business metrics including virtual key usage,
    /// model usage patterns, costs, and revenue tracking.
    /// </summary>
    public class BusinessMetricsService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<BusinessMetricsService> _logger;
        private readonly TimeSpan _collectionInterval = TimeSpan.FromMinutes(1);

        // Virtual Key metrics
        private static readonly Counter VirtualKeyRequests = Prometheus.Metrics
            .CreateCounter("conduit_virtualkey_requests_total", "Total requests per virtual key",
                new CounterConfiguration
                {
                    LabelNames = new[] { "virtual_key_id", "model", "status" }
                });

        private static readonly Gauge VirtualKeySpendTotal = Prometheus.Metrics
            .CreateGauge("conduit_virtualkey_spend_total", "Total spend per virtual key",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "virtual_key_id" }
                });

        private static readonly Gauge VirtualKeyBudgetUtilization = Prometheus.Metrics
            .CreateGauge("conduit_virtualkey_budget_utilization_percent", "Budget utilization percentage per virtual key",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "virtual_key_id" }
                });

        private static readonly Counter VirtualKeyBudgetExceeded = Prometheus.Metrics
            .CreateCounter("conduit_virtualkey_budget_exceeded_total", "Number of times budget was exceeded",
                new CounterConfiguration
                {
                    LabelNames = new[] { "virtual_key_id" }
                });

        // Model usage metrics
        private static readonly Counter ModelRequests = Prometheus.Metrics
            .CreateCounter("conduit_model_requests_total", "Total requests per model",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider", "status" }
                });

        private static readonly Histogram ModelResponseTime = Prometheus.Metrics
            .CreateHistogram("conduit_model_response_time_seconds", "Model response time",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "model", "provider" },
                    Buckets = Histogram.ExponentialBuckets(0.1, 2, 14) // 0.1s to ~820s
                });

        private static readonly Counter ModelTokensProcessed = Prometheus.Metrics
            .CreateCounter("conduit_model_tokens_total", "Total tokens processed",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider", "token_type" } // token_type: prompt, completion
                });

        // Cost tracking metrics
        private static readonly Counter CostTotal = Prometheus.Metrics
            .CreateCounter("conduit_cost_total_dollars", "Total cost in dollars",
                new CounterConfiguration
                {
                    LabelNames = new[] { "provider", "model", "operation_type" }
                });

        private static readonly Gauge CostRate = Prometheus.Metrics
            .CreateGauge("conduit_cost_rate_dollars_per_minute", "Cost rate in dollars per minute",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "provider" }
                });

        private static readonly Histogram CostPerRequest = Prometheus.Metrics
            .CreateHistogram("conduit_cost_per_request_dollars", "Cost per request in dollars",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "model", "provider" },
                    Buckets = new[] { 0.001, 0.01, 0.1, 0.5, 1, 5, 10, 50, 100 }
                });

        // Provider metrics
        private static readonly Counter ProviderErrors = Prometheus.Metrics
            .CreateCounter("conduit_provider_errors_total", "Total provider errors",
                new CounterConfiguration
                {
                    LabelNames = new[] { "provider", "error_type" }
                });

        private static readonly Summary ProviderLatency = Prometheus.Metrics
            .CreateSummary("conduit_provider_latency_seconds", "Provider API latency",
                new SummaryConfiguration
                {
                    LabelNames = new[] { "provider", "operation" },
                    Objectives = new[]
                    {
                        new QuantileEpsilonPair(0.5, 0.05),
                        new QuantileEpsilonPair(0.9, 0.01),
                        new QuantileEpsilonPair(0.95, 0.005),
                        new QuantileEpsilonPair(0.99, 0.001)
                    },
                    MaxAge = TimeSpan.FromMinutes(5),
                    AgeBuckets = 5
                });

        // Active entities metrics
        private static readonly Gauge ActiveVirtualKeys = Prometheus.Metrics
            .CreateGauge("conduit_virtualkeys_active_count", "Number of active virtual keys");

        private static readonly Gauge ActiveModels = Prometheus.Metrics
            .CreateGauge("conduit_models_active_count", "Number of active model mappings",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "provider" }
                });

        // SLA metrics
        private static readonly Counter SLAViolations = Prometheus.Metrics
            .CreateCounter("conduit_sla_violations_total", "Total SLA violations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "sla_type", "model" } // sla_type: latency, availability, error_rate
                });

        public BusinessMetricsService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<BusinessMetricsService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Business metrics service starting with {IntervalSeconds}s collection interval",
                _collectionInterval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectMetricsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting business metrics");
                }

                await Task.Delay(_collectionInterval, stoppingToken);
            }

            _logger.LogInformation("Business metrics service stopped");
        }

        private async Task CollectMetricsAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using var scope = _serviceScopeFactory.CreateScope();

            var tasks = new[]
            {
                CollectVirtualKeyMetrics(scope),
                CollectModelUsageMetrics(scope),
                CollectCostMetrics(scope),
                CollectActiveEntityMetrics(scope)
            };

            await Task.WhenAll(tasks);

            stopwatch.Stop();
            _logger.LogDebug("Business metrics collection cycle completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }

        private async Task CollectVirtualKeyMetrics(IServiceScope scope)
        {
            try
            {
                // Note: Budget tracking is now at the group level
                // Individual key metrics are no longer tracked for budget/spend
                // No need to load all virtual keys - just count active ones if needed
                var virtualKeyRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeyRepository>();
                var activeKeyCount = await virtualKeyRepo.CountActiveAsync();
                // activeKeyCount is available for metrics if needed in the future
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting virtual key metrics");
            }
        }

        private async Task CollectModelUsageMetrics(IServiceScope scope)
        {
            // NOTE: Model/provider counters (conduit_model_requests_total, conduit_model_tokens_total)
            // are updated in REAL-TIME via static methods called from UsageTrackingMiddleware.
            // This background method only collects supplementary gauge metrics.
            //
            // DO NOT increment counters here - it would cause double-counting since the middleware
            // already records each request as it happens.

            try
            {
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>>();
                await using var context = await dbContextFactory.CreateDbContextAsync();

                // Get model usage statistics for the last 5 minutes to calculate current rates
                var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

                var requestLogs = await context.RequestLogs
                    .Where(r => r.Timestamp >= fiveMinutesAgo)
                    .ToListAsync();

                // Use the new ProviderType field directly instead of parsing model names
                var modelStats = requestLogs
                    .GroupBy(r => new { Model = r.ModelName, Provider = r.ProviderType ?? "unknown" })
                    .Select(g => new
                    {
                        g.Key.Model,
                        g.Key.Provider,
                        RequestCount = g.Count(),
                        TotalPromptTokens = g.Sum(r => r.InputTokens),
                        TotalCompletionTokens = g.Sum(r => r.OutputTokens),
                        AvgResponseTime = g.Average(r => r.ResponseTimeMs)
                    })
                    .ToList();

                _logger.LogDebug("Collected model usage metrics: {Count} model/provider combinations in last 5 minutes",
                    modelStats.Count);

                // Observe average response times (histograms are safe to update periodically)
                foreach (var stat in modelStats)
                {
                    if (stat.AvgResponseTime > 0)
                    {
                        ModelResponseTime.WithLabels(stat.Model ?? "unknown", stat.Provider)
                            .Observe(stat.AvgResponseTime / 1000.0); // Convert ms to seconds
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting model usage metrics");
            }
        }

        private async Task CollectCostMetrics(IServiceScope scope)
        {
            // NOTE: Cost counters (conduit_cost_total_dollars) are updated in REAL-TIME via
            // static methods called from UsageTrackingMiddleware.
            // This background method only updates the CostRate gauge for rate calculations.

            try
            {
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>>();
                await using var context = await dbContextFactory.CreateDbContextAsync();

                // Calculate cost rate per provider using the ProviderType field
                var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

                var costLogs = await context.RequestLogs
                    .Where(r => r.Timestamp >= fiveMinutesAgo && r.Cost > 0)
                    .ToListAsync();

                // Use the new ProviderType field directly
                var costByProvider = costLogs
                    .GroupBy(r => r.ProviderType ?? "unknown")
                    .Select(g => new
                    {
                        Provider = g.Key,
                        TotalCost = g.Sum(r => r.Cost)
                    })
                    .ToList();

                foreach (var providerCost in costByProvider)
                {
                    var provider = providerCost.Provider;
                    var costPerMinute = (double)(providerCost.TotalCost / 5); // 5-minute window

                    // Update the rate gauge (this is safe to update periodically)
                    CostRate.WithLabels(provider).Set(costPerMinute);
                }

                _logger.LogDebug("Collected cost metrics: {Count} providers with costs in last 5 minutes",
                    costByProvider.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting cost metrics");
            }
        }

        private async Task CollectActiveEntityMetrics(IServiceScope scope)
        {
            try
            {
                var virtualKeyRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeyRepository>();
                var modelMappingService = scope.ServiceProvider.GetRequiredService<IModelProviderMappingService>();

                // Count active virtual keys using database-level count
                var activeKeyCount = await virtualKeyRepo.CountActiveAsync();
                ActiveVirtualKeys.Set(activeKeyCount);

                // Count active model mappings by provider
                var mappings = await modelMappingService.GetAllMappingsAsync();
                // Group by provider type
                // TODO: Fix IsEnabled check once we verify the return type
                var mappingsByProvider = mappings
                    // .Where(m => m.IsEnabled)
                    .GroupBy(m => m.ProviderId.ToString())
                    .Select(g => new { Provider = g.Key, Count = g.Count() });

                foreach (var group in mappingsByProvider)
                {
                    ActiveModels.WithLabels(group.Provider).Set((double)group.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting active entity metrics");
            }
        }

        // Static methods to be called by application code
        public static void RecordVirtualKeyRequest(string virtualKeyId, string model, string status)
        {
            VirtualKeyRequests.WithLabels(virtualKeyId, model, status).Inc();
        }

        public static void RecordModelRequest(string model, string provider, string status)
        {
            ModelRequests.WithLabels(model, provider, status).Inc();
        }

        public static void RecordProviderError(string provider, string errorType)
        {
            ProviderErrors.WithLabels(provider, errorType).Inc();
        }

        public static void RecordProviderLatency(string provider, string operation, double latencySeconds)
        {
            ProviderLatency.WithLabels(provider, operation).Observe(latencySeconds);
        }

        public static void RecordCost(string provider, string model, string operationType, double costDollars)
        {
            CostTotal.WithLabels(provider, model, operationType).Inc(costDollars);
            CostPerRequest.WithLabels(model, provider).Observe(costDollars);
        }

        public static void RecordTokens(string model, string provider, int promptTokens, int completionTokens)
        {
            if (promptTokens > 0)
            {
                ModelTokensProcessed.WithLabels(model, provider, "prompt").Inc(promptTokens);
            }
            if (completionTokens > 0)
            {
                ModelTokensProcessed.WithLabels(model, provider, "completion").Inc(completionTokens);
            }
        }

        public static void RecordResponseTime(string model, string provider, double responseTimeSeconds)
        {
            if (responseTimeSeconds > 0)
            {
                ModelResponseTime.WithLabels(model, provider).Observe(responseTimeSeconds);
            }
        }

        public static void RecordSLAViolation(string slaType, string model)
        {
            SLAViolations.WithLabels(slaType, model).Inc();
        }
    }
}