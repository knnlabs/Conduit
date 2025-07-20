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
        if (modelCost.EmbeddingTokenCost.HasValue && usage.CompletionTokens == 0)
        {
            // Use specialized embedding cost for prompt tokens
            calculatedCost += (usage.PromptTokens * modelCost.EmbeddingTokenCost.Value);
        }
        else
        {
            // Use regular token costs for chat/completions
            calculatedCost += (usage.PromptTokens * modelCost.InputTokenCost);
        }
        
        // Always add completion token cost
        calculatedCost += (usage.CompletionTokens * modelCost.OutputTokenCost);

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

        // Apply batch processing discount if applicable
        if (usage.IsBatch == true && modelCost.SupportsBatchProcessing && modelCost.BatchProcessingMultiplier.HasValue)
        {
            var originalCost = calculatedCost;
            calculatedCost *= modelCost.BatchProcessingMultiplier!.Value;
            _logger.LogDebug("Applied batch processing discount for model {ModelId}. Original cost: {OriginalCost}, Discounted cost: {DiscountedCost}, Multiplier: {Multiplier}",
                modelId, originalCost, calculatedCost, modelCost.BatchProcessingMultiplier.Value);
        }

        _logger.LogDebug("Calculated cost for model {ModelId} with usage (Prompt: {PromptTokens}, Completion: {CompletionTokens}, Images: {ImageCount}, Video: {VideoDuration}s, IsBatch: {IsBatch}) is {CalculatedCost}",
            modelId, usage.PromptTokens, usage.CompletionTokens, usage.ImageCount ?? 0, usage.VideoDurationSeconds ?? 0, usage.IsBatch ?? false, calculatedCost);

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
        if (modelCost.EmbeddingTokenCost.HasValue && refundUsage.CompletionTokens == 0 && refundUsage.PromptTokens > 0)
        {
            // Use specialized embedding cost for prompt token refunds
            breakdown.EmbeddingRefund = refundUsage.PromptTokens * modelCost.EmbeddingTokenCost.Value;
            breakdown.InputTokenRefund = 0; // Clear input token refund since we're using embedding cost
            totalRefund += breakdown.EmbeddingRefund;
        }
        else if (refundUsage.PromptTokens > 0)
        {
            // Use regular input token cost
            breakdown.InputTokenRefund = refundUsage.PromptTokens * modelCost.InputTokenCost;
            totalRefund += breakdown.InputTokenRefund;
        }

        // Always add completion token refund
        if (refundUsage.CompletionTokens > 0)
        {
            breakdown.OutputTokenRefund = refundUsage.CompletionTokens * modelCost.OutputTokenCost;
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

        return messages;
    }
}
