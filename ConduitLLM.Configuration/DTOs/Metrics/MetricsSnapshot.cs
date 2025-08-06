using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Real-time metrics snapshot sent via SignalR to dashboard clients.
    /// </summary>
    public class MetricsSnapshot
    {
        /// <summary>
        /// Timestamp when this snapshot was captured.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// HTTP request metrics.
        /// </summary>
        public HttpMetrics Http { get; set; } = new();

        /// <summary>
        /// Infrastructure metrics.
        /// </summary>
        public InfrastructureMetrics Infrastructure { get; set; } = new();

        /// <summary>
        /// Business metrics.
        /// </summary>
        public BusinessMetrics Business { get; set; } = new();

        /// <summary>
        /// Provider health status.
        /// </summary>
        public List<ProviderHealthStatus> ProviderHealth { get; set; } = new();

        /// <summary>
        /// System resource metrics.
        /// </summary>
        public SystemMetrics System { get; set; } = new();
    }
}