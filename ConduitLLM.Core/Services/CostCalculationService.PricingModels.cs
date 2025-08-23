using System.Text.Json;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Pricing;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service implementation for various pricing model cost calculations
/// </summary>
public partial class CostCalculationService
{
    private Task<decimal> CalculatePerVideoCostAsync(string modelId, ModelCost modelCost, Usage usage)
    {
        if (!usage.VideoDurationSeconds.HasValue || string.IsNullOrEmpty(usage.VideoResolution))
        {
            _logger.LogDebug("No video usage data for per-video pricing model {ModelId}", modelId);
            return Task.FromResult(0m);
        }

        // Parse configuration from JSON
        PerVideoPricingConfig? config = null;
        if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
        {
            try
            {
                config = JsonSerializer.Deserialize<PerVideoPricingConfig>(modelCost.PricingConfiguration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse per-video pricing configuration for model {ModelId}", modelId);
                throw new InvalidOperationException($"Invalid per-video pricing configuration for model {modelId}");
            }
        }

        if (config == null || config.Rates == null || config.Rates.Count() == 0)
        {
            _logger.LogError("No per-video pricing rates configured for model {ModelId}", modelId);
            throw new InvalidOperationException($"No per-video pricing rates configured for model {modelId}");
        }

        // Build lookup key (e.g., "720p_6" for 720p resolution, 6 seconds)
        var duration = (int)Math.Round(usage.VideoDurationSeconds.Value);
        var lookupKey = $"{usage.VideoResolution}_{duration}";

        if (!config.Rates.TryGetValue(lookupKey, out var flatRate))
        {
            _logger.LogError("No pricing found for video {Resolution} {Duration}s for model {ModelId}", 
                usage.VideoResolution, duration, modelId);
            throw new InvalidOperationException($"No pricing available for {usage.VideoResolution} {duration}s video on model {modelId}");
        }

        _logger.LogDebug("Per-video cost for model {ModelId}: {Resolution} {Duration}s = ${Cost}",
            modelId, usage.VideoResolution, duration, flatRate);

        return Task.FromResult(flatRate);
    }

    private Task<decimal> CalculatePerSecondVideoCostAsync(string modelId, ModelCost modelCost, Usage usage)
    {
        if (!usage.VideoDurationSeconds.HasValue)
        {
            _logger.LogDebug("No video duration for per-second video pricing model {ModelId}", modelId);
            return Task.FromResult(0m);
        }

        // Parse configuration from JSON
        PerSecondVideoPricingConfig? config = null;
        if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
        {
            try
            {
                config = JsonSerializer.Deserialize<PerSecondVideoPricingConfig>(modelCost.PricingConfiguration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse per-second video pricing configuration for model {ModelId}", modelId);
                throw new InvalidOperationException($"Invalid per-second video pricing configuration for model {modelId}");
            }
        }

        if (config == null)
        {
            _logger.LogError("No per-second video pricing configuration for model {ModelId}", modelId);
            throw new InvalidOperationException($"No per-second video pricing configuration for model {modelId}");
        }

        var baseCost = (decimal)usage.VideoDurationSeconds.Value * config.BaseRate;

        // Apply resolution multiplier if available
        if (!string.IsNullOrEmpty(usage.VideoResolution) && 
            config.ResolutionMultipliers != null &&
            config.ResolutionMultipliers.TryGetValue(usage.VideoResolution, out var multiplier))
        {
            baseCost *= multiplier;
            _logger.LogDebug("Applied video resolution multiplier {Multiplier} for {Resolution}", multiplier, usage.VideoResolution);
        }

        _logger.LogDebug("Per-second video cost for model {ModelId}: {Duration}s × ${BaseRate} = ${Cost}",
            modelId, usage.VideoDurationSeconds.Value, config.BaseRate, baseCost);

        return Task.FromResult(baseCost);
    }

    private Task<decimal> CalculateInferenceStepsCostAsync(string modelId, ModelCost modelCost, Usage usage)
    {
        // Parse configuration or use pre-parsed
        InferenceStepsPricingConfig? config = null;
        if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
        {
            try
            {
                config = JsonSerializer.Deserialize<InferenceStepsPricingConfig>(modelCost.PricingConfiguration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse inference steps pricing configuration for model {ModelId}", modelId);
                throw new InvalidOperationException($"Invalid inference steps pricing configuration for model {modelId}");
            }
        }

        if (config == null)
        {
            _logger.LogError("No inference steps pricing configuration for model {ModelId}", modelId);
            throw new InvalidOperationException($"No inference steps pricing configuration for model {modelId}");
        }

        // Use provided steps or default
        var steps = usage.InferenceSteps ?? config.DefaultSteps;
        if (steps <= 0)
        {
            _logger.LogDebug("No inference steps for model {ModelId}", modelId);
            return Task.FromResult(0m);
        }

        var cost = steps * config.CostPerStep;

        _logger.LogDebug("Inference steps cost for model {ModelId}: {Steps} steps × ${CostPerStep} = ${Cost}",
            modelId, steps, config.CostPerStep, cost);

        return Task.FromResult(cost);
    }

    private Task<decimal> CalculateTieredTokensCostAsync(string modelId, ModelCost modelCost, Usage usage)
    {
        // Parse configuration or use pre-parsed
        TieredTokensPricingConfig? config = null;
        if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
        {
            try
            {
                config = JsonSerializer.Deserialize<TieredTokensPricingConfig>(modelCost.PricingConfiguration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse tiered tokens pricing configuration for model {ModelId}", modelId);
                throw new InvalidOperationException($"Invalid tiered tokens pricing configuration for model {modelId}");
            }
        }

        if (config == null || config.Tiers == null || config.Tiers.Count() == 0)
        {
            _logger.LogError("No tiered tokens pricing configuration for model {ModelId}", modelId);
            throw new InvalidOperationException($"No tiered tokens pricing configuration for model {modelId}");
        }

        var inputTokens = usage.PromptTokens ?? 0;
        var outputTokens = usage.CompletionTokens ?? 0;

        // Find the appropriate tier based on total context length
        var totalTokens = inputTokens + outputTokens;
        TokenPricingTier? tier = null;

        foreach (var t in config.Tiers.OrderBy(t => t.MaxContext ?? int.MaxValue))
        {
            if (!t.MaxContext.HasValue || totalTokens <= t.MaxContext.Value)
            {
                tier = t;
                break;
            }
        }

        if (tier == null)
        {
            tier = config.Tiers.Last(); // Use highest tier if none match
        }

        var inputCost = (inputTokens * tier.InputCost) / 1_000_000m;
        var outputCost = (outputTokens * tier.OutputCost) / 1_000_000m;

        _logger.LogDebug("Tiered tokens cost for model {ModelId}: Context {TotalTokens}, Tier ≤{MaxContext}, " +
            "Input: {InputTokens} × ${InputRate} + Output: {OutputTokens} × ${OutputRate} = ${TotalCost}",
            modelId, totalTokens, tier.MaxContext, inputTokens, tier.InputCost, outputTokens, tier.OutputCost, inputCost + outputCost);

        return Task.FromResult(inputCost + outputCost);
    }

    private Task<decimal> CalculatePerImageCostAsync(string modelId, ModelCost modelCost, Usage usage)
    {
        if (!usage.ImageCount.HasValue || usage.ImageCount.Value <= 0)
        {
            _logger.LogDebug("No image count for per-image pricing model {ModelId}", modelId);
            return Task.FromResult(0m);
        }

        // Parse configuration or use pre-parsed
        PerImagePricingConfig? config = null;
        if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
        {
            try
            {
                config = JsonSerializer.Deserialize<PerImagePricingConfig>(modelCost.PricingConfiguration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse per-image pricing configuration for model {ModelId}", modelId);
                throw new InvalidOperationException($"Invalid per-image pricing configuration for model {modelId}");
            }
        }

        if (config == null)
        {
            _logger.LogError("No per-image pricing configuration for model {ModelId}", modelId);
            throw new InvalidOperationException($"No per-image pricing configuration for model {modelId}");
        }

        var cost = usage.ImageCount.Value * config.BaseRate;

        // Apply quality multiplier
        if (!string.IsNullOrEmpty(usage.ImageQuality) && 
            config.QualityMultipliers != null &&
            config.QualityMultipliers.TryGetValue(usage.ImageQuality.ToLowerInvariant(), out var qualityMultiplier))
        {
            cost *= qualityMultiplier;
            _logger.LogDebug("Applied image quality multiplier {Multiplier} for {Quality}", qualityMultiplier, usage.ImageQuality);
        }

        // Apply resolution multiplier
        if (!string.IsNullOrEmpty(usage.ImageResolution) && 
            config.ResolutionMultipliers != null &&
            config.ResolutionMultipliers.TryGetValue(usage.ImageResolution, out var resolutionMultiplier))
        {
            cost *= resolutionMultiplier;
            _logger.LogDebug("Applied image resolution multiplier {Multiplier} for {Resolution}", resolutionMultiplier, usage.ImageResolution);
        }

        _logger.LogDebug("Per-image cost for model {ModelId}: {Count} images × ${BaseRate} = ${Cost}",
            modelId, usage.ImageCount.Value, config.BaseRate, cost);

        return Task.FromResult(cost);
    }

    private async Task<decimal> CalculatePerMinuteAudioCostAsync(string modelId, ModelCost modelCost, Usage usage)
    {
        // This pricing model is for audio transcription/realtime
        // Delegate to standard calculation which already handles audio costs
        return await CalculateStandardCostAsync(modelId, modelCost, usage);
    }

    private async Task<decimal> CalculatePerThousandCharactersCostAsync(string modelId, ModelCost modelCost, Usage usage)
    {
        // This pricing model is for text-to-speech
        // The standard calculation already handles AudioCostPerKCharacters
        return await CalculateStandardCostAsync(modelId, modelCost, usage);
    }
}