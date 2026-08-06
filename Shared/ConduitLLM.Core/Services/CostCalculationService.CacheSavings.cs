using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Prompt cache savings calculation for the CostCalculationService.
/// </summary>
public partial class CostCalculationService
{
    /// <inheritdoc />
    public async Task<decimal> CalculateCacheSavingsAsync(string modelId, Usage usage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(modelId) || usage == null)
            return 0m;

        if (!HasCachedTokens(usage))
            return 0m;

        var modelCost = await _modelCostService.GetCostForModelAsync(modelId, cancellationToken);
        return CalculateSavingsFromModelCost(modelCost, usage);
    }

    /// <inheritdoc />
    public async Task<decimal> CalculateCacheSavingsByIdAsync(int modelCostId, Usage usage, CancellationToken cancellationToken = default)
    {
        if (usage == null)
            return 0m;

        if (!HasCachedTokens(usage))
            return 0m;

        var modelCost = await _modelCostService.GetCostByIdAsync(modelCostId, cancellationToken);
        return CalculateSavingsFromModelCost(modelCost, usage);
    }

    public async Task<decimal> CalculateCacheWritePremiumAsync(string modelId, Usage usage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(modelId) || usage.CachedWriteTokens is not > 0) return 0m;
        return CalculateWritePremium(await _modelCostService.GetCostForModelAsync(modelId, cancellationToken), usage);
    }

    public async Task<decimal> CalculateCacheWritePremiumByIdAsync(int modelCostId, Usage usage, CancellationToken cancellationToken = default)
    {
        if (usage.CachedWriteTokens is not > 0) return 0m;
        return CalculateWritePremium(await _modelCostService.GetCostByIdAsync(modelCostId, cancellationToken), usage);
    }

    private static bool HasCachedTokens(Usage usage)
        => usage.CachedInputTokens.HasValue && usage.CachedInputTokens.Value > 0;

    private decimal CalculateSavingsFromModelCost(ModelCost? modelCost, Usage usage)
    {
        if (modelCost == null)
            return 0m;

        // Savings from cached reads: tokens that were charged at cached rate instead of full rate
        decimal savings = 0m;

        if (usage.CachedInputTokens.HasValue && usage.CachedInputTokens.Value > 0
            && modelCost.CachedInputCostPerMillionTokens.HasValue)
        {
            var fullCost = usage.CachedInputTokens.Value * modelCost.InputCostPerMillionTokens / 1_000_000m;
            var cachedCost = usage.CachedInputTokens.Value * modelCost.CachedInputCostPerMillionTokens.Value / 1_000_000m;
            savings = fullCost - cachedCost;
        }

        if (savings > 0)
        {
            _logger.LogDebug("Prompt caching savings for model: {CachedTokens} cached tokens saved ${Savings:F6}",
                usage.CachedInputTokens, savings);
        }

        return Math.Max(0m, savings);
    }

    private static decimal CalculateWritePremium(ModelCost? modelCost, Usage usage)
    {
        if (modelCost?.CachedInputWriteCostPerMillionTokens is not decimal writeRate ||
            usage.CachedWriteTokens is not > 0) return 0m;
        var premiumRate = Math.Max(0m, writeRate - modelCost.InputCostPerMillionTokens);
        return usage.CachedWriteTokens.Value * premiumRate / 1_000_000m;
    }
}
