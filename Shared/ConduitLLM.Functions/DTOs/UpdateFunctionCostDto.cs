using ConduitLLM.Functions.Enums;
using System.Text.Json;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// DTO for updating an existing function cost configuration
/// </summary>
public class UpdateFunctionCostDto
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int Id { get; set; }
    public string? CostName { get; set; }
    public FunctionPurpose? Purpose { get; set; }
    public string? Description { get; set; }
    public decimal? BaseCost { get; set; }
    public FunctionPricingModel? PricingModel { get; set; }
    public Dictionary<string, JsonElement>? PricingConfiguration { get; set; }
    public bool? IsActive { get; set; }
    public int? Priority { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
