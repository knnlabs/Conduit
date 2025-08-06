namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Individual pool statistics
    /// </summary>
    public class PoolStats
    {
        /// <summary>
        /// Active connections
        /// </summary>
        public int Active { get; set; }

        /// <summary>
        /// Idle connections
        /// </summary>
        public int Idle { get; set; }

        /// <summary>
        /// Maximum pool size
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// Pool utilization percentage
        /// </summary>
        public double UtilizationPercent { get; set; }

        /// <summary>
        /// Wait queue length
        /// </summary>
        public int WaitQueueLength { get; set; }
    }
}