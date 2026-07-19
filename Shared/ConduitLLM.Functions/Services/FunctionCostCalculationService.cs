using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Service implementation that calculates the cost of function executions based on usage data and pricing configuration.
/// </summary>
/// <remarks>
/// <para>
/// The FunctionCostCalculationService provides functionality to calculate the monetary cost of function executions
/// by combining usage data (results, tokens, duration) with pricing information from the function cost repository.
/// </para>
/// <para>
/// This service supports cost calculation for different pricing models:
/// </para>
/// <list type="bullet">
///   <item><description>FlatRate - Fixed cost per execution</description></item>
///   <item><description>PerResult - Cost based on number of results returned</description></item>
///   <item><description>PerToken - Cost based on tokens consumed (for Answer functions)</description></item>
///   <item><description>TimeBased - Cost based on execution duration</description></item>
///   <item><description>Tiered - Volume-based pricing tiers</description></item>
///   <item><description>Hybrid - Complex multi-dimensional pricing (e.g., Exa)</description></item>
/// </list>
/// <para>
/// Cost calculation is essential for budget management, usage tracking, and accurate billing.
/// </para>
/// </remarks>
public partial class FunctionCostCalculationService : IFunctionCostCalculationService
{
    private const decimal ProviderCostDriftWarningThreshold = 0.05m;
    private const decimal ProviderCostDriftMinimumUsd = 0.000001m;

    private readonly IFunctionCostService _functionCostService;
    private readonly ILogger<FunctionCostCalculationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionCostCalculationService"/> class.
    /// </summary>
    /// <param name="functionCostService">The service for retrieving function cost information.</param>
    /// <param name="logger">The logger for recording diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when functionCostService or logger is null.</exception>
    public FunctionCostCalculationService(
        IFunctionCostService functionCostService,
        ILogger<FunctionCostCalculationService> logger)
    {
        _functionCostService = functionCostService ?? throw new ArgumentNullException(nameof(functionCostService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// This implementation performs cost calculation using the following logic:
    /// </para>
    /// <list type="number">
    ///   <item><description>Retrieves the cost configuration for the function</description></item>
    ///   <item><description>Validates input parameters and handles edge cases</description></item>
    ///   <item><description>Determines the pricing model (FlatRate, PerResult, Hybrid, etc.)</description></item>
    ///   <item><description>Applies the appropriate pricing formula based on the model</description></item>
    /// </list>
    /// <para>
    /// If cost information is not found and the provider did not report an authoritative cost,
    /// the method fails rather than silently treating a billable execution as free.
    /// </para>
    /// </remarks>
    public async Task<decimal> CalculateCostAsync(
        int functionConfigurationId,
        FunctionExecutionUsage usage,
        CancellationToken cancellationToken = default)
    {
        if (functionConfigurationId <= 0)
        {
            _logger.LogWarning("Function configuration ID is invalid: {ConfigId}. Cannot calculate cost.", functionConfigurationId);
            return 0m;
        }

        if (usage == null)
        {
            _logger.LogWarning("Usage data is null for function configuration {ConfigId}. Cannot calculate cost.", functionConfigurationId);
            return 0m;
        }

        var functionCost = await _functionCostService.GetCostForConfigurationAsync(functionConfigurationId, cancellationToken);

        if (functionCost == null)
        {
            if (usage.ProviderReportedCost is >= 0m)
            {
                _logger.LogWarning(
                    "Cost information not found for function configuration {ConfigId}. Billing authoritative provider-reported cost {ProviderReportedCost}.",
                    functionConfigurationId, usage.ProviderReportedCost.Value);
                return usage.ProviderReportedCost.Value;
            }

            throw new InvalidOperationException(
                $"Cost information is required for function configuration {functionConfigurationId}.");
        }

        decimal calculatedCost = 0m;

        // Handle polymorphic pricing models
        switch (functionCost.PricingModel)
        {
            case Enums.FunctionPricingModel.FlatRate:
                calculatedCost = CalculateFlatRateCost(functionCost, usage);
                break;

            case Enums.FunctionPricingModel.PerResult:
                calculatedCost = CalculatePerResultCost(functionCost, usage);
                break;

            case Enums.FunctionPricingModel.PerToken:
                calculatedCost = CalculatePerTokenCost(functionCost, usage);
                break;

            case Enums.FunctionPricingModel.TimeBased:
                calculatedCost = CalculateTimeBasedCost(functionCost, usage);
                break;

            case Enums.FunctionPricingModel.Tiered:
                calculatedCost = CalculateTieredCost(functionCost, usage);
                break;

            case Enums.FunctionPricingModel.Hybrid:
                calculatedCost = await CalculateHybridCostAsync(functionCost, usage, cancellationToken);
                break;

            default:
                _logger.LogWarning("Unknown pricing model {PricingModel} for function configuration {ConfigId}. Using flat rate fallback.",
                    functionCost.PricingModel, functionConfigurationId);
                calculatedCost = CalculateFlatRateCost(functionCost, usage);
                break;
        }

        _logger.LogDebug("Calculated cost for function configuration {ConfigId} using pricing model {PricingModel} is {CalculatedCost}",
            functionConfigurationId, functionCost.PricingModel, calculatedCost);

        return ReconcileProviderReportedCost(functionConfigurationId, calculatedCost, usage.ProviderReportedCost);
    }

    private decimal ReconcileProviderReportedCost(
        int functionConfigurationId,
        decimal calculatedCost,
        decimal? providerReportedCost)
    {
        if (providerReportedCost is not >= 0m)
            return calculatedCost;

        var difference = Math.Abs(calculatedCost - providerReportedCost.Value);
        var comparisonBase = Math.Max(Math.Abs(calculatedCost), Math.Abs(providerReportedCost.Value));
        var driftRatio = comparisonBase == 0m ? 0m : difference / comparisonBase;

        if (difference > ProviderCostDriftMinimumUsd && driftRatio > ProviderCostDriftWarningThreshold)
        {
            _logger.LogWarning(
                "Function cost drift detected for configuration {ConfigId}: configured cost {CalculatedCost}, " +
                "provider-reported cost {ProviderReportedCost}, drift {DriftPercentage:P2}. Billing provider-reported cost.",
                functionConfigurationId, calculatedCost, providerReportedCost.Value, driftRatio);
        }

        return providerReportedCost.Value;
    }
}
