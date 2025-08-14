namespace ConduitLLM.Configuration.DTOs.Routing
{
    /// <summary>
    /// Data transfer object for updating routing configuration
    /// </summary>
    public class UpdateRoutingConfigDto
    {
        /// <summary>
        /// Enable or disable failover
        /// </summary>
        public bool EnableFailover { get; set; }

        /// <summary>
        /// Enable or disable load balancing
        /// </summary>
        public bool EnableLoadBalancing { get; set; }

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        public int RequestTimeoutSeconds { get; set; }

        /// <summary>
        /// Circuit breaker threshold
        /// </summary>
        public int CircuitBreakerThreshold { get; set; }
    }
}