using System;
using System.Collections.Generic;

using ConduitLLM.Configuration;

namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Response DTO for the routing configuration endpoint.
    /// </summary>
    public class RoutingConfigurationDto
    {
        /// <summary>
        /// Timestamp when the configuration snapshot was generated (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Model-to-provider routing rules.
        /// </summary>
        public List<RoutingRuleDto> RoutingRules { get; set; } = new();

        /// <summary>
        /// Configured load balancers.
        /// </summary>
        public List<LoadBalancerDto> LoadBalancers { get; set; } = new();

        /// <summary>
        /// Routing statistics for the last 24 hours.
        /// </summary>
        public RoutingStatisticsDto Statistics { get; set; } = new();

        /// <summary>
        /// General routing configuration settings.
        /// </summary>
        public RoutingSettingsDto Configuration { get; set; } = new();
    }

    /// <summary>
    /// A single model-to-provider routing rule.
    /// </summary>
    public class RoutingRuleDto
    {
        /// <summary>
        /// The model provider mapping ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The model alias exposed to clients.
        /// </summary>
        public string ModelAlias { get; set; } = string.Empty;

        /// <summary>
        /// The provider-specific model identifier.
        /// </summary>
        public string ProviderModelId { get; set; } = string.Empty;

        /// <summary>
        /// Whether the mapping is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// The provider that serves this rule.
        /// </summary>
        public RoutingRuleProviderDto Provider { get; set; } = new();
    }

    /// <summary>
    /// Provider details for a routing rule.
    /// </summary>
    public class RoutingRuleProviderDto
    {
        /// <summary>
        /// The provider ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The provider display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The provider type.
        /// </summary>
        public ProviderType Type { get; set; }

        /// <summary>
        /// Whether the provider is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Load balancer configuration details.
    /// </summary>
    public class LoadBalancerDto
    {
        /// <summary>
        /// The load balancer identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The load balancer display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The load balancing algorithm in use.
        /// </summary>
        public string Algorithm { get; set; } = string.Empty;

        /// <summary>
        /// Health check interval in seconds.
        /// </summary>
        public int HealthCheckInterval { get; set; }

        /// <summary>
        /// Number of failures before failover is triggered.
        /// </summary>
        public int FailoverThreshold { get; set; }

        /// <summary>
        /// Provider endpoints behind this load balancer.
        /// </summary>
        public List<LoadBalancerEndpointDto> Endpoints { get; set; } = new();
    }

    /// <summary>
    /// A provider endpoint behind a load balancer.
    /// </summary>
    public class LoadBalancerEndpointDto
    {
        /// <summary>
        /// The provider ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The provider display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The provider type name.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The endpoint URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// The load balancing weight assigned to this endpoint.
        /// </summary>
        public int Weight { get; set; }
    }

    /// <summary>
    /// Routing statistics over the last 24 hours.
    /// </summary>
    public class RoutingStatisticsDto
    {
        /// <summary>
        /// Total number of requests processed.
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Per-model request distribution.
        /// </summary>
        public List<ProviderDistributionDto> ProviderDistribution { get; set; } = new();
    }

    /// <summary>
    /// Request distribution statistics for a single model.
    /// </summary>
    public class ProviderDistributionDto
    {
        /// <summary>
        /// The model name the requests were routed for.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Number of requests processed.
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Percentage of requests that succeeded (0-100).
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average response latency in milliseconds.
        /// </summary>
        public double AvgLatency { get; set; }
    }

    /// <summary>
    /// General routing configuration settings.
    /// </summary>
    public class RoutingSettingsDto
    {
        /// <summary>
        /// Whether automatic failover is enabled.
        /// </summary>
        public bool EnableFailover { get; set; }

        /// <summary>
        /// Whether load balancing is enabled.
        /// </summary>
        public bool EnableLoadBalancing { get; set; }

        /// <summary>
        /// Request timeout in seconds.
        /// </summary>
        public int RequestTimeout { get; set; }

        /// <summary>
        /// Number of failures before the circuit breaker opens.
        /// </summary>
        public int CircuitBreakerThreshold { get; set; }
    }
}
