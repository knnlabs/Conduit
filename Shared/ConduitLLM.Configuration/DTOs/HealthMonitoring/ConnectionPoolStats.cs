namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Connection pool statistics
    /// </summary>
    public class ConnectionPoolStats
    {
        /// <summary>
        /// Database connection pool
        /// </summary>
        public PoolStats Database { get; set; } = new();

        /// <summary>
        /// Redis connection pool
        /// </summary>
        public PoolStats Redis { get; set; } = new();

        /// <summary>
        /// HTTP client connections
        /// </summary>
        public PoolStats HttpClients { get; set; } = new();
    }
}