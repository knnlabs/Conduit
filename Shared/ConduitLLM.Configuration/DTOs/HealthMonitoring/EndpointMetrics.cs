namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Metrics for a specific API endpoint
    /// </summary>
    public class EndpointMetrics
    {
        /// <summary>
        /// API endpoint path
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Total number of requests
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Number of successful requests
        /// </summary>
        public int SuccessfulRequests { get; set; }

        /// <summary>
        /// Total response time for all requests
        /// </summary>
        public double TotalResponseTime { get; set; }

        /// <summary>
        /// Maximum response time observed
        /// </summary>
        public double MaxResponseTime { get; set; }

        /// <summary>
        /// Minimum response time observed
        /// </summary>
        public double MinResponseTime { get; set; }

        /// <summary>
        /// Last time metrics were updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Average response time for this endpoint
        /// </summary>
        public double AverageResponseTime => TotalRequests > 0 ? TotalResponseTime / TotalRequests : 0;

        /// <summary>
        /// Success rate as a percentage
        /// </summary>
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 100;
    }
}