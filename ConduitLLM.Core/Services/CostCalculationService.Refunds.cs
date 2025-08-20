using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service implementation for refund cost calculations
/// </summary>
public partial class CostCalculationService
{
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
        if (validationMessages.Count() > 0)
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
            if (!string.IsNullOrEmpty(modelCost.ImageQualityMultipliers) && 
                !string.IsNullOrEmpty(refundUsage.ImageQuality))
            {
                try
                {
                    var qualityMultipliers = JsonSerializer.Deserialize<Dictionary<string, decimal>>(modelCost.ImageQualityMultipliers);
                    if (qualityMultipliers != null && qualityMultipliers.TryGetValue(refundUsage.ImageQuality.ToLowerInvariant(), out var multiplier))
                    {
                        imageRefund *= multiplier;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse ImageQualityMultipliers for refund calculation");
                }
            }
            
            breakdown.ImageRefund = imageRefund;
            totalRefund += breakdown.ImageRefund;
        }

        // Handle video generation refunds
        if (modelCost.VideoCostPerSecond.HasValue && refundUsage.VideoDurationSeconds.HasValue && refundUsage.VideoDurationSeconds.Value > 0)
        {
            var videoRefund = (decimal)refundUsage.VideoDurationSeconds.Value * modelCost.VideoCostPerSecond.Value;
            
            // Apply resolution multiplier if available
            if (!string.IsNullOrEmpty(modelCost.VideoResolutionMultipliers) && 
                !string.IsNullOrEmpty(refundUsage.VideoResolution))
            {
                try
                {
                    var resolutionMultipliers = JsonSerializer.Deserialize<Dictionary<string, decimal>>(modelCost.VideoResolutionMultipliers);
                    if (resolutionMultipliers != null && resolutionMultipliers.TryGetValue(refundUsage.VideoResolution, out var multiplier))
                    {
                        videoRefund *= multiplier;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse VideoResolutionMultipliers for refund calculation");
                }
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