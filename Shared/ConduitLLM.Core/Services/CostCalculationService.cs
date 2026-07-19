using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
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
public partial class CostCalculationService : ICostCalculationService
{
    private readonly IModelCostService _modelCostService;
    private readonly ILogger<CostCalculationService> _logger;
    private readonly IPricingRulesEvaluator? _pricingRulesEvaluator;
    private readonly ICachedPricingRulesService? _cachedPricingRulesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CostCalculationService"/> class.
    /// </summary>
    /// <param name="modelCostService">The service for retrieving model cost information.</param>
    /// <param name="logger">The logger for recording diagnostic information.</param>
    /// <param name="pricingRulesEvaluator">Optional evaluator for rules-based pricing.</param>
    /// <param name="cachedPricingRulesService">Optional service for caching parsed pricing rules configurations.</param>
    /// <exception cref="ArgumentNullException">Thrown when modelCostService or logger is null.</exception>
    public CostCalculationService(
        IModelCostService modelCostService,
        ILogger<CostCalculationService> logger,
        IPricingRulesEvaluator? pricingRulesEvaluator = null,
        ICachedPricingRulesService? cachedPricingRulesService = null)
    {
        _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pricingRulesEvaluator = pricingRulesEvaluator;
        _cachedPricingRulesService = cachedPricingRulesService;
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
    /// If cost information is not found for the specified model, the method throws so the
    /// calling billing pipeline can preserve the usage for reconciliation.
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

        // Provider-reported cost is authoritative when the provider is configured as trusted.
        // Checked before the ModelCost lookup so a missing/stale ModelCost row still bills correctly.
        if (TryBillProviderReportedCost(usage, modelId, out var providerBilledCost))
        {
            return providerBilledCost;
        }

        var modelCost = await _modelCostService.GetCostForModelAsync(modelId, cancellationToken);

        if (modelCost == null)
        {
            _logger.LogError(
                "BILLING ALERT: Cost information not found for model {ModelId}. " +
                "The usage must be reconciled before it can be billed. " +
                "Usage details: PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, ImageCount={ImageCount}",
                modelId, usage.PromptTokens, usage.CompletionTokens, usage.ImageCount);
            throw new InvalidOperationException(
                $"No active model cost configuration was found for model '{modelId}'.");
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
            case PricingModel.RulesBased:
                calculatedCost = await CalculateRulesBasedCostAsync(modelId, modelCost, usage);
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

        // Log at Information level when cost is 0 to aid troubleshooting billing issues
        if (calculatedCost == 0m)
        {
            _logger.LogInformation(
                "Zero cost calculated for model {ModelId}. PricingModel={PricingModel}, " +
                "ModelCostId={ModelCostId}, CostName={CostName}, IsActive={IsActive}. " +
                "Usage: PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, ImageCount={ImageCount}, " +
                "VideoDurationSeconds={VideoDurationSeconds}. " +
                "Pricing: InputCost={InputCost}, OutputCost={OutputCost}",
                modelId, modelCost.PricingModel, modelCost.Id, modelCost.CostName, modelCost.IsActive,
                usage.PromptTokens, usage.CompletionTokens, usage.ImageCount, usage.VideoDurationSeconds,
                modelCost.InputCostPerMillionTokens, modelCost.OutputCostPerMillionTokens);
        }
        else
        {
            _logger.LogDebug("Calculated cost for model {ModelId} using pricing model {PricingModel} is {CalculatedCost}",
                modelId, modelCost.PricingModel, calculatedCost);
        }

        return calculatedCost;
    }

    /// <inheritdoc />
    public async Task<decimal> CalculateCostByIdAsync(int modelCostId, Usage usage, CancellationToken cancellationToken = default)
    {
        if (usage == null)
        {
            _logger.LogWarning("Usage data is null for ModelCostId {ModelCostId}. Cannot calculate cost.", modelCostId);
            return 0m;
        }

        // Provider-reported cost is authoritative when the provider is configured as trusted.
        // Checked before the ModelCost lookup so a missing/stale ModelCost row still bills correctly.
        if (TryBillProviderReportedCost(usage, $"ModelCostId:{modelCostId}", out var providerBilledCost))
        {
            return providerBilledCost;
        }

        var modelCost = await _modelCostService.GetCostByIdAsync(modelCostId, cancellationToken);

        if (modelCost == null)
        {
            _logger.LogError(
                "BILLING ALERT: Cost information not found for ModelCostId {ModelCostId}. " +
                "The cost record may be missing, inactive, not yet effective, or expired. " +
                "Usage details: PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, ImageCount={ImageCount}",
                modelCostId, usage.PromptTokens, usage.CompletionTokens, usage.ImageCount);
            throw new InvalidOperationException(
                $"No active model cost configuration was found for ModelCostId {modelCostId}.");
        }

        // Use the cost name as the model identifier for logging purposes
        var modelIdentifier = modelCost.CostName ?? $"ModelCostId:{modelCostId}";

        decimal calculatedCost = 0m;

        // Handle polymorphic pricing models (same logic as string-based lookup)
        switch (modelCost.PricingModel)
        {
            case PricingModel.Standard:
                calculatedCost = await CalculateStandardCostAsync(modelIdentifier, modelCost, usage);
                break;
            case PricingModel.PerVideo:
                calculatedCost = await CalculatePerVideoCostAsync(modelIdentifier, modelCost, usage);
                break;
            case PricingModel.PerSecondVideo:
                calculatedCost = await CalculatePerSecondVideoCostAsync(modelIdentifier, modelCost, usage);
                break;
            case PricingModel.InferenceSteps:
                calculatedCost = await CalculateInferenceStepsCostAsync(modelIdentifier, modelCost, usage);
                break;
            case PricingModel.TieredTokens:
                calculatedCost = await CalculateTieredTokensCostAsync(modelIdentifier, modelCost, usage);
                break;
            case PricingModel.PerImage:
                calculatedCost = await CalculatePerImageCostAsync(modelIdentifier, modelCost, usage);
                break;
            case PricingModel.RulesBased:
                calculatedCost = await CalculateRulesBasedCostAsync(modelIdentifier, modelCost, usage);
                break;
            default:
                _logger.LogWarning("Unknown pricing model {PricingModel} for ModelCostId {ModelCostId}. Using standard calculation.",
                    modelCost.PricingModel, modelCostId);
                calculatedCost = await CalculateStandardCostAsync(modelIdentifier, modelCost, usage);
                break;
        }

        // Apply batch processing discount if applicable
        if (usage.IsBatch == true && modelCost.SupportsBatchProcessing && modelCost.BatchProcessingMultiplier.HasValue)
        {
            var originalCost = calculatedCost;
            calculatedCost *= modelCost.BatchProcessingMultiplier!.Value;
            _logger.LogDebug("Applied batch processing discount for ModelCostId {ModelCostId}. Original: {OriginalCost}, Discounted: {DiscountedCost}, Multiplier: {Multiplier}",
                modelCostId, originalCost, calculatedCost, modelCost.BatchProcessingMultiplier.Value);
        }

        // Log at Information level when cost is 0 to aid troubleshooting billing issues
        if (calculatedCost == 0m)
        {
            _logger.LogInformation(
                "Zero cost calculated for ModelCostId {ModelCostId}. CostName={CostName}, PricingModel={PricingModel}, " +
                "IsActive={IsActive}. Usage: PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, " +
                "ImageCount={ImageCount}, VideoDurationSeconds={VideoDurationSeconds}. " +
                "Pricing: InputCost={InputCost}, OutputCost={OutputCost}, PricingConfiguration={PricingConfig}",
                modelCostId, modelCost.CostName, modelCost.PricingModel, modelCost.IsActive,
                usage.PromptTokens, usage.CompletionTokens, usage.ImageCount, usage.VideoDurationSeconds,
                modelCost.InputCostPerMillionTokens, modelCost.OutputCostPerMillionTokens,
                modelCost.PricingConfiguration ?? "(none)");
        }
        else
        {
            _logger.LogDebug("Calculated cost for ModelCostId {ModelCostId} ({CostName}) using pricing model {PricingModel} is {CalculatedCost}",
                modelCostId, modelCost.CostName, modelCost.PricingModel, calculatedCost);
        }

        return calculatedCost;
    }

    /// <summary>
    /// When the usage carries a trusted provider-reported cost, computes the authoritative billed
    /// amount (provider cost times the configured markup) and returns true, bypassing ModelCost
    /// calculation. Returns false when the provider is not trusted or reported no cost.
    /// </summary>
    /// <remarks>
    /// The batch-processing multiplier is deliberately NOT applied here: the provider-reported cost
    /// is the actual amount the operator was charged, so applying the list-price batch discount on
    /// top would double-discount. A trusted cost of 0 (e.g. free model variants) bills as 0.
    /// </remarks>
    private bool TryBillProviderReportedCost(Usage usage, string modelIdentifier, out decimal billedCost)
    {
        billedCost = 0m;

        if (usage.ProviderCostPolicy is not { TrustProviderReportedCost: true } policy ||
            usage.ProviderReportedCostUsd is not decimal providerCost ||
            providerCost < 0m)
        {
            return false;
        }

        var markup = policy.MarkupMultiplier > 0m ? policy.MarkupMultiplier : 1.0m;
        billedCost = providerCost * markup;

        _logger.LogInformation(
            "Billing provider-reported cost for {ModelIdentifier}: provider cost {ProviderCost}, " +
            "markup {Markup}, billed {BilledCost}. ModelCost calculation bypassed.",
            modelIdentifier, providerCost, markup, billedCost);

        return true;
    }
}
