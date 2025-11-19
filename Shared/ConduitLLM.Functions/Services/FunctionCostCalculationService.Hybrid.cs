using System.Text.Json;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Models;
using ConduitLLM.Functions.Models.Pricing;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Hybrid pricing model calculations for FunctionCostCalculationService.
/// </summary>
public partial class FunctionCostCalculationService
{
    /// <summary>
    /// Calculates cost using hybrid pricing model (multiple pricing dimensions).
    /// </summary>
    /// <param name="functionCost">The function cost configuration.</param>
    /// <param name="usage">The usage data containing multiple billing dimensions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calculated cost based on hybrid pricing rules.</returns>
    /// <remarks>
    /// Hybrid pricing combines multiple cost components:
    ///
    /// For Exa.ai example:
    /// 1. Search cost (based on type: neural vs keyword, and result count tier)
    /// 2. Content extraction costs (text, highlights, summary) per page
    ///
    /// Total Cost = SearchCost + ContentExtractionCosts
    ///
    /// Configuration is stored as JSON in PricingConfiguration field.
    /// The specific structure depends on the provider (e.g., ExaHybridPricingConfig for Exa).
    ///
    /// This model supports the most complex pricing scenarios.
    /// </remarks>
    private async Task<decimal> CalculateHybridCostAsync(
        FunctionCost functionCost,
        FunctionExecutionUsage usage,
        CancellationToken cancellationToken)
    {
        // Parse hybrid pricing configuration
        if (string.IsNullOrWhiteSpace(functionCost.PricingConfiguration))
        {
            _logger.LogWarning("Hybrid pricing model configured but PricingConfiguration JSON is null/empty for cost {CostName}. Returning 0.",
                functionCost.CostName);
            return 0m;
        }

        // Try to detect configuration type from structure
        // Try Exa format first
        try
        {
            var exaConfig = JsonSerializer.Deserialize<ExaHybridPricingConfig>(functionCost.PricingConfiguration);
            if (exaConfig != null)
            {
                return CalculateExaHybridCost(exaConfig, usage);
            }
        }
        catch (JsonException)
        {
            // Not Exa format, try next
            _logger.LogDebug("Failed to parse as Exa hybrid pricing config, trying Tavily format...");
        }

        // Try Tavily format
        try
        {
            var tavilyConfig = JsonSerializer.Deserialize<TavilySearchPricingConfig>(functionCost.PricingConfiguration);
            if (tavilyConfig != null)
            {
                return CalculateTavilySearchCost(tavilyConfig, usage);
            }
        }
        catch (JsonException)
        {
            // Not Tavily format either
            _logger.LogDebug("Failed to parse as Tavily pricing config, trying generic hybrid format...");
        }

        // Future: Add other hybrid pricing formats here as needed
        // For example:
        // - Perplexity hybrid pricing (base + tokens + citations)
        // - Custom RAG pricing (storage + retrieval + embeddings)

        _logger.LogWarning("Could not parse hybrid pricing configuration for cost {CostName}. Unknown format. Returning 0.",
            functionCost.CostName);
        return 0m;
    }

    /// <summary>
    /// Calculates cost using Exa.ai hybrid pricing rules.
    /// </summary>
    /// <param name="config">The Exa hybrid pricing configuration.</param>
    /// <param name="usage">The usage data.</param>
    /// <returns>The calculated cost.</returns>
    /// <remarks>
    /// Exa pricing breakdown:
    /// 1. Search cost: Depends on search type (neural/keyword) and result count tier
    /// 2. Text extraction: $0.001 per page
    /// 3. Highlights extraction: $0.001 per page
    /// 4. Summary generation: $0.001 per page
    ///
    /// Example: Neural search with 30 results, all with text and highlights:
    /// - Neural search (26-100 tier): $0.025
    /// - Text extraction: 30 × $0.001 = $0.030
    /// - Highlights extraction: 30 × $0.001 = $0.030
    /// - Total: $0.085
    /// </remarks>
    private decimal CalculateExaHybridCost(ExaHybridPricingConfig config, FunctionExecutionUsage usage)
    {
        decimal totalCost = 0m;

        // 1. Calculate search cost
        decimal searchCost = CalculateExaSearchCost(config, usage);
        totalCost += searchCost;

        // 2. Calculate content extraction costs
        decimal contentCost = CalculateExaContentExtractionCost(config, usage);
        totalCost += contentCost;

        _logger.LogDebug("Exa hybrid cost breakdown: Search=${SearchCost}, Content=${ContentCost}, Total=${TotalCost}",
            searchCost, contentCost, totalCost);

        return totalCost;
    }

    /// <summary>
    /// Calculates Exa search cost based on search type and result count.
    /// </summary>
    private decimal CalculateExaSearchCost(ExaHybridPricingConfig config, FunctionExecutionUsage usage)
    {
        var searchType = usage.SearchType?.ToLowerInvariant() ?? "auto";
        var resultCount = usage.ResultCount ?? 0;

        if (resultCount == 0)
        {
            _logger.LogDebug("No results returned, search cost = 0");
            return 0m;
        }

        decimal searchCost;

        switch (searchType)
        {
            case "neural":
                // Determine tier based on result count
                if (resultCount <= (config.SearchCosts.Neural.Tier1.MaxResults ?? 25))
                {
                    searchCost = config.SearchCosts.Neural.Tier1.Cost;
                    _logger.LogDebug("Neural search tier 1 (1-{Max} results): ${Cost}",
                        config.SearchCosts.Neural.Tier1.MaxResults ?? 25, searchCost);
                }
                else
                {
                    searchCost = config.SearchCosts.Neural.Tier2.Cost;
                    _logger.LogDebug("Neural search tier 2 ({Min}+ results): ${Cost}",
                        (config.SearchCosts.Neural.Tier1.MaxResults ?? 25) + 1, searchCost);
                }
                break;

            case "keyword":
                searchCost = config.SearchCosts.Keyword.Cost;
                _logger.LogDebug("Keyword search (any results): ${Cost}", searchCost);
                break;

            case "auto":
                // Auto mode: use conservative estimate (keyword pricing by default, or neural tier 1 if configured)
                if (config.SearchCosts.Auto?.FallbackToKeyword == true)
                {
                    searchCost = config.SearchCosts.Keyword.Cost;
                    _logger.LogDebug("Auto search (fallback to keyword pricing): ${Cost}", searchCost);
                }
                else
                {
                    // Conservative: assume neural tier 1
                    searchCost = config.SearchCosts.Neural.Tier1.Cost;
                    _logger.LogDebug("Auto search (fallback to neural tier 1 pricing): ${Cost}", searchCost);
                }
                break;

            default:
                _logger.LogWarning("Unknown search type '{SearchType}', defaulting to keyword pricing", searchType);
                searchCost = config.SearchCosts.Keyword.Cost;
                break;
        }

        return searchCost;
    }

    /// <summary>
    /// Calculates Exa content extraction costs (text, highlights, summary).
    /// </summary>
    private decimal CalculateExaContentExtractionCost(ExaHybridPricingConfig config, FunctionExecutionUsage usage)
    {
        decimal totalContentCost = 0m;

        // Text extraction cost
        if (usage.TextPagesExtracted.HasValue && usage.TextPagesExtracted.Value > 0)
        {
            var textCost = usage.TextPagesExtracted.Value * config.ContentExtractionCosts.Text;
            totalContentCost += textCost;
            _logger.LogDebug("Text extraction: {Pages} pages × ${CostPerPage} = ${Cost}",
                usage.TextPagesExtracted.Value, config.ContentExtractionCosts.Text, textCost);
        }

        // Highlights extraction cost
        if (usage.HighlightPagesExtracted.HasValue && usage.HighlightPagesExtracted.Value > 0)
        {
            var highlightsCost = usage.HighlightPagesExtracted.Value * config.ContentExtractionCosts.Highlights;
            totalContentCost += highlightsCost;
            _logger.LogDebug("Highlights extraction: {Pages} pages × ${CostPerPage} = ${Cost}",
                usage.HighlightPagesExtracted.Value, config.ContentExtractionCosts.Highlights, highlightsCost);
        }

        // Summary generation cost
        if (usage.SummaryPagesGenerated.HasValue && usage.SummaryPagesGenerated.Value > 0)
        {
            var summaryCost = usage.SummaryPagesGenerated.Value * config.ContentExtractionCosts.Summary;
            totalContentCost += summaryCost;
            _logger.LogDebug("Summary generation: {Pages} pages × ${CostPerPage} = ${Cost}",
                usage.SummaryPagesGenerated.Value, config.ContentExtractionCosts.Summary, summaryCost);
        }

        return totalContentCost;
    }

    /// <summary>
    /// Calculates cost using Tavily search pricing rules.
    /// </summary>
    /// <param name="config">The Tavily pricing configuration.</param>
    /// <param name="usage">The usage data.</param>
    /// <returns>The calculated cost.</returns>
    /// <remarks>
    /// Tavily pricing breakdown:
    /// 1. Base search cost: Depends on search depth (basic: 1 credit, advanced: 2 credits)
    /// 2. Auto-parameters addon: 2 credits (if enabled)
    /// 3. All content extraction included in base cost (images, answer, raw content)
    ///
    /// Example: Advanced search with auto-parameters:
    /// - Advanced search: 2 credits × $0.008 = $0.016
    /// - Auto-parameters: 2 credits × $0.008 = $0.016
    /// - Total: $0.032
    ///
    /// Unlike Exa, Tavily does NOT charge per result or for content extraction.
    /// </remarks>
    private decimal CalculateTavilySearchCost(TavilySearchPricingConfig config, FunctionExecutionUsage usage)
    {
        decimal totalCost = 0m;
        int totalCredits = 0;

        // 1. Calculate base search cost
        var searchDepth = usage.SearchType?.ToLowerInvariant() ?? "basic";
        int searchCredits = searchDepth == "advanced"
            ? config.AdvancedSearchCredits
            : config.BasicSearchCredits;

        totalCredits += searchCredits;
        _logger.LogDebug("Tavily {SearchDepth} search: {Credits} credits",
            searchDepth, searchCredits);

        // 2. Add auto-parameters cost if enabled
        if (usage.Metadata?.TryGetValue("autoParametersEnabled", out var autoParamsObj) == true)
        {
            if (autoParamsObj is bool autoParams && autoParams)
            {
                var autoParamsCredits = config.AutoParametersCredits ?? 0;
                totalCredits += autoParamsCredits;
                _logger.LogDebug("Tavily auto-parameters: {Credits} credits", autoParamsCredits);
            }
        }

        // 3. Calculate base cost from credits
        totalCost = totalCredits * config.CostPerCredit;

        // 4. Optional: Charge for answer generation separately (future-proofing)
        if (config.ChargeForAnswerGeneration &&
            usage.Metadata?.ContainsKey("answerGenerated") == true)
        {
            var answerCost = config.AnswerGenerationCost ?? 0m;
            totalCost += answerCost;
            _logger.LogDebug("Tavily answer generation: ${Cost}", answerCost);
        }

        // 5. Optional: Charge for images separately (future-proofing)
        if (config.ChargeForImageResults &&
            usage.Metadata?.TryGetValue("imageResults", out var imageCountObj) == true)
        {
            if (imageCountObj is int imageCount && imageCount > 0)
            {
                var imageCost = imageCount * (config.CostPerImage ?? 0m);
                totalCost += imageCost;
                _logger.LogDebug("Tavily image results: {Count} images × ${CostPerImage} = ${Cost}",
                    imageCount, config.CostPerImage ?? 0m, imageCost);
            }
        }

        _logger.LogDebug("Tavily cost breakdown: {Credits} credits × ${CostPerCredit} = ${TotalCost}",
            totalCredits, config.CostPerCredit, totalCost);

        return totalCost;
    }
}
