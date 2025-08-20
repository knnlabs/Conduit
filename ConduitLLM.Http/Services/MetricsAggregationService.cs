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
using ConduitLLM.Configuration.DTOs.Metrics;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service that aggregates metrics from various sources and provides them to the dashboard.
    /// </summary>
    public partial class MetricsAggregationService : BackgroundService, IMetricsAggregationService
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

                        if (filteredSeries.DataPoints.Count() > 0)
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
                .Where(p => p.ProviderType.ToString().Equals(providerName, StringComparison.OrdinalIgnoreCase))
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


        /// <summary>
        /// Checks provider health status.
        /// </summary>
        /// <remarks>
        /// Provider health monitoring has been removed. This method now returns
        /// all enabled providers as healthy.
        /// </remarks>
        public async Task<List<ProviderHealthStatus>> CheckProviderHealthAsync(ProviderType? providerType)
        {
            using var scope = _serviceProvider.CreateScope();
            var providerRepository = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
            
            var healthStatuses = new List<ProviderHealthStatus>();
            
            // Get all providers
            var providers = await providerRepository.GetAllAsync();
            
            // Group providers by type
            var providersByType = providers
                .Where(p => p.IsEnabled)
                .GroupBy(p => p.ProviderType)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            foreach (var typeGroup in providersByType)
            {
                // Skip if filtering by type and this isn't the requested type
                if (providerType.HasValue && typeGroup.Key != providerType.Value)
                    continue;
                
                // All enabled providers are considered healthy
                healthStatuses.Add(new ProviderHealthStatus
                {
                    ProviderType = typeGroup.Key,
                    Status = "healthy",
                    AverageLatency = 0,
                    LastSuccessfulRequest = DateTime.UtcNow,
                    ErrorRate = 0,
                    IsEnabled = true,
                    AvailableModels = 0
                });
            }
            
            return healthStatuses;
        }
    }
}