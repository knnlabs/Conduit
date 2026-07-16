using ConduitLLM.Core.Models.Pricing;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Service for caching parsed pricing rules configurations.
/// Reduces JSON parsing overhead by caching deserialized <see cref="PricingRulesConfig"/> objects.
/// </summary>
public interface ICachedPricingRulesService
{
    /// <summary>
    /// Gets a cached pricing rules configuration for the specified model cost ID.
    /// </summary>
    /// <param name="modelCostId">The ID of the model cost entity.</param>
    /// <param name="pricingConfiguration">The JSON configuration string to parse if not cached.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed pricing rules configuration, or null if parsing fails.</returns>
    Task<PricingRulesConfig?> GetConfigAsync(int modelCostId, string pricingConfiguration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached configuration for a specific model cost.
    /// Should be called when the model cost's pricing configuration is updated.
    /// </summary>
    /// <param name="modelCostId">The ID of the model cost entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateCacheAsync(int modelCostId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cached pricing rules configurations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
