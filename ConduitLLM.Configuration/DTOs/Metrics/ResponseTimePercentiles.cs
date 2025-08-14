namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Response time percentiles.
    /// </summary>
    public class ResponseTimePercentiles
    {
        public double P50 { get; set; }
        public double P90 { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }
    }
}