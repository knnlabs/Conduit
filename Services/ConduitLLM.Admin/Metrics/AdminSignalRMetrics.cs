using Prometheus;

namespace ConduitLLM.Admin.Metrics
{
    /// <summary>
    /// Prometheus metrics for Admin SignalR hub operations.
    /// Tracks connections, subscriptions, and message delivery.
    /// </summary>
    public static class AdminSignalRMetrics
    {
        /// <summary>
        /// Total SignalR connections by status.
        /// </summary>
        public static readonly Counter Connections = Prometheus.Metrics
            .CreateCounter("conduit_admin_signalr_connections_total", "Total SignalR connections",
                new CounterConfiguration
                {
                    LabelNames = new[] { "event" } // event: connected, disconnected, disconnected_error
                });

        /// <summary>
        /// Currently active SignalR connections.
        /// </summary>
        public static readonly Gauge ActiveConnections = Prometheus.Metrics
            .CreateGauge("conduit_admin_signalr_active_connections", "Active SignalR connections");

        /// <summary>
        /// Total subscription operations by type and status.
        /// </summary>
        public static readonly Counter Subscriptions = Prometheus.Metrics
            .CreateCounter("conduit_admin_signalr_subscriptions_total", "Total subscription operations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "type", "action", "status" } // type: virtualkey, provider; action: subscribe, unsubscribe; status: success, failure
                });
    }
}
