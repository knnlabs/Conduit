using ConduitLLM.Core.Models;
using System.Threading;
using System.Threading.Tasks;

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
}
