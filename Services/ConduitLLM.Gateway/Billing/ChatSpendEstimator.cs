using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Options;

using Microsoft.Extensions.Options;

namespace ConduitLLM.Gateway.Billing;

public sealed record ChatSpendEstimate(
    bool Succeeded,
    decimal Amount,
    int PromptTokens,
    int MaximumOutputTokens,
    int? ModelCostId,
    string? FailureReason = null);

public interface IChatSpendEstimator
{
    Task<ChatSpendEstimate> EstimateMaximumCostAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ChatSpendEstimator : IChatSpendEstimator
{
    private readonly IModelProviderMappingService _mappingService;
    private readonly ITokenCounter _tokenCounter;
    private readonly ICostCalculationService _costCalculationService;
    private readonly BillingAdmissionOptions _options;

    public ChatSpendEstimator(
        IModelProviderMappingService mappingService,
        ITokenCounter tokenCounter,
        ICostCalculationService costCalculationService,
        IOptions<BillingAdmissionOptions> options)
    {
        _mappingService = mappingService;
        _tokenCounter = tokenCounter;
        _costCalculationService = costCalculationService;
        _options = options.Value;
    }

    public async Task<ChatSpendEstimate> EstimateMaximumCostAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _mappingService.GetMappingByModelAliasAsync(request.Model);
        if (mapping is null)
        {
            return Failed("No active model-provider mapping is available");
        }

        var maximumOutputTokens = request.MaxTokens ??
                                  mapping.ModelProviderTypeAssociation?.MaxOutputTokens ??
                                  mapping.ModelProviderTypeAssociation?.Model?.MaxOutputTokens ??
                                  _options.DefaultMaximumOutputTokens;
        maximumOutputTokens = Math.Min(maximumOutputTokens, _options.MaximumOutputTokensCap);
        if (maximumOutputTokens <= 0)
        {
            return Failed("A positive maximum output-token bound is required");
        }

        var promptTokens = await _tokenCounter.EstimateTokenCountAsync(request.Model, request.Messages);
        var usage = new Usage
        {
            PromptTokens = promptTokens,
            CompletionTokens = maximumOutputTokens,
            TotalTokens = checked(promptTokens + maximumOutputTokens)
        };
        var modelCostId = mapping.ModelProviderTypeAssociation?.ModelCostId;
        var amount = modelCostId.HasValue
            ? await _costCalculationService.CalculateCostByIdAsync(modelCostId.Value, usage, cancellationToken)
            : await _costCalculationService.CalculateCostAsync(request.Model, usage, cancellationToken);
        if (amount <= 0m)
        {
            return Failed("Pricing did not produce a positive bounded reservation amount");
        }

        return new ChatSpendEstimate(
            true,
            amount,
            promptTokens,
            maximumOutputTokens,
            modelCostId);
    }

    private static ChatSpendEstimate Failed(string reason) =>
        new(false, 0m, 0, 0, null, reason);
}
