namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// DTO for exporting model costs
    /// </summary>
    public class ModelCostExportDto
    {
        public string CostName { get; set; } = string.Empty;
        public PricingModel PricingModel { get; set; } = PricingModel.Standard;
        public string? PricingConfiguration { get; set; }
        public decimal InputCostPerMillionTokens { get; set; }
        public decimal OutputCostPerMillionTokens { get; set; }
        public decimal? EmbeddingCostPerMillionTokens { get; set; }
        public decimal? ImageCostPerImage { get; set; }
        public decimal? VideoCostPerSecond { get; set; }
        public string? VideoResolutionMultipliers { get; set; }
        public string? ImageResolutionMultipliers { get; set; }
        public decimal? BatchProcessingMultiplier { get; set; }
        public bool SupportsBatchProcessing { get; set; }
        public decimal? CostPerSearchUnit { get; set; }
        public decimal? CostPerInferenceStep { get; set; }
        public int? DefaultInferenceSteps { get; set; }
    }
}