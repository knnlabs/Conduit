using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service implementation for standard pricing model cost calculations
/// </summary>
public partial class CostCalculationService
{
    private Task<decimal> CalculateStandardCostAsync(string modelId, ModelCost modelCost, Usage usage)
    {
        var calculatedCost = ApplyTokenPricing(modelId, usage, TokenPricingRates.FromModelCost(modelCost)).Total;

        // Image and video generation costs are handled via RulesBased pricing configuration
        // (PricingModel.PerImage, PricingModel.PerVideo, or PricingModel.PerSecondVideo).
        // Inference step costs use PricingModel.InferenceSteps.
        // Batch processing discount is applied in the main CalculateCostAsync method for all pricing models.

        _logger.LogDebug("Calculated cost for model {ModelId} with usage (Prompt: {PromptTokens}, Completion: {CompletionTokens}, CachedInput: {CachedInputTokens}, CachedWrite: {CachedWriteTokens}, Images: {ImageCount}, Video: {VideoDuration}s, SearchUnits: {SearchUnits}, InferenceSteps: {InferenceSteps}, IsBatch: {IsBatch}) is {CalculatedCost}",
            modelId, usage.PromptTokens, usage.CompletionTokens, usage.CachedInputTokens ?? 0, usage.CachedWriteTokens ?? 0, usage.ImageCount ?? 0, usage.VideoDurationSeconds ?? 0, usage.SearchUnits ?? 0, usage.InferenceSteps ?? 0, usage.IsBatch ?? false, calculatedCost);

        return Task.FromResult(calculatedCost);
    }
}
