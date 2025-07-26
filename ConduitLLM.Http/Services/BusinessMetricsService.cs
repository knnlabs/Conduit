using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for tracking business metrics including virtual key usage,
    /// model usage patterns, costs, and revenue tracking.
    /// </summary>
    public class BusinessMetricsService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
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
        private static readonly Gauge ProviderHealth = Prometheus.Metrics
            .CreateGauge("conduit_provider_health", "Provider health status (1=healthy, 0=unhealthy)",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "provider" }
                });

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

        private readonly Dictionary<string, DateTime> _lastCostUpdate = new();
        private readonly Dictionary<string, decimal> _lastCostValue = new();

        public BusinessMetricsService(
            IServiceProvider serviceProvider,
            ILogger<BusinessMetricsService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Business metrics service starting...");

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
            using var scope = _serviceProvider.CreateScope();

            var tasks = new[]
            {
                CollectVirtualKeyMetrics(scope),
                CollectModelUsageMetrics(scope),
                CollectCostMetrics(scope),
                CollectProviderMetrics(scope),
                CollectActiveEntityMetrics(scope)
            };

            await Task.WhenAll(tasks);
        }

        private async Task CollectVirtualKeyMetrics(IServiceScope scope)
        {
            try
            {
                var virtualKeyRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeyRepository>();
                var spendHistoryRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeySpendHistoryRepository>();

                // Get all virtual keys and filter for active ones
                var allKeys = await virtualKeyRepo.GetAllAsync();
                var activeKeys = allKeys.Where(k => k.IsEnabled && (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow)).ToList();

                foreach (var key in activeKeys)
                {
                    // Calculate budget utilization
                    if (key.MaxBudget.HasValue && key.MaxBudget > 0)
                    {
                        var utilization = (key.CurrentSpend / key.MaxBudget.Value) * 100;
                        VirtualKeyBudgetUtilization.WithLabels(key.Id.ToString()).Set((double)utilization);

                        if (key.CurrentSpend >= key.MaxBudget.Value)
                        {
                            VirtualKeyBudgetExceeded.WithLabels(key.Id.ToString()).Inc();
                        }
                    }

                    VirtualKeySpendTotal.WithLabels(key.Id.ToString()).Set((double)key.CurrentSpend);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting virtual key metrics");
            }
        }

        private async Task CollectModelUsageMetrics(IServiceScope scope)
        {
            try
            {
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>>();
                await using var context = await dbContextFactory.CreateDbContextAsync();

                // Get model usage statistics for the last hour
                var oneHourAgo = DateTime.UtcNow.AddHours(-1);

                // First get the data, then process in memory to avoid expression tree issues
                var requestLogs = await context.RequestLogs
                    .Where(r => r.Timestamp >= oneHourAgo)
                    .ToListAsync();

                var modelStats = requestLogs
                    .GroupBy(r => new { Model = r.ModelName, Provider = r.ModelName.Contains("/") ? r.ModelName.Split('/')[0] : "unknown" })
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

                foreach (var stat in modelStats)
                {
                    if (stat.TotalPromptTokens > 0)
                    {
                        ModelTokensProcessed.WithLabels(stat.Model ?? "unknown", stat.Provider ?? "unknown", "prompt")
                            .Inc(stat.TotalPromptTokens);
                    }

                    if (stat.TotalCompletionTokens > 0)
                    {
                        ModelTokensProcessed.WithLabels(stat.Model ?? "unknown", stat.Provider ?? "unknown", "completion")
                            .Inc(stat.TotalCompletionTokens);
                    }

                    if (stat.AvgResponseTime > 0)
                    {
                        ModelResponseTime.WithLabels(stat.Model ?? "unknown", stat.Provider ?? "unknown")
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
            try
            {
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>>();
                await using var context = await dbContextFactory.CreateDbContextAsync();

                // Calculate cost rate per provider
                var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

                // First get the data, then process in memory to avoid expression tree issues
                var costLogs = await context.RequestLogs
                    .Where(r => r.Timestamp >= fiveMinutesAgo && r.Cost > 0)
                    .ToListAsync();

                var costByProvider = costLogs
                    .GroupBy(r => r.ModelName.Contains("/") ? r.ModelName.Split('/')[0] : "unknown")
                    .Select(g => new
                    {
                        Provider = g.Key,
                        TotalCost = g.Sum(r => r.Cost)
                    })
                    .ToList();

                foreach (var providerCost in costByProvider)
                {
                    var provider = providerCost.Provider ?? "unknown";
                    var costPerMinute = (double)(providerCost.TotalCost / 5); // 5-minute window

                    CostRate.WithLabels(provider).Set(costPerMinute);

                    // Track cost changes
                    if (_lastCostUpdate.TryGetValue(provider, out var lastUpdate))
                    {
                        var timeDiff = (DateTime.UtcNow - lastUpdate).TotalMinutes;
                        if (timeDiff > 0 && _lastCostValue.TryGetValue(provider, out var lastCost))
                        {
                            var costDiff = providerCost.TotalCost - lastCost;
                            if (costDiff > 0)
                            {
                                CostTotal.WithLabels(provider, "all", "inference").Inc((double)costDiff);
                            }
                        }
                    }

                    _lastCostUpdate[provider] = DateTime.UtcNow;
                    _lastCostValue[provider] = providerCost.TotalCost;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting cost metrics");
            }
        }

        private async Task CollectProviderMetrics(IServiceScope scope)
        {
            try
            {
                var providerCredentialService = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.IProviderCredentialService>();
                var providers = await providerCredentialService.GetAllCredentialsAsync();

                foreach (var provider in providers.Where(p => p.IsEnabled))
                {
                    // Set provider health based on enabled status
                    // In a real implementation, this would check actual provider health
                    ProviderHealth.WithLabels(provider.ProviderType.ToString()).Set(1);
                }

                // Disabled providers
                foreach (var provider in providers.Where(p => !p.IsEnabled))
                {
                    ProviderHealth.WithLabels(provider.ProviderType.ToString()).Set(0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting provider metrics");
            }
        }

        private async Task CollectActiveEntityMetrics(IServiceScope scope)
        {
            try
            {
                var virtualKeyRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeyRepository>();
                var modelMappingService = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.IModelProviderMappingService>();

                // Count active virtual keys
                var allKeys = await virtualKeyRepo.GetAllAsync();
                var activeKeyCount = allKeys.Count(k => k.IsEnabled && (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow));
                ActiveVirtualKeys.Set(activeKeyCount);

                // Count active model mappings by provider
                var mappings = await modelMappingService.GetAllMappingsAsync();
                // Group by provider type
                // TODO: Fix IsEnabled check once we verify the return type
                var mappingsByProvider = mappings
                    // .Where(m => m.IsEnabled)
                    .GroupBy(m => m.ProviderType.ToString())
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

        public static void RecordSLAViolation(string slaType, string model)
        {
            SLAViolations.WithLabels(slaType, model).Inc();
        }
    }
}