using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.DTOs.Metrics;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Channels;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using ConduitLLM.Configuration;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for streaming real-time metrics to dashboard clients.
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    public class MetricsHub : Hub
    {
        private readonly ILogger<MetricsHub> _logger;
        private readonly IMetricsAggregationService _metricsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsHub"/> class.
        /// </summary>
        public MetricsHub(
            ILogger<MetricsHub> logger,
            IMetricsAggregationService metricsService)
        {
            _logger = logger;
            _metricsService = metricsService;
        }

        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Metrics dashboard client connected: {ConnectionId}", Context.ConnectionId);
            
            // Send initial metrics snapshot
            var snapshot = await _metricsService.GetCurrentSnapshotAsync();
            await Clients.Caller.SendAsync("MetricsSnapshot", snapshot);
            
            // Add to metrics subscribers group
            await Groups.AddToGroupAsync(Context.ConnectionId, "metrics-subscribers");
            
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Metrics dashboard client disconnected: {ConnectionId}", Context.ConnectionId);
            
            // Remove from metrics subscribers group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "metrics-subscribers");
            
            if (exception != null)
            {
                _logger.LogError(exception, "Client disconnected with error");
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribes to specific metric updates.
        /// </summary>
        /// <param name="metricTypes">Types of metrics to subscribe to.</param>
        public async Task SubscribeToMetrics(string[] metricTypes)
        {
            _logger.LogInformation("Client {ConnectionId} subscribing to metrics: {MetricTypes}", 
                Context.ConnectionId, string.Join(", ", metricTypes));
            
            foreach (var metricType in metricTypes)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"metrics-{metricType.ToLower()}");
            }
        }

        /// <summary>
        /// Unsubscribes from specific metric updates.
        /// </summary>
        /// <param name="metricTypes">Types of metrics to unsubscribe from.</param>
        public async Task UnsubscribeFromMetrics(string[] metricTypes)
        {
            _logger.LogInformation("Client {ConnectionId} unsubscribing from metrics: {MetricTypes}", 
                Context.ConnectionId, string.Join(", ", metricTypes));
            
            foreach (var metricType in metricTypes)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"metrics-{metricType.ToLower()}");
            }
        }

        /// <summary>
        /// Streams real-time metrics to the client.
        /// </summary>
        /// <param name="interval">Update interval in seconds (minimum 1, maximum 60).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async IAsyncEnumerable<MetricsSnapshot> StreamMetrics(
            int interval,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Validate interval
            interval = Math.Max(1, Math.Min(60, interval));
            _logger.LogInformation("Client {ConnectionId} started streaming metrics with {Interval}s interval", 
                Context.ConnectionId, interval);

            var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
            
            try
            {
                while (await periodicTimer.WaitForNextTickAsync(cancellationToken))
                {
                    var snapshot = await _metricsService.GetCurrentSnapshotAsync();
                    yield return snapshot;
                }
            }
            finally
            {
                periodicTimer.Dispose();
                _logger.LogInformation("Client {ConnectionId} stopped streaming metrics", Context.ConnectionId);
            }
        }

        /// <summary>
        /// Gets historical metrics for a specific time range.
        /// </summary>
        /// <param name="request">Historical metrics request parameters.</param>
        /// <returns>Historical metrics data.</returns>
        public async Task<HistoricalMetricsResponse> GetHistoricalMetrics(HistoricalMetricsRequest request)
        {
            _logger.LogInformation("Client {ConnectionId} requesting historical metrics from {StartTime} to {EndTime}", 
                Context.ConnectionId, request.StartTime, request.EndTime);
            
            // Validate time range (max 24 hours)
            if ((request.EndTime - request.StartTime).TotalHours > 24)
            {
                throw new HubException("Time range cannot exceed 24 hours");
            }
            
            return await _metricsService.GetHistoricalMetricsAsync(request);
        }

        /// <summary>
        /// Gets real-time alert status.
        /// </summary>
        /// <returns>Current alert status.</returns>
        public async Task<List<MetricAlert>> GetAlertStatus()
        {
            _logger.LogInformation("Client {ConnectionId} requesting alert status", Context.ConnectionId);
            return await _metricsService.GetActiveAlertsAsync();
        }

        /// <summary>
        /// Requests a detailed provider health check.
        /// </summary>
        /// <param name="providerType">Optional provider type to check. If null, checks all providers.</param>
        /// <returns>Provider health details.</returns>
        public async Task<List<ProviderHealthStatus>> CheckProviderHealth(ProviderType? providerType = null)
        {
            _logger.LogInformation("Client {ConnectionId} requesting provider health check for: {Provider}", 
                Context.ConnectionId, providerType?.ToString() ?? "all");
            
            return await _metricsService.CheckProviderHealthAsync(providerType);
        }

        /// <summary>
        /// Gets top N virtual keys by various metrics.
        /// </summary>
        /// <param name="metric">Metric to sort by (requests, spend, errors).</param>
        /// <param name="count">Number of results to return.</param>
        /// <returns>Top virtual keys.</returns>
        public async Task<List<VirtualKeyStats>> GetTopVirtualKeys(string metric, int count = 10)
        {
            _logger.LogInformation("Client {ConnectionId} requesting top {Count} virtual keys by {Metric}", 
                Context.ConnectionId, count, metric);
            
            count = Math.Max(1, Math.Min(100, count)); // Limit between 1-100
            return await _metricsService.GetTopVirtualKeysAsync(metric, count);
        }
    }

    /// <summary>
    /// Metric alert information.
    /// </summary>
    public class MetricAlert
    {
        /// <summary>
        /// Alert ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Alert severity (info, warning, critical).
        /// </summary>
        public string Severity { get; set; } = "info";

        /// <summary>
        /// Metric that triggered the alert.
        /// </summary>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>
        /// Alert message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Current metric value.
        /// </summary>
        public double CurrentValue { get; set; }

        /// <summary>
        /// Threshold value.
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// When the alert was triggered.
        /// </summary>
        public DateTime TriggeredAt { get; set; }

        /// <summary>
        /// Is the alert currently active.
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Interface for metrics aggregation service.
    /// </summary>
    public interface IMetricsAggregationService
    {
        /// <summary>
        /// Gets the current metrics snapshot.
        /// </summary>
        Task<MetricsSnapshot> GetCurrentSnapshotAsync();

        /// <summary>
        /// Gets historical metrics for a time range.
        /// </summary>
        Task<HistoricalMetricsResponse> GetHistoricalMetricsAsync(HistoricalMetricsRequest request);

        /// <summary>
        /// Gets currently active alerts.
        /// </summary>
        Task<List<MetricAlert>> GetActiveAlertsAsync();

        /// <summary>
        /// Checks provider health status.
        /// </summary>
        Task<List<ProviderHealthStatus>> CheckProviderHealthAsync(ProviderType? providerType);

        /// <summary>
        /// Gets top virtual keys by metric.
        /// </summary>
        Task<List<VirtualKeyStats>> GetTopVirtualKeysAsync(string metric, int count);
    }
}