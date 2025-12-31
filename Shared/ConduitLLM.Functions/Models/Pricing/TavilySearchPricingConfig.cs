using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Models.Pricing;

/// <summary>
/// Pricing configuration schema for Tavily search API using credit-based pricing model.
/// </summary>
/// <remarks>
/// This class defines the structure for Tavily pricing configuration.
/// Actual pricing values must be provided via seed data or admin configuration.
/// All fields default to 0 - functions without cost configuration are free.
///
/// Tavily pricing structure:
/// - Basic search: N credits
/// - Advanced search: N credits
/// - Auto-parameters addon: N credits (optional)
/// - All content extraction (raw content, images, answer) included in search cost
///
/// Unlike Exa, Tavily does not charge separately for content extraction.
/// The search cost is all-inclusive based on search depth.
///
/// This configuration is stored as JSON in FunctionCost.PricingConfiguration.
/// </remarks>
public class TavilySearchPricingConfig
{
    /// <summary>
    /// Cost per credit in USD.
    /// Must be configured via seed data or admin UI.
    /// </summary>
    [JsonPropertyName("costPerCredit")]
    public decimal CostPerCredit { get; set; }

    /// <summary>
    /// Credits consumed by basic search.
    /// Basic search provides quick results with standard depth.
    /// </summary>
    [JsonPropertyName("basicSearchCredits")]
    public int BasicSearchCredits { get; set; }

    /// <summary>
    /// Credits consumed by advanced search.
    /// Advanced search provides more comprehensive results with deeper analysis.
    /// Supports content chunks (1-3 chunks per source).
    /// </summary>
    [JsonPropertyName("advancedSearchCredits")]
    public int AdvancedSearchCredits { get; set; }

    /// <summary>
    /// Additional credits for auto-parameters feature.
    /// When enabled, Tavily automatically configures optimal search parameters.
    /// Null means not supported or not charged separately.
    /// </summary>
    [JsonPropertyName("autoParametersCredits")]
    public int? AutoParametersCredits { get; set; }

    /// <summary>
    /// Whether to charge for answer generation separately.
    /// Default: false (included in search cost).
    /// This flag allows future-proofing if Tavily changes pricing model.
    /// </summary>
    [JsonPropertyName("chargeForAnswerGeneration")]
    public bool ChargeForAnswerGeneration { get; set; } = false;

    /// <summary>
    /// Cost for answer generation if charged separately (default: 0).
    /// Only applied if ChargeForAnswerGeneration is true.
    /// </summary>
    [JsonPropertyName("answerGenerationCost")]
    public decimal? AnswerGenerationCost { get; set; } = 0m;

    /// <summary>
    /// Whether to charge for image results separately.
    /// Default: false (included in search cost).
    /// </summary>
    [JsonPropertyName("chargeForImageResults")]
    public bool ChargeForImageResults { get; set; } = false;

    /// <summary>
    /// Cost per image result if charged separately (default: 0).
    /// Only applied if ChargeForImageResults is true.
    /// </summary>
    [JsonPropertyName("costPerImage")]
    public decimal? CostPerImage { get; set; } = 0m;
}
