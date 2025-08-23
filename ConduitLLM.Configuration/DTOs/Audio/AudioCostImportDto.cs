namespace ConduitLLM.Configuration.DTOs.Audio
{
    /// <summary>
    /// DTO for importing audio costs.
    /// </summary>
    public class AudioCostImportDto
    {
        public int ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string CostUnit { get; set; } = string.Empty;
        public decimal CostPerUnit { get; set; }
        public decimal? MinimumCharge { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? EffectiveFrom { get; set; }
    }
}