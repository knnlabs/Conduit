using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Models;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Per-token pricing model calculations for FunctionCostCalculationService.
/// </summary>
public partial class FunctionCostCalculationService
{
    /// <summary>
    /// Calculates cost using per-token pricing model.
    /// </summary>
    /// <param name="functionCost">The function cost configuration.</param>
    /// <param name="usage">The usage data containing token count.</param>
    /// <returns>The calculated cost based on tokens consumed.</returns>
    /// <remarks>
    /// Per-token pricing charges based on the number of tokens consumed:
    /// Cost = TokensConsumed × CostPerToken
    ///
    /// This model is ideal for:
    /// - Answer/LLM-based functions (e.g., Perplexity Answer API)
    /// - Functions that call LLMs internally
    /// - RAG systems with embedding generation
    ///
    /// If no tokens are consumed (TokensConsumed is null or 0), cost is 0.
    /// </remarks>
    private decimal CalculatePerTokenCost(FunctionCost functionCost, FunctionExecutionUsage usage)
    {
        if (!functionCost.CostPerToken.HasValue)
        {
            _logger.LogWarning("PerToken pricing model configured but CostPerToken is null for cost {CostName}. Returning 0.",
                functionCost.CostName);
            return 0m;
        }

        if (!usage.TokensConsumed.HasValue || usage.TokensConsumed.Value == 0)
        {
            _logger.LogDebug("No tokens consumed for per-token pricing. Returning 0 cost.");
            return 0m;
        }

        var cost = usage.TokensConsumed.Value * functionCost.CostPerToken.Value;

        _logger.LogDebug("Per-token cost calculated: {TokenCount} tokens × ${CostPerToken} = ${Cost}",
            usage.TokensConsumed.Value, functionCost.CostPerToken.Value, cost);

        return cost;
    }
}
