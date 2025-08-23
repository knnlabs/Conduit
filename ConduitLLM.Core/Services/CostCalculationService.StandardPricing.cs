using System.Text.Json;

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
        decimal calculatedCost = 0m;

        // Calculate cost based on token usage
        // For embeddings: prioritize embedding cost when available and no completion tokens
        if (modelCost.EmbeddingCostPerMillionTokens.HasValue && usage.CompletionTokens.GetValueOrDefault() == 0 && usage.PromptTokens.HasValue)
        {
            // Use specialized embedding cost for prompt tokens (cost is per million tokens)
            calculatedCost += (usage.PromptTokens.Value * modelCost.EmbeddingCostPerMillionTokens.Value) / 1_000_000m;
        }
        else
        {
            // Calculate input token costs, accounting for cached tokens
            var regularInputTokens = usage.PromptTokens.GetValueOrDefault();
            
            // Handle cached input tokens (read from cache)
            if (usage.CachedInputTokens.HasValue && usage.CachedInputTokens.Value > 0 && modelCost.CachedInputCostPerMillionTokens.HasValue)
            {
                // Subtract cached tokens from regular input tokens
                regularInputTokens -= usage.CachedInputTokens.Value;
                
                // Add cost for cached tokens at the cached rate (cost is per million tokens)
                calculatedCost += (usage.CachedInputTokens.Value * modelCost.CachedInputCostPerMillionTokens.Value) / 1_000_000m;
                
                _logger.LogDebug("Applied cached input token pricing for {CachedTokens} tokens at rate {CachedRate}",
                    usage.CachedInputTokens.Value, modelCost.CachedInputCostPerMillionTokens.Value);
            }
            
            // Handle cache write tokens
            if (usage.CachedWriteTokens.HasValue && usage.CachedWriteTokens.Value > 0 && modelCost.CachedInputWriteCostPerMillionTokens.HasValue)
            {
                // Cache writes are additional to regular input processing (cost is per million tokens)
                calculatedCost += (usage.CachedWriteTokens.Value * modelCost.CachedInputWriteCostPerMillionTokens.Value) / 1_000_000m;
                
                _logger.LogDebug("Applied cache write token pricing for {WriteTokens} tokens at rate {WriteRate}",
                    usage.CachedWriteTokens.Value, modelCost.CachedInputWriteCostPerMillionTokens.Value);
            }
            
            // Add cost for remaining regular input tokens (cost is per million tokens)
            if (regularInputTokens > 0)
            {
                calculatedCost += (regularInputTokens * modelCost.InputCostPerMillionTokens) / 1_000_000m;
            }
        }
        
        // Always add completion token cost (cost is per million tokens)
        if (usage.CompletionTokens.HasValue)
        {
            calculatedCost += (usage.CompletionTokens.Value * modelCost.OutputCostPerMillionTokens) / 1_000_000m;
        }

        // Add image generation cost if applicable
        if (modelCost.ImageCostPerImage.HasValue && usage.ImageCount.HasValue)
        {
            var imageCost = usage.ImageCount.Value * modelCost.ImageCostPerImage.Value;
            
            // Apply quality multiplier if available
            if (!string.IsNullOrEmpty(modelCost.ImageQualityMultipliers) && 
                !string.IsNullOrEmpty(usage.ImageQuality))
            {
                try
                {
                    var qualityMultipliers = JsonSerializer.Deserialize<Dictionary<string, decimal>>(modelCost.ImageQualityMultipliers);
                    if (qualityMultipliers != null && qualityMultipliers.TryGetValue(usage.ImageQuality.ToLowerInvariant(), out var multiplier))
                    {
                        imageCost *= multiplier;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse ImageQualityMultipliers for model {ModelId}", modelId);
                }
            }
            
            calculatedCost += imageCost;
        }

        // Add video generation cost if applicable
        if (modelCost.VideoCostPerSecond.HasValue && usage.VideoDurationSeconds.HasValue)
        {
            var baseCost = (decimal)usage.VideoDurationSeconds.Value * modelCost.VideoCostPerSecond.Value;
            
            // Apply resolution multiplier if available
            if (!string.IsNullOrEmpty(modelCost.VideoResolutionMultipliers) && 
                !string.IsNullOrEmpty(usage.VideoResolution))
            {
                try
                {
                    var resolutionMultipliers = JsonSerializer.Deserialize<Dictionary<string, decimal>>(modelCost.VideoResolutionMultipliers);
                    if (resolutionMultipliers != null && resolutionMultipliers.TryGetValue(usage.VideoResolution, out var multiplier))
                    {
                        baseCost *= multiplier;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse VideoResolutionMultipliers for model {ModelId}", modelId);
                }
            }
            
            calculatedCost += baseCost;
        }

        // Add search unit cost if applicable
        if (usage.SearchUnits.HasValue && usage.SearchUnits.Value > 0 && modelCost.CostPerSearchUnit.HasValue)
        {
            // Convert from per-1K-units to per-unit
            var costPerUnit = modelCost.CostPerSearchUnit.Value / 1000m;
            var searchCost = usage.SearchUnits.Value * costPerUnit;
            calculatedCost += searchCost;
            
            _logger.LogDebug(
                "Search cost calculation for model {ModelId}: {Units} units × ${CostPerUnit} = ${Total}",
                modelId,
                usage.SearchUnits.Value,
                costPerUnit,
                searchCost);
        }

        // Add inference step cost if applicable (for image generation)
        if (usage.InferenceSteps.HasValue && usage.InferenceSteps.Value > 0 && modelCost.CostPerInferenceStep.HasValue)
        {
            var stepCost = usage.InferenceSteps.Value * modelCost.CostPerInferenceStep.Value;
            calculatedCost += stepCost;
            
            _logger.LogDebug(
                "Inference step cost calculation for model {ModelId}: {Steps} steps × ${CostPerStep} = ${Total}",
                modelId,
                usage.InferenceSteps.Value,
                modelCost.CostPerInferenceStep.Value,
                stepCost);
        }

        // Batch processing discount is now applied in the main CalculateCostAsync method for all pricing models

        _logger.LogDebug("Calculated cost for model {ModelId} with usage (Prompt: {PromptTokens}, Completion: {CompletionTokens}, CachedInput: {CachedInputTokens}, CachedWrite: {CachedWriteTokens}, Images: {ImageCount}, Video: {VideoDuration}s, SearchUnits: {SearchUnits}, InferenceSteps: {InferenceSteps}, IsBatch: {IsBatch}) is {CalculatedCost}",
            modelId, usage.PromptTokens, usage.CompletionTokens, usage.CachedInputTokens ?? 0, usage.CachedWriteTokens ?? 0, usage.ImageCount ?? 0, usage.VideoDurationSeconds ?? 0, usage.SearchUnits ?? 0, usage.InferenceSteps ?? 0, usage.IsBatch ?? false, calculatedCost);

        return Task.FromResult(calculatedCost);
    }
}