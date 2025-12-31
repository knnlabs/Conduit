using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Models;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Flat rate pricing model calculations for FunctionCostCalculationService.
/// </summary>
public partial class FunctionCostCalculationService
{
    /// <summary>
    /// Calculates cost using flat rate pricing model.
    /// </summary>
    /// <param name="functionCost">The function cost configuration.</param>
    /// <param name="usage">The usage data (not used for flat rate, but included for consistency).</param>
    /// <returns>The flat rate cost.</returns>
    /// <remarks>
    /// Flat rate pricing charges a fixed amount per execution, regardless of:
    /// - Number of results returned
    /// - Execution duration
    /// - Tokens consumed
    ///
    /// This is the simplest pricing model and is useful for:
    /// - Predictable budgeting
    /// - Simple integrations
    /// - Services with consistent resource consumption
    /// </remarks>
    private decimal CalculateFlatRateCost(FunctionCost functionCost, FunctionExecutionUsage usage)
    {
        if (!functionCost.CostPerExecution.HasValue)
        {
            _logger.LogWarning("FlatRate pricing model configured but CostPerExecution is null for cost {CostName}. Returning 0.",
                functionCost.CostName);
            return 0m;
        }

        var cost = functionCost.CostPerExecution.Value;

        _logger.LogDebug("Flat rate cost calculated: {Cost} for function cost {CostName}",
            cost, functionCost.CostName);

        return cost;
    }
}
