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
                // OpenAI includes cached tokens in prompt_tokens; Anthropic input_tokens excludes them.
                if (usage.CachedInputTokensIncludedInPrompt)
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

        // Add reasoning token cost if applicable (cost is per million tokens)
        if (usage.ReasoningTokens.HasValue && usage.ReasoningTokens.Value > 0)
        {
            // Use specific reasoning rate if available, otherwise fall back to output rate
            var reasoningRate = modelCost.ReasoningCostPerMillionTokens ?? modelCost.OutputCostPerMillionTokens;
            calculatedCost += (usage.ReasoningTokens.Value * reasoningRate) / 1_000_000m;
            
            _logger.LogDebug("Applied reasoning token pricing for {ReasoningTokens} tokens at rate {ReasoningRate}",
                usage.ReasoningTokens.Value, reasoningRate);
        }

        // Image and video generation costs are now handled via RulesBased pricing configuration
        // Use PricingModel.PerImage, PricingModel.PerVideo, or PricingModel.PerSecondVideo instead

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

        // Add audio transcription (speech-to-text) cost, billed per minute of audio.
        if (usage.AudioDurationSeconds is > 0 && modelCost.AudioCostPerMinute.HasValue)
        {
            var audioCost = ((decimal)usage.AudioDurationSeconds.Value / 60m) * modelCost.AudioCostPerMinute.Value;
            calculatedCost += audioCost;

            _logger.LogDebug(
                "Audio transcription cost for model {ModelId}: {Seconds}s = ${Total}",
                modelId, usage.AudioDurationSeconds.Value, audioCost);
        }

        // Add text-to-speech cost, billed per thousand input characters.
        if (usage.TtsCharacters is > 0 && modelCost.AudioCostPerThousandCharacters.HasValue)
        {
            var ttsCost = (usage.TtsCharacters.Value / 1000m) * modelCost.AudioCostPerThousandCharacters.Value;
            calculatedCost += ttsCost;

            _logger.LogDebug(
                "Text-to-speech cost for model {ModelId}: {Chars} chars = ${Total}",
                modelId, usage.TtsCharacters.Value, ttsCost);
        }

        // Inference step costs are now handled via RulesBased pricing configuration
        // Use PricingModel.InferenceSteps instead

        // Batch processing discount is now applied in the main CalculateCostAsync method for all pricing models

        _logger.LogDebug("Calculated cost for model {ModelId} with usage (Prompt: {PromptTokens}, Completion: {CompletionTokens}, CachedInput: {CachedInputTokens}, CachedWrite: {CachedWriteTokens}, Images: {ImageCount}, Video: {VideoDuration}s, SearchUnits: {SearchUnits}, InferenceSteps: {InferenceSteps}, IsBatch: {IsBatch}) is {CalculatedCost}",
            modelId, usage.PromptTokens, usage.CompletionTokens, usage.CachedInputTokens ?? 0, usage.CachedWriteTokens ?? 0, usage.ImageCount ?? 0, usage.VideoDurationSeconds ?? 0, usage.SearchUnits ?? 0, usage.InferenceSteps ?? 0, usage.IsBatch ?? false, calculatedCost);

        return Task.FromResult(calculatedCost);
    }
}
