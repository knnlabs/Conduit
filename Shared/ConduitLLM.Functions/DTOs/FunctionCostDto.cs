using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// DTO for function cost configuration information
/// </summary>
public class FunctionCostDto
{
    public int Id { get; set; }
    public required string CostName { get; set; }
    public FunctionPricingModel PricingModel { get; set; }
    public decimal? CostPerExecution { get; set; }
    public decimal? CostPerResult { get; set; }
    public decimal? CostPerToken { get; set; }
    public decimal? CostPerMinute { get; set; }
    public string? TieredPricing { get; set; }
    public string? PricingConfiguration { get; set; }
    public bool IsActive { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
