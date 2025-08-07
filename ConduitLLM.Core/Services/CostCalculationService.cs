using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Pricing;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service implementation that calculates the cost of LLM operations based on usage data and model pricing.
/// </summary>
/// <remarks>
/// <para>
/// The CostCalculationService provides functionality to calculate the monetary cost of LLM operations
/// by combining usage data (tokens, images) with pricing information from the model cost repository.
/// </para>
/// <para>
/// This service supports cost calculation for different types of operations:
/// </para>
/// <list type="bullet">
///   <item><description>Text generation (prompt and completion tokens)</description></item>
///   <item><description>Embeddings (vector representations)</description></item>
///   <item><description>Image generation</description></item>
/// </list>
/// <para>
/// Cost calculation is an essential component for budget management, usage tracking,
/// and providing accurate billing information to users of the system.
/// </para>
/// </remarks>
public class CostCalculationService : ICostCalculationService
{
    private readonly IModelCostService _modelCostService;
    private readonly ILogger<CostCalculationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CostCalculationService"/> class.
    /// </summary>
    /// <param name="modelCostService">The service for retrieving model cost information.</param>
    /// <param name="logger">The logger for recording diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when modelCostService or logger is null.</exception>
    public CostCalculationService(IModelCostService modelCostService, ILogger<CostCalculationService> logger)
    {
        _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// This implementation performs cost calculation using the following logic:
    /// </para>
    /// <list type="number">
    ///   <item><description>Retrieves the cost information for the specified model</description></item>
    ///   <item><description>Validates input parameters and handles edge cases</description></item>
    ///   <item><description>Determines the operation type (text generation, embedding, or image generation)</description></item>
    ///   <item><description>Applies the appropriate pricing formula based on the operation type</description></item>
    /// </list>
    /// <para>
    /// The service uses different calculation strategies depending on the operation type:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>For text generation: (promptTokens * inputTokenCost) + (completionTokens * outputTokenCost)</description></item>
    ///   <item><description>For embeddings: promptTokens * embeddingTokenCost</description></item>
    ///   <item><description>For image generation: imageCount * imageGenerationCost</description></item>
    /// </list>
    /// <para>
    /// If cost information is not found for the specified model, the method returns 0.
    /// </para>
    /// </remarks>
    public async Task<decimal> CalculateCostAsync(string modelId, Usage usage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            _logger.LogWarning("Model ID is null or empty. Cannot calculate cost.");
            return 0m;
        }

        if (usage == null)
        {
            _logger.LogWarning("Usage data is null for model {ModelId}. Cannot calculate cost.", modelId);
            return 0m;
        }

        var modelCost = await _modelCostService.GetCostForModelAsync(modelId, cancellationToken);

        if (modelCost == null)
        {
            _logger.LogWarning("Cost information not found for model {ModelId}. Returning 0 cost.", modelId);
            return 0m;
        }

        decimal calculatedCost = 0m;

        // Handle polymorphic pricing models
        switch (modelCost.PricingModel)
        {
            case PricingModel.Standard:
                calculatedCost = await CalculateStandardCostAsync(modelId, modelCost, usage);
                break;
            case PricingModel.PerVideo:
                calculatedCost = await CalculatePerVideoCostAsync(modelId, modelCost, usage);
                break;
            case PricingModel.PerSecondVideo:
                calculatedCost = await CalculatePerSecondVideoCostAsync(modelId, modelCost, usage);
                break;
            case PricingModel.InferenceSteps:
                calculatedCost = await CalculateInferenceStepsCostAsync(modelId, modelCost, usage);
                break;
            case PricingModel.TieredTokens:
                calculatedCost = await CalculateTieredTokensCostAsync(modelId, modelCost, usage);
                break;
            case PricingModel.PerImage:
                calculatedCost = await CalculatePerImageCostAsync(modelId, modelCost, usage);
                break;
            case PricingModel.PerMinuteAudio:
                calculatedCost = await CalculatePerMinuteAudioCostAsync(modelId, modelCost, usage);
                break;
            case PricingModel.PerThousandCharacters:
                calculatedCost = await CalculatePerThousandCharactersCostAsync(modelId, modelCost, usage);
                break;
            default:
                _logger.LogWarning("Unknown pricing model {PricingModel} for model {ModelId}. Using standard calculation.", modelCost.PricingModel, modelId);
                calculatedCost = await CalculateStandardCostAsync(modelId, modelCost, usage);
                break;
        }

        // Apply batch processing discount if applicable (works across all pricing models)
        if (usage.IsBatch == true && modelCost.SupportsBatchProcessing && modelCost.BatchProcessingMultiplier.HasValue)
        {
            var originalCost = calculatedCost;
            calculatedCost *= modelCost.BatchProcessingMultiplier!.Value;
            _logger.LogDebug("Applied batch processing discount for model {ModelId}. Original cost: {OriginalCost}, Discounted cost: {DiscountedCost}, Multiplier: {Multiplier}",
                modelId, originalCost, calculatedCost, modelCost.BatchProcessingMultiplier.Value);
        }

        _logger.LogDebug("Calculated cost for model {ModelId} using pricing model {PricingModel} is {CalculatedCost}",
            modelId, modelCost.PricingModel, calculatedCost);

        return calculatedCost;
    }

    private async Task<decimal> CalculateStandardCostAsync(string modelId, ModelCostInfo modelCost, Usage usage)
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
            if (modelCost.ImageQualityMultipliers != null && 
                !string.IsNullOrEmpty(usage.ImageQuality) &&
                modelCost.ImageQualityMultipliers.TryGetValue(usage.ImageQuality.ToLowerInvariant(), out var multiplier))
            {
                imageCost *= multiplier;
            }
            
            calculatedCost += imageCost;
        }

        // Add video generation cost if applicable
        if (modelCost.VideoCostPerSecond.HasValue && usage.VideoDurationSeconds.HasValue)
        {
            var baseCost = (decimal)usage.VideoDurationSeconds.Value * modelCost.VideoCostPerSecond.Value;
            
            // Apply resolution multiplier if available
            if (modelCost.VideoResolutionMultipliers != null && 
                !string.IsNullOrEmpty(usage.VideoResolution) &&
                modelCost.VideoResolutionMultipliers.TryGetValue(usage.VideoResolution, out var multiplier))
            {
                baseCost *= multiplier;
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

        return calculatedCost;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// This implementation calculates refunds using the following logic:
    /// </para>
    /// <list type="number">
    ///   <item><description>Validates the refund request parameters</description></item>
    ///   <item><description>Ensures refund amounts don't exceed original amounts</description></item>
    ///   <item><description>Calculates refund based on the same pricing logic as charges</description></item>
    ///   <item><description>Returns a detailed refund result with breakdown</description></item>
    /// </list>
    /// <para>
    /// The method enforces validation rules to ensure data integrity:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Refund amounts cannot exceed original amounts</description></item>
    ///   <item><description>All usage values must be non-negative</description></item>
    ///   <item><description>Partial refunds are allowed and tracked</description></item>
    /// </list>
    /// </remarks>
    public async Task<RefundResult> CalculateRefundAsync(
        string modelId,
        Usage originalUsage,
        Usage refundUsage,
        string refundReason,
        string? originalTransactionId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new RefundResult
        {
            ModelId = modelId,
            OriginalUsage = originalUsage,
            RefundUsage = refundUsage,
            RefundReason = refundReason,
            OriginalTransactionId = originalTransactionId,
            RefundedAt = DateTime.UtcNow
        };

        // Validate inputs
        if (string.IsNullOrEmpty(modelId))
        {
            result.ValidationMessages.Add("Model ID is required for refund calculation.");
            return result;
        }

        if (originalUsage == null || refundUsage == null)
        {
            result.ValidationMessages.Add("Both original and refund usage data are required.");
            return result;
        }

        if (string.IsNullOrEmpty(refundReason))
        {
            result.ValidationMessages.Add("Refund reason is required.");
            return result;
        }

        // Validate refund amounts don't exceed original amounts
        var validationMessages = ValidateRefundAmounts(originalUsage, refundUsage);
        if (validationMessages.Any())
        {
            result.ValidationMessages.AddRange(validationMessages);
            result.IsPartialRefund = true;
        }

        // Get model cost information
        var modelCost = await _modelCostService.GetCostForModelAsync(modelId, cancellationToken);
        if (modelCost == null)
        {
            _logger.LogWarning("Cost information not found for model {ModelId} during refund calculation.", modelId);
            result.ValidationMessages.Add($"Cost information not found for model {modelId}.");
            return result;
        }

        // Calculate refund amount using the same logic as charging
        var breakdown = new RefundBreakdown();
        decimal totalRefund = 0m;

        // Calculate token-based refunds
        // For embeddings: prioritize embedding cost when available and no completion tokens
        if (modelCost.EmbeddingCostPerMillionTokens.HasValue && refundUsage.CompletionTokens.GetValueOrDefault() == 0 && refundUsage.PromptTokens.GetValueOrDefault() > 0)
        {
            // Use specialized embedding cost for prompt token refunds (cost is per million tokens)
            breakdown.EmbeddingRefund = (refundUsage.PromptTokens!.Value * modelCost.EmbeddingCostPerMillionTokens.Value) / 1_000_000m;
            breakdown.InputTokenRefund = 0; // Clear input token refund since we're using embedding cost
            totalRefund += breakdown.EmbeddingRefund;
        }
        else
        {
            // Calculate input token refunds, accounting for cached tokens
            var regularInputTokens = refundUsage.PromptTokens.GetValueOrDefault();
            
            // Handle cached input token refunds (read from cache)
            if (refundUsage.CachedInputTokens.HasValue && refundUsage.CachedInputTokens.Value > 0 && modelCost.CachedInputCostPerMillionTokens.HasValue)
            {
                // Subtract cached tokens from regular input tokens for refund calculation
                regularInputTokens -= refundUsage.CachedInputTokens.Value;
                
                // Add refund for cached tokens at the cached rate (cost is per million tokens)
                var cachedRefund = (refundUsage.CachedInputTokens.Value * modelCost.CachedInputCostPerMillionTokens.Value) / 1_000_000m;
                breakdown.InputTokenRefund = breakdown.InputTokenRefund + cachedRefund;
                totalRefund += cachedRefund;
                
                _logger.LogDebug("Applied cached input token refund for {CachedTokens} tokens at rate {CachedRate}",
                    refundUsage.CachedInputTokens.Value, modelCost.CachedInputCostPerMillionTokens.Value);
            }
            
            // Handle cache write token refunds
            if (refundUsage.CachedWriteTokens.HasValue && refundUsage.CachedWriteTokens.Value > 0 && modelCost.CachedInputWriteCostPerMillionTokens.HasValue)
            {
                // Cache write refunds are additional (cost is per million tokens)
                var cacheWriteRefund = (refundUsage.CachedWriteTokens.Value * modelCost.CachedInputWriteCostPerMillionTokens.Value) / 1_000_000m;
                breakdown.InputTokenRefund = breakdown.InputTokenRefund + cacheWriteRefund;
                totalRefund += cacheWriteRefund;
                
                _logger.LogDebug("Applied cache write token refund for {WriteTokens} tokens at rate {WriteRate}",
                    refundUsage.CachedWriteTokens.Value, modelCost.CachedInputWriteCostPerMillionTokens.Value);
            }
            
            // Add refund for remaining regular input tokens (cost is per million tokens)
            if (regularInputTokens > 0)
            {
                var regularRefund = (regularInputTokens * modelCost.InputCostPerMillionTokens) / 1_000_000m;
                breakdown.InputTokenRefund = breakdown.InputTokenRefund + regularRefund;
                totalRefund += regularRefund;
            }
        }

        // Always add completion token refund (cost is per million tokens)
        if (refundUsage.CompletionTokens.HasValue && refundUsage.CompletionTokens.Value > 0)
        {
            breakdown.OutputTokenRefund = (refundUsage.CompletionTokens.Value * modelCost.OutputCostPerMillionTokens) / 1_000_000m;
            totalRefund += breakdown.OutputTokenRefund;
        }

        // Handle image generation refunds
        if (modelCost.ImageCostPerImage.HasValue && refundUsage.ImageCount.HasValue && refundUsage.ImageCount.Value > 0)
        {
            var imageRefund = refundUsage.ImageCount.Value * modelCost.ImageCostPerImage.Value;
            
            // Apply quality multiplier if available
            if (modelCost.ImageQualityMultipliers != null && 
                !string.IsNullOrEmpty(refundUsage.ImageQuality) &&
                modelCost.ImageQualityMultipliers.TryGetValue(refundUsage.ImageQuality.ToLowerInvariant(), out var multiplier))
            {
                imageRefund *= multiplier;
            }
            
            breakdown.ImageRefund = imageRefund;
            totalRefund += breakdown.ImageRefund;
        }

        // Handle video generation refunds
        if (modelCost.VideoCostPerSecond.HasValue && refundUsage.VideoDurationSeconds.HasValue && refundUsage.VideoDurationSeconds.Value > 0)
        {
            var videoRefund = (decimal)refundUsage.VideoDurationSeconds.Value * modelCost.VideoCostPerSecond.Value;
            
            // Apply resolution multiplier if available
            if (modelCost.VideoResolutionMultipliers != null && 
                !string.IsNullOrEmpty(refundUsage.VideoResolution) &&
                modelCost.VideoResolutionMultipliers.TryGetValue(refundUsage.VideoResolution, out var multiplier))
            {
                videoRefund *= multiplier;
            }
            
            breakdown.VideoRefund = videoRefund;
            totalRefund += breakdown.VideoRefund;
        }

        // Handle search unit refunds
        if (modelCost.CostPerSearchUnit.HasValue && refundUsage.SearchUnits.HasValue && refundUsage.SearchUnits.Value > 0)
        {
            // Convert from per-1K-units to per-unit
            var costPerUnit = modelCost.CostPerSearchUnit.Value / 1000m;
            var searchRefund = refundUsage.SearchUnits.Value * costPerUnit;
            breakdown.SearchUnitRefund = searchRefund;
            totalRefund += searchRefund;
            
            _logger.LogDebug(
                "Search unit refund for model {ModelId}: {Units} units × ${CostPerUnit} = ${Total}",
                modelId,
                refundUsage.SearchUnits.Value,
                costPerUnit,
                searchRefund);
        }

        // Handle inference step refunds (for image generation)
        if (modelCost.CostPerInferenceStep.HasValue && refundUsage.InferenceSteps.HasValue && refundUsage.InferenceSteps.Value > 0)
        {
            var stepRefund = refundUsage.InferenceSteps.Value * modelCost.CostPerInferenceStep.Value;
            breakdown.InferenceStepRefund = stepRefund;
            totalRefund += stepRefund;
            
            _logger.LogDebug(
                "Inference step refund for model {ModelId}: {Steps} steps × ${CostPerStep} = ${Total}",
                modelId,
                refundUsage.InferenceSteps.Value,
                modelCost.CostPerInferenceStep.Value,
                stepRefund);
        }

        // Apply batch processing discount if applicable
        if (refundUsage.IsBatch == true && modelCost.SupportsBatchProcessing && modelCost.BatchProcessingMultiplier.HasValue)
        {
            var originalRefund = totalRefund;
            totalRefund *= modelCost.BatchProcessingMultiplier!.Value;
            _logger.LogDebug("Applied batch processing discount to refund for model {ModelId}. Original refund: {OriginalRefund}, Discounted refund: {DiscountedRefund}, Multiplier: {Multiplier}",
                modelId, originalRefund, totalRefund, modelCost.BatchProcessingMultiplier.Value);
        }

        result.RefundAmount = totalRefund;
        result.Breakdown = breakdown;

        _logger.LogInformation(
            "Calculated refund for model {ModelId}: {RefundAmount}. Reason: {RefundReason}. Original Transaction: {OriginalTransactionId}",
            modelId, totalRefund, refundReason, originalTransactionId ?? "N/A");

        return result;
    }

    private List<string> ValidateRefundAmounts(Usage originalUsage, Usage refundUsage)
    {
        var messages = new List<string>();

        if (refundUsage.PromptTokens > originalUsage.PromptTokens)
        {
            messages.Add($"Refund prompt tokens ({refundUsage.PromptTokens}) cannot exceed original ({originalUsage.PromptTokens}).");
        }

        if (refundUsage.CompletionTokens > originalUsage.CompletionTokens)
        {
            messages.Add($"Refund completion tokens ({refundUsage.CompletionTokens}) cannot exceed original ({originalUsage.CompletionTokens}).");
        }

        if (refundUsage.ImageCount.HasValue && originalUsage.ImageCount.HasValue &&
            refundUsage.ImageCount.Value > originalUsage.ImageCount.Value)
        {
            messages.Add($"Refund image count ({refundUsage.ImageCount.Value}) cannot exceed original ({originalUsage.ImageCount.Value}).");
        }

        if (refundUsage.VideoDurationSeconds.HasValue && originalUsage.VideoDurationSeconds.HasValue &&
            refundUsage.VideoDurationSeconds.Value > originalUsage.VideoDurationSeconds.Value)
        {
            messages.Add($"Refund video duration ({refundUsage.VideoDurationSeconds.Value}s) cannot exceed original ({originalUsage.VideoDurationSeconds.Value}s).");
        }

        // Validate all values are non-negative
        if (refundUsage.PromptTokens < 0 || refundUsage.CompletionTokens < 0)
        {
            messages.Add("Refund token counts must be non-negative.");
        }

        if (refundUsage.ImageCount.HasValue && refundUsage.ImageCount.Value < 0)
        {
            messages.Add("Refund image count must be non-negative.");
        }

        if (refundUsage.VideoDurationSeconds.HasValue && refundUsage.VideoDurationSeconds.Value < 0)
        {
            messages.Add("Refund video duration must be non-negative.");
        }

        // Validate search unit refund amounts
        if (refundUsage.SearchUnits.HasValue && originalUsage.SearchUnits.HasValue &&
            refundUsage.SearchUnits.Value > originalUsage.SearchUnits.Value)
        {
            messages.Add($"Refund search units ({refundUsage.SearchUnits.Value}) cannot exceed original ({originalUsage.SearchUnits.Value}).");
        }

        if (refundUsage.SearchUnits.HasValue && refundUsage.SearchUnits.Value < 0)
        {
            messages.Add("Refund search units must be non-negative.");
        }

        // Validate inference steps refund amounts
        if (refundUsage.InferenceSteps.HasValue && originalUsage.InferenceSteps.HasValue &&
            refundUsage.InferenceSteps.Value > originalUsage.InferenceSteps.Value)
        {
            messages.Add($"Refund inference steps ({refundUsage.InferenceSteps.Value}) cannot exceed original ({originalUsage.InferenceSteps.Value}).");
        }

        if (refundUsage.InferenceSteps.HasValue && refundUsage.InferenceSteps.Value < 0)
        {
            messages.Add("Refund inference steps must be non-negative.");
        }

        return messages;
    }

    private async Task<decimal> CalculatePerVideoCostAsync(string modelId, ModelCostInfo modelCost, Usage usage)
    {
        if (!usage.VideoDurationSeconds.HasValue || string.IsNullOrEmpty(usage.VideoResolution))
        {
            _logger.LogDebug("No video usage data for per-video pricing model {ModelId}", modelId);
            return 0m;
        }

        // Parse configuration or use pre-parsed
        PerVideoPricingConfig? config = null;
        if (modelCost.ParsedPricingConfiguration is PerVideoPricingConfig parsed)
        {
            config = parsed;
        }
        else if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
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

        if (config == null || config.Rates == null || config.Rates.Count == 0)
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

        return flatRate;
    }

    private async Task<decimal> CalculatePerSecondVideoCostAsync(string modelId, ModelCostInfo modelCost, Usage usage)
    {
        if (!usage.VideoDurationSeconds.HasValue)
        {
            _logger.LogDebug("No video duration for per-second video pricing model {ModelId}", modelId);
            return 0m;
        }

        // Parse configuration or use pre-parsed
        PerSecondVideoPricingConfig? config = null;
        if (modelCost.ParsedPricingConfiguration is PerSecondVideoPricingConfig parsed)
        {
            config = parsed;
        }
        else if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
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

        return baseCost;
    }

    private async Task<decimal> CalculateInferenceStepsCostAsync(string modelId, ModelCostInfo modelCost, Usage usage)
    {
        // Parse configuration or use pre-parsed
        InferenceStepsPricingConfig? config = null;
        if (modelCost.ParsedPricingConfiguration is InferenceStepsPricingConfig parsed)
        {
            config = parsed;
        }
        else if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
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
            return 0m;
        }

        var cost = steps * config.CostPerStep;

        _logger.LogDebug("Inference steps cost for model {ModelId}: {Steps} steps × ${CostPerStep} = ${Cost}",
            modelId, steps, config.CostPerStep, cost);

        return cost;
    }

    private async Task<decimal> CalculateTieredTokensCostAsync(string modelId, ModelCostInfo modelCost, Usage usage)
    {
        // Parse configuration or use pre-parsed
        TieredTokensPricingConfig? config = null;
        if (modelCost.ParsedPricingConfiguration is TieredTokensPricingConfig parsed)
        {
            config = parsed;
        }
        else if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
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

        if (config == null || config.Tiers == null || config.Tiers.Count == 0)
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

        return inputCost + outputCost;
    }

    private async Task<decimal> CalculatePerImageCostAsync(string modelId, ModelCostInfo modelCost, Usage usage)
    {
        if (!usage.ImageCount.HasValue || usage.ImageCount.Value <= 0)
        {
            _logger.LogDebug("No image count for per-image pricing model {ModelId}", modelId);
            return 0m;
        }

        // Parse configuration or use pre-parsed
        PerImagePricingConfig? config = null;
        if (modelCost.ParsedPricingConfiguration is PerImagePricingConfig parsed)
        {
            config = parsed;
        }
        else if (!string.IsNullOrEmpty(modelCost.PricingConfiguration))
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

        return cost;
    }

    private async Task<decimal> CalculatePerMinuteAudioCostAsync(string modelId, ModelCostInfo modelCost, Usage usage)
    {
        // This pricing model is for audio transcription/realtime
        // Delegate to standard calculation which already handles audio costs
        return await CalculateStandardCostAsync(modelId, modelCost, usage);
    }

    private async Task<decimal> CalculatePerThousandCharactersCostAsync(string modelId, ModelCostInfo modelCost, Usage usage)
    {
        // This pricing model is for text-to-speech
        // The standard calculation already handles AudioCostPerKCharacters
        return await CalculateStandardCostAsync(modelId, modelCost, usage);
    }
}
