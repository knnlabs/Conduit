using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// DTO for creating a new function cost configuration
/// </summary>
public class CreateFunctionCostDto
{
    public required string CostName { get; set; }
    public FunctionProviderType ProviderType { get; set; }
    public FunctionPurpose? Purpose { get; set; }
    public string? Description { get; set; }
    public decimal? BaseCost { get; set; }
    public FunctionPricingModel PricingModel { get; set; }
    public string? PricingConfiguration { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 1;
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiryDate { get; set; }
}
