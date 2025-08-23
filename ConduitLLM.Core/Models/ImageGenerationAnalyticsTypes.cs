namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Extended provider statistics for analytics purposes.
    /// </summary>
    public class ImageGenerationProviderAnalyticsStats
    {
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int RequestCount { get; set; }
        public int ImageCount { get; set; }
        public decimal Cost { get; set; }
        public decimal AverageCostPerImage { get; set; }
        public double AverageResponseTime { get; set; }
        public double SuccessRate { get; set; }
        public Dictionary<string, ModelBreakdownStats> ModelBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Model breakdown statistics.
    /// </summary>
    public class ModelBreakdownStats
    {
        public string ModelName { get; set; } = string.Empty;
        public int RequestCount { get; set; }
        public int TotalImages { get; set; }
        public decimal TotalCost { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public Dictionary<string, int> SizeDistribution { get; set; } = new();
    }
}