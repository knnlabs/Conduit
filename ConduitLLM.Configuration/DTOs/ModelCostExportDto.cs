namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// DTO for exporting model costs
    /// </summary>
    public class ModelCostExportDto
    {
        public string CostName { get; set; } = string.Empty;
        public decimal InputTokenCost { get; set; }
        public decimal OutputTokenCost { get; set; }
        public decimal? EmbeddingTokenCost { get; set; }
        public decimal? ImageCostPerImage { get; set; }
        public decimal? AudioCostPerMinute { get; set; }
        public decimal? AudioCostPerKCharacters { get; set; }
        public decimal? AudioInputCostPerMinute { get; set; }
        public decimal? AudioOutputCostPerMinute { get; set; }
        public decimal? VideoCostPerSecond { get; set; }
        public string? VideoResolutionMultipliers { get; set; }
        public decimal? BatchProcessingMultiplier { get; set; }
        public bool SupportsBatchProcessing { get; set; }
        public decimal? CostPerSearchUnit { get; set; }
        public decimal? CostPerInferenceStep { get; set; }
        public int? DefaultInferenceSteps { get; set; }
    }
}