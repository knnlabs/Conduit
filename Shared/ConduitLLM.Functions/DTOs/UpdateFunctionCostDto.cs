using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// DTO for updating an existing function cost configuration
/// </summary>
public class UpdateFunctionCostDto
{
    public int Id { get; set; }
    public required string CostName { get; set; }
    public FunctionPurpose? Purpose { get; set; }
    public string? Description { get; set; }
    public decimal? BaseCost { get; set; }
    public FunctionPricingModel PricingModel { get; set; }
    public string? PricingConfiguration { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
