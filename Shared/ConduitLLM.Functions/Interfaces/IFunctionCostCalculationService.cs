using ConduitLLM.Functions.Models;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Service for calculating function execution costs based on usage data and pricing configuration.
/// </summary>
/// <remarks>
/// This service is analogous to ICostCalculationService for LLM operations.
/// It provides polymorphic cost calculation based on the FunctionPricingModel enum,
/// supporting multiple billing strategies (flat rate, per-result, tiered, hybrid, etc.).
/// </remarks>
public interface IFunctionCostCalculationService
{
    /// <summary>
    /// Calculates the cost of a function execution based on actual usage.
    /// </summary>
    /// <param name="functionConfigurationId">ID of the function configuration to calculate cost for.</param>
    /// <param name="usage">Actual usage data from the function execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Calculated cost in USD.</returns>
    /// <remarks>
    /// This method is called after function execution completes to determine the actual cost
    /// based on real usage data (result count, tokens consumed, content extracted, etc.).
    /// The cost is calculated using the active FunctionCost configuration for the given function.
    /// </remarks>
    Task<decimal> CalculateCostAsync(
        int functionConfigurationId,
        FunctionExecutionUsage usage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates the cost of a function execution before it runs.
    /// </summary>
    /// <param name="functionConfigurationId">ID of the function configuration to estimate cost for.</param>
    /// <param name="requestParameters">Request parameters that will be sent to the function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Estimated cost in USD.</returns>
    /// <remarks>
    /// This method is called before function execution to reserve balance from the virtual key.
    /// The estimation is typically conservative (slightly over-estimates) to ensure sufficient balance.
    /// The difference between estimated and actual cost is refunded after execution completes.
    ///
    /// Estimation logic varies by provider:
    /// - For Exa: Assumes worst case (neural search + all requested content extractions)
    /// - For Perplexity: Estimates based on typical token consumption
    /// - For RAG: Estimates based on document count and vector dimensions
    /// </remarks>
    Task<decimal> EstimateCostAsync(
        int functionConfigurationId,
        Dictionary<string, object> requestParameters,
        CancellationToken cancellationToken = default);
}
