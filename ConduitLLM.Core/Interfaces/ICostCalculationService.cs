using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Service for calculating the cost of LLM operations based on usage and model information.
/// </summary>
public interface ICostCalculationService
{
    /// <summary>
    /// Calculates the estimated cost of an LLM operation based on usage and model ID.
    /// </summary>
    /// <param name="modelId">The specific model ID used (e.g., "openai/gpt-4o", "anthropic.claude-3-sonnet-20240229-v1:0").</param>
    /// <param name="usage">The usage data returned by the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calculated cost as a decimal, or 0 if cost cannot be determined.</returns>
    Task<decimal> CalculateCostAsync(string modelId, Usage usage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates a refund for a previous LLM operation.
    /// </summary>
    /// <param name="modelId">The specific model ID used in the original operation.</param>
    /// <param name="originalUsage">The original usage data that was charged.</param>
    /// <param name="refundUsage">The usage data to be refunded.</param>
    /// <param name="refundReason">The reason for the refund.</param>
    /// <param name="originalTransactionId">Optional original transaction ID for audit trail.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A RefundResult containing the refund details and any validation messages.</returns>
    Task<RefundResult> CalculateRefundAsync(
        string modelId,
        Usage originalUsage,
        Usage refundUsage,
        string refundReason,
        string? originalTransactionId = null,
        CancellationToken cancellationToken = default);
}
