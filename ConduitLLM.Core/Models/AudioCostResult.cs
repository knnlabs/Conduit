namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Result of audio cost calculation.
    /// </summary>
    public class AudioCostResult
    {
        public string Provider { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public double UnitCount { get; set; }
        public string UnitType { get; set; } = string.Empty;
        public decimal RatePerUnit { get; set; }
        public double TotalCost { get; set; }
        public string? VirtualKey { get; set; }
        public string? Voice { get; set; }
        public bool IsEstimate { get; set; }
        public Dictionary<string, double>? DetailedBreakdown { get; set; }
    }
}