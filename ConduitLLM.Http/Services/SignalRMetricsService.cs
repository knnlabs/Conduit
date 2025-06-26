using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for tracking SignalR connection metrics and hub activity.
    /// Critical for monitoring real-time communication at 10K scale.
    /// </summary>
    public class SignalRMetricsService : IHostedService, IDisposable
    {
        private readonly ILogger<SignalRMetricsService> _logger;
        private readonly ConcurrentDictionary<string, ConnectionInfo> _activeConnections;
        private Timer? _metricsTimer;

        // Connection tracking
        private class ConnectionInfo
        {
            public string ConnectionId { get; set; } = string.Empty;
            public string HubName { get; set; } = string.Empty;
            public string VirtualKeyId { get; set; } = string.Empty;
            public DateTime ConnectedAt { get; set; }
            public DateTime LastActivity { get; set; }
        }

        // Prometheus metrics
        private static readonly Gauge ActiveConnections = Metrics
            .CreateGauge("conduit_signalr_connections_active", "Number of active SignalR connections",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "hub", "virtual_key_id" }
                });

        private static readonly Counter ConnectionsTotal = Metrics
            .CreateCounter("conduit_signalr_connections_total", "Total number of SignalR connections",
                new CounterConfiguration
                {
                    LabelNames = new[] { "hub", "status" } // status: connected, disconnected, failed
                });

        private static readonly Histogram ConnectionDuration = Metrics
            .CreateHistogram("conduit_signalr_connection_duration_seconds", "SignalR connection duration in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "hub" },
                    Buckets = Histogram.ExponentialBuckets(1, 2, 16) // 1s to ~18 hours
                });

        private static readonly Counter MessagesTotal = Metrics
            .CreateCounter("conduit_signalr_messages_total", "Total number of SignalR messages",
                new CounterConfiguration
                {
                    LabelNames = new[] { "hub", "method", "direction" } // direction: sent, received
                });

        private static readonly Counter SubscriptionsTotal = Metrics
            .CreateCounter("conduit_signalr_subscriptions_total", "Total number of task subscriptions",
                new CounterConfiguration
                {
                    LabelNames = new[] { "hub", "task_type" } // task_type: image, video
                });

        private static readonly Gauge ActiveSubscriptions = Metrics
            .CreateGauge("conduit_signalr_subscriptions_active", "Number of active task subscriptions",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "hub", "task_type" }
                });

        private static readonly Counter ReconnectionsTotal = Metrics
            .CreateCounter("conduit_signalr_reconnections_total", "Total number of SignalR reconnections",
                new CounterConfiguration
                {
                    LabelNames = new[] { "hub" }
                });

        private static readonly Summary MessageProcessingTime = Metrics
            .CreateSummary("conduit_signalr_message_processing_seconds", "SignalR message processing time",
                new SummaryConfiguration
                {
                    LabelNames = new[] { "hub", "method" },
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

        private static readonly Gauge ConnectionPoolUtilization = Metrics
            .CreateGauge("conduit_signalr_connection_pool_utilization", "SignalR connection pool utilization percentage",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "hub" }
                });

        // Connection limits from issue spec
        private const int MaxConnectionsPerVirtualKey = 100;
        private const int MaxTotalConnections = 10000;

        public SignalRMetricsService(ILogger<SignalRMetricsService> logger)
        {
            _logger = logger;
            _activeConnections = new ConcurrentDictionary<string, ConnectionInfo>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR metrics service starting...");

            // Start periodic metrics calculation (every 30 seconds)
            _metricsTimer = new Timer(CalculateMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR metrics service stopping...");

            _metricsTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _metricsTimer?.Dispose();
        }

        /// <summary>
        /// Track a new SignalR connection
        /// </summary>
        public void OnConnected(string connectionId, string hubName, string virtualKeyId)
        {
            var info = new ConnectionInfo
            {
                ConnectionId = connectionId,
                HubName = hubName,
                VirtualKeyId = virtualKeyId,
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };

            if (_activeConnections.TryAdd(connectionId, info))
            {
                ConnectionsTotal.WithLabels(hubName, "connected").Inc();
                ActiveConnections.WithLabels(hubName, virtualKeyId).Inc();

                _logger.LogDebug("SignalR connection {ConnectionId} connected to hub {HubName}", connectionId, hubName);
            }
        }

        /// <summary>
        /// Track a SignalR disconnection
        /// </summary>
        public void OnDisconnected(string connectionId, string? exception = null)
        {
            if (_activeConnections.TryRemove(connectionId, out var info))
            {
                var duration = (DateTime.UtcNow - info.ConnectedAt).TotalSeconds;
                var status = string.IsNullOrEmpty(exception) ? "disconnected" : "failed";

                ConnectionsTotal.WithLabels(info.HubName, status).Inc();
                ActiveConnections.WithLabels(info.HubName, info.VirtualKeyId).Dec();
                ConnectionDuration.WithLabels(info.HubName).Observe(duration);

                _logger.LogDebug("SignalR connection {ConnectionId} disconnected from hub {HubName} after {Duration:F2}s",
                    connectionId, info.HubName, duration);
            }
        }

        /// <summary>
        /// Track a reconnection
        /// </summary>
        public void OnReconnected(string connectionId, string hubName)
        {
            ReconnectionsTotal.WithLabels(hubName).Inc();
            
            if (_activeConnections.TryGetValue(connectionId, out var info))
            {
                info.LastActivity = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Track message sent to client
        /// </summary>
        public void OnMessageSent(string hubName, string method, double processingTimeMs = 0)
        {
            MessagesTotal.WithLabels(hubName, method, "sent").Inc();
            
            if (processingTimeMs > 0)
            {
                MessageProcessingTime.WithLabels(hubName, method).Observe(processingTimeMs / 1000.0);
            }
        }

        /// <summary>
        /// Track message received from client
        /// </summary>
        public void OnMessageReceived(string hubName, string method)
        {
            MessagesTotal.WithLabels(hubName, method, "received").Inc();
        }

        /// <summary>
        /// Track task subscription
        /// </summary>
        public void OnTaskSubscribed(string hubName, string taskType)
        {
            SubscriptionsTotal.WithLabels(hubName, taskType).Inc();
            ActiveSubscriptions.WithLabels(hubName, taskType).Inc();
        }

        /// <summary>
        /// Track task unsubscription
        /// </summary>
        public void OnTaskUnsubscribed(string hubName, string taskType)
        {
            ActiveSubscriptions.WithLabels(hubName, taskType).Dec();
        }

        /// <summary>
        /// Get connection count for a virtual key
        /// </summary>
        public int GetConnectionCountForVirtualKey(string virtualKeyId)
        {
            var count = 0;
            foreach (var connection in _activeConnections.Values)
            {
                if (connection.VirtualKeyId == virtualKeyId)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Check if virtual key has reached connection limit
        /// </summary>
        public bool IsConnectionLimitReached(string virtualKeyId)
        {
            return GetConnectionCountForVirtualKey(virtualKeyId) >= MaxConnectionsPerVirtualKey;
        }

        /// <summary>
        /// Check if global connection limit is reached
        /// </summary>
        public bool IsGlobalConnectionLimitReached()
        {
            return _activeConnections.Count >= MaxTotalConnections;
        }

        private void CalculateMetrics(object? state)
        {
            try
            {
                // Calculate connection pool utilization per hub
                var hubConnections = new Dictionary<string, int>();
                foreach (var connection in _activeConnections.Values)
                {
                    if (!hubConnections.ContainsKey(connection.HubName))
                        hubConnections[connection.HubName] = 0;
                    hubConnections[connection.HubName]++;
                }

                // Update pool utilization metrics
                foreach (var (hub, count) in hubConnections)
                {
                    var utilization = (double)count / MaxTotalConnections * 100;
                    ConnectionPoolUtilization.WithLabels(hub).Set(utilization);
                }

                // Clean up stale connections (no activity for 5 minutes)
                var staleThreshold = DateTime.UtcNow.AddMinutes(-5);
                var staleConnections = _activeConnections
                    .Where(kvp => kvp.Value.LastActivity < staleThreshold)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var connectionId in staleConnections)
                {
                    _logger.LogWarning("Removing stale SignalR connection {ConnectionId}", connectionId);
                    OnDisconnected(connectionId, "Stale connection removed");
                }

                // Log warning if approaching limits
                var totalConnections = _activeConnections.Count;
                if (totalConnections > MaxTotalConnections * 0.8)
                {
                    _logger.LogWarning("SignalR connections approaching limit: {Count}/{Max} ({Percentage:F1}%)",
                        totalConnections, MaxTotalConnections, (double)totalConnections / MaxTotalConnections * 100);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating SignalR metrics");
            }
        }
    }
}