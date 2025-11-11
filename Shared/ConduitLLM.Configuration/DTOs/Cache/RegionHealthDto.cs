namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for individual region health
    /// </summary>
    public class RegionHealthDto
    {
        /// <summary>
        /// Region status (healthy, degraded, unhealthy)
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response time in milliseconds
        /// </summary>
        public int ResponseTimeMs { get; set; }

        /// <summary>
        /// Whether the region is accessible
        /// </summary>
        public bool IsAccessible { get; set; }

        /// <summary>
        /// Error message if unhealthy
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}