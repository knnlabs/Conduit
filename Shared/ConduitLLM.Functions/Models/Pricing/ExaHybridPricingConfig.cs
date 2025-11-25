using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Models.Pricing;

/// <summary>
/// Pricing configuration schema for Exa.ai search API using hybrid pricing model.
/// Combines search costs (tiered by result count and search type) with content extraction costs.
/// </summary>
/// <remarks>
/// This class defines the structure for Exa pricing configuration.
/// Actual pricing values must be provided via seed data or admin configuration.
/// All fields default to 0 - functions without cost configuration are free.
///
/// Exa pricing structure:
/// - Neural search: tiered by result count
/// - Keyword search: flat rate
/// - Content retrieval: per 1000 pieces of content
/// - Text/Highlights/Summary extraction: per page
///
/// This configuration is stored as JSON in FunctionCost.PricingConfiguration.
/// </remarks>
public class ExaHybridPricingConfig
{
    /// <summary>
    /// Search operation costs, tiered by search type and result count.
    /// </summary>
    [JsonPropertyName("searchCosts")]
    public SearchCostTiers? SearchCosts { get; set; }

    /// <summary>
    /// Content retrieval costs (for get contents API).
    /// </summary>
    [JsonPropertyName("contentRetrievalCosts")]
    public ContentRetrievalCosts? ContentRetrievalCosts { get; set; }

    /// <summary>
    /// Content extraction costs per page for various extraction types.
    /// </summary>
    [JsonPropertyName("contentExtractionCosts")]
    public required ContentExtractionCosts ContentExtractionCosts { get; set; }
}

/// <summary>
/// Search cost tiers for different search types.
/// </summary>
public class SearchCostTiers
{
    /// <summary>
    /// Neural (embeddings-based) search pricing with tiered costs based on result count.
    /// </summary>
    [JsonPropertyName("neural")]
    public required NeuralSearchCosts Neural { get; set; }

    /// <summary>
    /// Keyword (traditional SERP) search pricing.
    /// </summary>
    [JsonPropertyName("keyword")]
    public required KeywordSearchCosts Keyword { get; set; }

    /// <summary>
    /// Auto search mode configuration (intelligently combines neural + keyword).
    /// </summary>
    [JsonPropertyName("auto")]
    public AutoSearchCosts? Auto { get; set; }
}

/// <summary>
/// Neural search costs with two-tier pricing based on result count.
/// </summary>
public class NeuralSearchCosts
{
    /// <summary>
    /// Tier 1: Low result count (typically 1-25 results).
    /// </summary>
    [JsonPropertyName("tier1")]
    public required SearchCostTier Tier1 { get; set; }

    /// <summary>
    /// Tier 2: High result count (typically 26-100 results).
    /// </summary>
    [JsonPropertyName("tier2")]
    public required SearchCostTier Tier2 { get; set; }
}

/// <summary>
/// Keyword search costs (typically flat rate regardless of result count).
/// </summary>
public class KeywordSearchCosts
{
    /// <summary>
    /// Flat cost per keyword search request.
    /// </summary>
    [JsonPropertyName("cost")]
    public decimal Cost { get; set; }
}

/// <summary>
/// Auto search mode configuration.
/// </summary>
public class AutoSearchCosts
{
    /// <summary>
    /// If true, assumes keyword pricing when search type is "auto" (conservative estimate).
    /// If false, assumes neural tier1 pricing.
    /// </summary>
    [JsonPropertyName("fallbackToKeyword")]
    public bool FallbackToKeyword { get; set; }
}

/// <summary>
/// Represents a single pricing tier for search operations.
/// </summary>
public class SearchCostTier
{
    /// <summary>
    /// Maximum number of results for this tier (exclusive).
    /// Null means this tier applies to all results beyond the previous tier.
    /// </summary>
    [JsonPropertyName("maxResults")]
    public int? MaxResults { get; set; }

    /// <summary>
    /// Cost in USD for searches in this tier.
    /// </summary>
    [JsonPropertyName("cost")]
    public decimal Cost { get; set; }
}

/// <summary>
/// Content retrieval costs for get contents API.
/// </summary>
public class ContentRetrievalCosts
{
    /// <summary>
    /// Base cost per 1000 pieces of content retrieved (pages including subpages).
    /// Must be configured via seed data or admin UI.
    /// </summary>
    [JsonPropertyName("costPer1000Pages")]
    public decimal CostPer1000Pages { get; set; }
}

/// <summary>
/// Content extraction costs per page for various extraction types.
/// </summary>
public class ContentExtractionCosts
{
    /// <summary>
    /// Cost per page for full text extraction.
    /// </summary>
    [JsonPropertyName("text")]
    public decimal Text { get; set; }

    /// <summary>
    /// Cost per page for highlights extraction.
    /// </summary>
    [JsonPropertyName("highlights")]
    public decimal Highlights { get; set; }

    /// <summary>
    /// Cost per page for AI-generated summary.
    /// </summary>
    [JsonPropertyName("summary")]
    public decimal Summary { get; set; }
}
