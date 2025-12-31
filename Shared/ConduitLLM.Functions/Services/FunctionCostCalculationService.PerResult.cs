using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Models;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Per-result pricing model calculations for FunctionCostCalculationService.
/// </summary>
public partial class FunctionCostCalculationService
{
    /// <summary>
    /// Calculates cost using per-result pricing model.
    /// </summary>
    /// <param name="functionCost">The function cost configuration.</param>
    /// <param name="usage">The usage data containing result count.</param>
    /// <returns>The calculated cost based on number of results.</returns>
    /// <remarks>
    /// Per-result pricing charges based on the number of results returned:
    /// Cost = ResultCount × CostPerResult
    ///
    /// This model is ideal for:
    /// - Search APIs (charge per search result)
    /// - RAG queries (charge per document retrieved)
    /// - Data enrichment services (charge per record processed)
    ///
    /// If no results are returned (ResultCount is null or 0), cost is 0.
    /// </remarks>
    private decimal CalculatePerResultCost(FunctionCost functionCost, FunctionExecutionUsage usage)
    {
        if (!functionCost.CostPerResult.HasValue)
        {
            _logger.LogWarning("PerResult pricing model configured but CostPerResult is null for cost {CostName}. Returning 0.",
                functionCost.CostName);
            return 0m;
        }

        if (!usage.ResultCount.HasValue || usage.ResultCount.Value == 0)
        {
            _logger.LogDebug("No results returned for per-result pricing. Returning 0 cost.");
            return 0m;
        }

        var cost = usage.ResultCount.Value * functionCost.CostPerResult.Value;

        _logger.LogDebug("Per-result cost calculated: {ResultCount} results × ${CostPerResult} = ${Cost}",
            usage.ResultCount.Value, functionCost.CostPerResult.Value, cost);

        return cost;
    }
}
