using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;

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

        // Calculate cost based on token usage
        // For embeddings: prioritize embedding cost when available and no completion tokens
        if (modelCost.EmbeddingTokenCost.HasValue && usage.CompletionTokens.GetValueOrDefault() == 0 && usage.PromptTokens.HasValue)
        {
            // Use specialized embedding cost for prompt tokens
            calculatedCost += (usage.PromptTokens.Value * modelCost.EmbeddingTokenCost.Value);
        }
        else
        {
            // Calculate input token costs, accounting for cached tokens
            var regularInputTokens = usage.PromptTokens.GetValueOrDefault();
            
            // Handle cached input tokens (read from cache)
            if (usage.CachedInputTokens.HasValue && usage.CachedInputTokens.Value > 0 && modelCost.CachedInputTokenCost.HasValue)
            {
                // Subtract cached tokens from regular input tokens
                regularInputTokens -= usage.CachedInputTokens.Value;
                
                // Add cost for cached tokens at the cached rate
                calculatedCost += (usage.CachedInputTokens.Value * modelCost.CachedInputTokenCost.Value);
                
                _logger.LogDebug("Applied cached input token pricing for {CachedTokens} tokens at rate {CachedRate}",
                    usage.CachedInputTokens.Value, modelCost.CachedInputTokenCost.Value);
            }
            
            // Handle cache write tokens
            if (usage.CachedWriteTokens.HasValue && usage.CachedWriteTokens.Value > 0 && modelCost.CachedInputWriteCost.HasValue)
            {
                // Cache writes are additional to regular input processing
                calculatedCost += (usage.CachedWriteTokens.Value * modelCost.CachedInputWriteCost.Value);
                
                _logger.LogDebug("Applied cache write token pricing for {WriteTokens} tokens at rate {WriteRate}",
                    usage.CachedWriteTokens.Value, modelCost.CachedInputWriteCost.Value);
            }
            
            // Add cost for remaining regular input tokens
            if (regularInputTokens > 0)
            {
                calculatedCost += (regularInputTokens * modelCost.InputTokenCost);
            }
        }
        
        // Always add completion token cost
        if (usage.CompletionTokens.HasValue)
        {
            calculatedCost += (usage.CompletionTokens.Value * modelCost.OutputTokenCost);
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

        // Apply batch processing discount if applicable
        if (usage.IsBatch == true && modelCost.SupportsBatchProcessing && modelCost.BatchProcessingMultiplier.HasValue)
        {
            var originalCost = calculatedCost;
            calculatedCost *= modelCost.BatchProcessingMultiplier!.Value;
            _logger.LogDebug("Applied batch processing discount for model {ModelId}. Original cost: {OriginalCost}, Discounted cost: {DiscountedCost}, Multiplier: {Multiplier}",
                modelId, originalCost, calculatedCost, modelCost.BatchProcessingMultiplier.Value);
        }

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
        if (modelCost.EmbeddingTokenCost.HasValue && refundUsage.CompletionTokens.GetValueOrDefault() == 0 && refundUsage.PromptTokens.GetValueOrDefault() > 0)
        {
            // Use specialized embedding cost for prompt token refunds
            breakdown.EmbeddingRefund = refundUsage.PromptTokens!.Value * modelCost.EmbeddingTokenCost.Value;
            breakdown.InputTokenRefund = 0; // Clear input token refund since we're using embedding cost
            totalRefund += breakdown.EmbeddingRefund;
        }
        else
        {
            // Calculate input token refunds, accounting for cached tokens
            var regularInputTokens = refundUsage.PromptTokens.GetValueOrDefault();
            
            // Handle cached input token refunds (read from cache)
            if (refundUsage.CachedInputTokens.HasValue && refundUsage.CachedInputTokens.Value > 0 && modelCost.CachedInputTokenCost.HasValue)
            {
                // Subtract cached tokens from regular input tokens for refund calculation
                regularInputTokens -= refundUsage.CachedInputTokens.Value;
                
                // Add refund for cached tokens at the cached rate
                var cachedRefund = refundUsage.CachedInputTokens.Value * modelCost.CachedInputTokenCost.Value;
                breakdown.InputTokenRefund = breakdown.InputTokenRefund + cachedRefund;
                totalRefund += cachedRefund;
                
                _logger.LogDebug("Applied cached input token refund for {CachedTokens} tokens at rate {CachedRate}",
                    refundUsage.CachedInputTokens.Value, modelCost.CachedInputTokenCost.Value);
            }
            
            // Handle cache write token refunds
            if (refundUsage.CachedWriteTokens.HasValue && refundUsage.CachedWriteTokens.Value > 0 && modelCost.CachedInputWriteCost.HasValue)
            {
                // Cache write refunds are additional
                var cacheWriteRefund = refundUsage.CachedWriteTokens.Value * modelCost.CachedInputWriteCost.Value;
                breakdown.InputTokenRefund = breakdown.InputTokenRefund + cacheWriteRefund;
                totalRefund += cacheWriteRefund;
                
                _logger.LogDebug("Applied cache write token refund for {WriteTokens} tokens at rate {WriteRate}",
                    refundUsage.CachedWriteTokens.Value, modelCost.CachedInputWriteCost.Value);
            }
            
            // Add refund for remaining regular input tokens
            if (regularInputTokens > 0)
            {
                var regularRefund = regularInputTokens * modelCost.InputTokenCost;
                breakdown.InputTokenRefund = breakdown.InputTokenRefund + regularRefund;
                totalRefund += regularRefund;
            }
        }

        // Always add completion token refund
        if (refundUsage.CompletionTokens.HasValue && refundUsage.CompletionTokens.Value > 0)
        {
            breakdown.OutputTokenRefund = refundUsage.CompletionTokens.Value * modelCost.OutputTokenCost;
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
}
