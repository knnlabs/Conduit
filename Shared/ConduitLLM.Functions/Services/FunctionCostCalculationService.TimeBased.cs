using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Models;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Time-based pricing model calculations for FunctionCostCalculationService.
/// </summary>
public partial class FunctionCostCalculationService
{
    /// <summary>
    /// Calculates cost using time-based pricing model.
    /// </summary>
    /// <param name="functionCost">The function cost configuration.</param>
    /// <param name="usage">The usage data containing execution duration.</param>
    /// <returns>The calculated cost based on execution time.</returns>
    /// <remarks>
    /// Time-based pricing charges based on execution duration:
    /// Cost = ExecutionMinutes × CostPerMinute
    ///
    /// This model is ideal for:
    /// - Long-running computations
    /// - Video/audio processing
    /// - Complex data transformations
    ///
    /// If execution duration is not available, cost is 0.
    /// Duration is converted to minutes for calculation (fractions rounded up).
    /// </remarks>
    private decimal CalculateTimeBasedCost(FunctionCost functionCost, FunctionExecutionUsage usage)
    {
        if (!functionCost.CostPerMinute.HasValue)
        {
            _logger.LogWarning("TimeBased pricing model configured but CostPerMinute is null for cost {CostName}. Returning 0.",
                functionCost.CostName);
            return 0m;
        }

        if (!usage.ExecutionDuration.HasValue || usage.ExecutionDuration.Value == TimeSpan.Zero)
        {
            _logger.LogDebug("No execution duration for time-based pricing. Returning 0 cost.");
            return 0m;
        }

        // Convert duration to minutes (round up to nearest minute)
        var durationMinutes = (decimal)Math.Ceiling(usage.ExecutionDuration.Value.TotalMinutes);

        var cost = durationMinutes * functionCost.CostPerMinute.Value;

        _logger.LogDebug("Time-based cost calculated: {Duration:F2} minutes × ${CostPerMinute} = ${Cost}",
            durationMinutes, functionCost.CostPerMinute.Value, cost);

        return cost;
    }
}
