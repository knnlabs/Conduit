using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Models.Pricing;

/// <summary>
/// Pricing configuration for Perplexity requests that combine a base search
/// charge with input and output token charges.
/// </summary>
public class PerplexityHybridPricingConfig
{
    [JsonPropertyName("baseRequestCost")]
    public required decimal BaseRequestCost { get; set; }

    [JsonPropertyName("inputTokenCostPerMillion")]
    public required decimal InputTokenCostPerMillion { get; set; }

    [JsonPropertyName("outputTokenCostPerMillion")]
    public required decimal OutputTokenCostPerMillion { get; set; }
}
