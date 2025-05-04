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
