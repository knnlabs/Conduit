using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service implementation that calculates the cost of LLM operations based on usage data and model pricing.
/// </summary>
public class CostCalculationService : ICostCalculationService
{
    private readonly IModelCostService _modelCostService;
    private readonly ILogger<CostCalculationService> _logger;

    public CostCalculationService(IModelCostService modelCostService, ILogger<CostCalculationService> logger)
    {
        _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
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

        // Calculate cost based on token usage (Chat/Completions)
        calculatedCost += (usage.PromptTokens * modelCost.InputTokenCost);
        calculatedCost += (usage.CompletionTokens * modelCost.OutputTokenCost);

        // Add embedding cost if applicable (assuming embedding usage is in PromptTokens)
        if (modelCost.EmbeddingTokenCost.HasValue && usage.CompletionTokens == 0 && usage.ImageCount == null)
        {
            // Overwrite the calculation for embedding models
            // When it's an embedding request, only prompt tokens are used
            calculatedCost = usage.PromptTokens * modelCost.EmbeddingTokenCost.Value;
        }

        // Add image generation cost if applicable
        if (modelCost.ImageCostPerImage.HasValue && usage.ImageCount.HasValue)
        {
            calculatedCost += (usage.ImageCount.Value * modelCost.ImageCostPerImage.Value);
        }

        _logger.LogDebug("Calculated cost for model {ModelId} with usage (Prompt: {PromptTokens}, Completion: {CompletionTokens}, Images: {ImageCount}) is {CalculatedCost}",
            modelId, usage.PromptTokens, usage.CompletionTokens, usage.ImageCount ?? 0, calculatedCost);

        return calculatedCost;
    }
}
