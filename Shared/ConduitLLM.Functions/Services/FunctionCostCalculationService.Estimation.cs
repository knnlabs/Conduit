using System.Text.Json;
using ConduitLLM.Functions.Models.Pricing;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Cost estimation logic for FunctionCostCalculationService.
/// </summary>
public partial class FunctionCostCalculationService
{
    /// <inheritdoc />
    /// <remarks>
    /// Cost estimation is conservative (slightly over-estimates) to ensure sufficient balance
    /// is reserved before execution. The difference between estimated and actual cost is refunded
    /// after execution completes.
    ///
    /// Estimation strategies by pricing model:
    /// - FlatRate: Use CostPerExecution directly (exact)
    /// - PerResult: Estimate based on requested result count
    /// - PerToken: Estimate based on historical averages or max expected tokens
    /// - TimeBased: Estimate based on typical execution duration
    /// - Tiered: Use highest tier rate for conservatism
    /// - Hybrid: Conservative estimate for all dimensions (e.g., assume neural + all extractions for Exa)
    /// </remarks>
    public async Task<decimal> EstimateCostAsync(
        int functionConfigurationId,
        Dictionary<string, object> requestParameters,
        CancellationToken cancellationToken = default)
    {
        if (functionConfigurationId <= 0)
        {
            _logger.LogWarning("Function configuration ID is invalid: {ConfigId}. Cannot estimate cost.", functionConfigurationId);
            return 0m;
        }

        if (requestParameters == null || !requestParameters.Any())
        {
            _logger.LogWarning("Request parameters are null/empty for function configuration {ConfigId}. Cannot estimate cost.", functionConfigurationId);
            return 0m;
        }

        var functionCost = await _functionCostService.GetCostForConfigurationAsync(functionConfigurationId, cancellationToken);

        if (functionCost == null)
        {
            _logger.LogWarning("Cost information not found for function configuration {ConfigId}. Returning 0 estimate.", functionConfigurationId);
            return 0m;
        }

        decimal estimatedCost = 0m;

        // Estimate based on pricing model
        switch (functionCost.PricingModel)
        {
            case Enums.FunctionPricingModel.FlatRate:
                estimatedCost = functionCost.CostPerExecution ?? 0m;
                break;

            case Enums.FunctionPricingModel.PerResult:
                estimatedCost = EstimatePerResultCost(functionCost, requestParameters);
                break;

            case Enums.FunctionPricingModel.PerToken:
                estimatedCost = EstimatePerTokenCost(functionCost, requestParameters);
                break;

            case Enums.FunctionPricingModel.TimeBased:
                estimatedCost = EstimateTimeBasedCost(functionCost, requestParameters);
                break;

            case Enums.FunctionPricingModel.Tiered:
                estimatedCost = EstimateTieredCost(functionCost, requestParameters);
                break;

            case Enums.FunctionPricingModel.Hybrid:
                estimatedCost = EstimateHybridCost(functionCost, requestParameters);
                break;

            default:
                _logger.LogWarning("Unknown pricing model {PricingModel} for function configuration {ConfigId}. Using flat rate fallback.",
                    functionCost.PricingModel, functionConfigurationId);
                estimatedCost = functionCost.CostPerExecution ?? 0m;
                break;
        }

        _logger.LogDebug("Estimated cost for function configuration {ConfigId} using pricing model {PricingModel} is {EstimatedCost}",
            functionConfigurationId, functionCost.PricingModel, estimatedCost);

        return estimatedCost;
    }

    /// <summary>
    /// Estimates cost for per-result pricing model.
    /// </summary>
    private decimal EstimatePerResultCost(Entities.FunctionCost functionCost, Dictionary<string, object> requestParameters)
    {
        if (!functionCost.CostPerResult.HasValue)
        {
            return 0m;
        }

        // Try to extract requested result count from parameters
        int requestedResults = 10; // Default assumption

        if (requestParameters.TryGetValue("numResults", out var numResultsObj))
        {
            requestedResults = Convert.ToInt32(numResultsObj);
        }
        else if (requestParameters.TryGetValue("num_results", out var numResultsSnake))
        {
            requestedResults = Convert.ToInt32(numResultsSnake);
        }
        else if (requestParameters.TryGetValue("limit", out var limitObj))
        {
            requestedResults = Convert.ToInt32(limitObj);
        }

        var estimate = requestedResults * functionCost.CostPerResult.Value;

        _logger.LogDebug("Estimated per-result cost: {RequestedResults} results × ${CostPerResult} = ${Estimate}",
            requestedResults, functionCost.CostPerResult.Value, estimate);

        return estimate;
    }

    /// <summary>
    /// Estimates cost for per-token pricing model.
    /// </summary>
    private decimal EstimatePerTokenCost(Entities.FunctionCost functionCost, Dictionary<string, object> requestParameters)
    {
        if (!functionCost.CostPerToken.HasValue)
        {
            return 0m;
        }

        // Conservative estimate: assume 1000 tokens for Answer functions
        // Can be refined based on query length or historical averages
        int estimatedTokens = 1000;

        var estimate = estimatedTokens * functionCost.CostPerToken.Value;

        _logger.LogDebug("Estimated per-token cost: {EstimatedTokens} tokens × ${CostPerToken} = ${Estimate}",
            estimatedTokens, functionCost.CostPerToken.Value, estimate);

        return estimate;
    }

    /// <summary>
    /// Estimates cost for time-based pricing model.
    /// </summary>
    private decimal EstimateTimeBasedCost(Entities.FunctionCost functionCost, Dictionary<string, object> requestParameters)
    {
        if (!functionCost.CostPerMinute.HasValue)
        {
            return 0m;
        }

        // Conservative estimate: assume 1 minute execution time
        // Can be refined based on function type or historical averages
        decimal estimatedMinutes = 1m;

        var estimate = estimatedMinutes * functionCost.CostPerMinute.Value;

        _logger.LogDebug("Estimated time-based cost: {EstimatedMinutes} minutes × ${CostPerMinute} = ${Estimate}",
            estimatedMinutes, functionCost.CostPerMinute.Value, estimate);

        return estimate;
    }

    /// <summary>
    /// Estimates cost for tiered pricing model.
    /// </summary>
    private decimal EstimateTieredCost(Entities.FunctionCost functionCost, Dictionary<string, object> requestParameters)
    {
        if (string.IsNullOrWhiteSpace(functionCost.TieredPricing))
        {
            return 0m;
        }

        TieredPricingConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<TieredPricingConfig>(functionCost.TieredPricing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse tiered pricing configuration for estimation");
            return 0m;
        }

        if (config == null || config.Tiers == null || !config.Tiers.Any())
        {
            return 0m;
        }

        // Extract requested count
        int requestedUnits = 10; // Default
        if (requestParameters.TryGetValue("numResults", out var numObj))
        {
            requestedUnits = Convert.ToInt32(numObj);
        }

        // Use the highest tier rate as conservative estimate
        var highestRate = config.Tiers.Max(t => t.CostPerUnit);
        var estimate = requestedUnits * highestRate;

        _logger.LogDebug("Estimated tiered cost (conservative): {RequestedUnits} units × ${HighestRate} = ${Estimate}",
            requestedUnits, highestRate, estimate);

        return estimate;
    }

    /// <summary>
    /// Estimates cost for hybrid pricing model.
    /// </summary>
    private decimal EstimateHybridCost(Entities.FunctionCost functionCost, Dictionary<string, object> requestParameters)
    {
        if (string.IsNullOrWhiteSpace(functionCost.PricingConfiguration))
        {
            return 0m;
        }

        // Try Exa format
        try
        {
            var exaConfig = JsonSerializer.Deserialize<ExaHybridPricingConfig>(functionCost.PricingConfiguration);
            if (exaConfig != null)
            {
                return EstimateExaHybridCost(exaConfig, requestParameters);
            }
        }
        catch (JsonException)
        {
            _logger.LogDebug("Failed to parse as Exa hybrid pricing config for estimation");
        }

        // Future: Add other hybrid estimation formats

        _logger.LogWarning("Could not estimate hybrid pricing cost. Unknown format.");
        return 0m;
    }

    /// <summary>
    /// Estimates cost for Exa.ai hybrid pricing.
    /// </summary>
    /// <remarks>
    /// Conservative estimation strategy:
    /// 1. Assume neural search (more expensive than keyword)
    /// 2. Check requested result count to determine tier
    /// 3. Assume ALL requested content extractions will succeed
    ///
    /// This ensures we reserve sufficient balance upfront.
    /// </remarks>
    private decimal EstimateExaHybridCost(ExaHybridPricingConfig config, Dictionary<string, object> requestParameters)
    {
        decimal totalEstimate = 0m;

        // Extract requested result count
        int requestedResults = 10; // Default
        if (requestParameters.TryGetValue("numResults", out var numObj))
        {
            requestedResults = Convert.ToInt32(numObj);
        }
        else if (requestParameters.TryGetValue("num_results", out var numSnake))
        {
            requestedResults = Convert.ToInt32(numSnake);
        }

        // 1. Estimate search cost (assume neural for conservatism)
        decimal searchEstimate;
        if (requestedResults <= (config.SearchCosts.Neural.Tier1.MaxResults ?? 25))
        {
            searchEstimate = config.SearchCosts.Neural.Tier1.Cost;
        }
        else
        {
            searchEstimate = config.SearchCosts.Neural.Tier2.Cost;
        }
        totalEstimate += searchEstimate;

        // 2. Estimate content extraction costs (assume all requested extractions succeed)
        bool textRequested = requestParameters.ContainsKey("text");
        bool highlightsRequested = requestParameters.ContainsKey("highlights");
        bool summaryRequested = requestParameters.ContainsKey("summary");

        if (textRequested)
        {
            var textEstimate = requestedResults * config.ContentExtractionCosts.Text;
            totalEstimate += textEstimate;
            _logger.LogDebug("Estimated text extraction: {Pages} pages × ${Cost} = ${Estimate}",
                requestedResults, config.ContentExtractionCosts.Text, textEstimate);
        }

        if (highlightsRequested)
        {
            var highlightsEstimate = requestedResults * config.ContentExtractionCosts.Highlights;
            totalEstimate += highlightsEstimate;
            _logger.LogDebug("Estimated highlights extraction: {Pages} pages × ${Cost} = ${Estimate}",
                requestedResults, config.ContentExtractionCosts.Highlights, highlightsEstimate);
        }

        if (summaryRequested)
        {
            var summaryEstimate = requestedResults * config.ContentExtractionCosts.Summary;
            totalEstimate += summaryEstimate;
            _logger.LogDebug("Estimated summary generation: {Pages} pages × ${Cost} = ${Estimate}",
                requestedResults, config.ContentExtractionCosts.Summary, summaryEstimate);
        }

        _logger.LogDebug("Estimated Exa hybrid cost (conservative): Search=${SearchEstimate}, Total=${TotalEstimate}",
            searchEstimate, totalEstimate);

        return totalEstimate;
    }
}
