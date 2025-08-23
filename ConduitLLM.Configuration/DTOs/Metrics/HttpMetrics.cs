namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// HTTP request metrics.
    /// </summary>
    public class HttpMetrics
    {
        /// <summary>
        /// Total requests per second.
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Active concurrent requests.
        /// </summary>
        public int ActiveRequests { get; set; }

        /// <summary>
        /// Request rate by endpoint.
        /// </summary>
        public Dictionary<string, double> EndpointRequestRates { get; set; } = new();

        /// <summary>
        /// Response time percentiles in milliseconds.
        /// </summary>
        public ResponseTimePercentiles ResponseTimes { get; set; } = new();

        /// <summary>
        /// Error rate percentage.
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Status code distribution.
        /// </summary>
        public Dictionary<int, int> StatusCodeCounts { get; set; } = new();

        /// <summary>
        /// Rate limit hits per minute.
        /// </summary>
        public int RateLimitHitsPerMinute { get; set; }
    }
}