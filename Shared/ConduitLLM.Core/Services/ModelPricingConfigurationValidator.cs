using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Models.Pricing;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Validates persisted model pricing JSON before it can reach the billing path.
/// </summary>
public static class ModelPricingConfigurationValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Throws when a complex pricing model has malformed or unusable configuration.
    /// </summary>
    public static void Validate(PricingModel pricingModel, string? pricingConfiguration)
    {
        if (pricingModel == PricingModel.Standard)
            return;

        if (string.IsNullOrWhiteSpace(pricingConfiguration))
            throw new ArgumentException($"Pricing configuration is required for {pricingModel} pricing.");

        try
        {
            switch (pricingModel)
            {
                case PricingModel.PerVideo:
                    var perVideo = Deserialize<PerVideoPricingConfig>(pricingConfiguration);
                    if (perVideo.Rates == null || perVideo.Rates.Count == 0 || perVideo.Rates.Any(rate =>
                            string.IsNullOrWhiteSpace(rate.Key) || rate.Value <= 0))
                    {
                        throw new ArgumentException("Per-video pricing must contain at least one named rate greater than zero.");
                    }
                    break;

                case PricingModel.PerSecondVideo:
                    var perSecond = Deserialize<PerSecondVideoPricingConfig>(pricingConfiguration);
                    if (perSecond.BaseRate <= 0 || perSecond.ResolutionMultipliers?.Any(x => x.Value <= 0) == true)
                    {
                        throw new ArgumentException("Per-second video pricing requires a baseRate greater than zero and positive multipliers.");
                    }
                    break;

                case PricingModel.InferenceSteps:
                    var steps = Deserialize<InferenceStepsPricingConfig>(pricingConfiguration);
                    if (steps.CostPerStep <= 0 || steps.DefaultSteps <= 0)
                    {
                        throw new ArgumentException("Inference-step pricing requires costPerStep and defaultSteps greater than zero.");
                    }
                    break;

                case PricingModel.TieredTokens:
                    var tiered = Deserialize<TieredTokensPricingConfig>(pricingConfiguration);
                    if (tiered.Tiers == null || tiered.Tiers.Count == 0 || tiered.Tiers.Any(tier =>
                            tier.InputCost < 0 || tier.OutputCost < 0 || tier.MaxContext <= 0))
                    {
                        throw new ArgumentException("Tiered-token pricing requires at least one valid, non-negative tier.");
                    }
                    break;

                case PricingModel.PerImage:
                    var perImage = Deserialize<PerImagePricingConfig>(pricingConfiguration);
                    if (perImage.BaseRate <= 0 ||
                        perImage.QualityMultipliers?.Any(x => x.Value <= 0) == true ||
                        perImage.ResolutionMultipliers?.Any(x => x.Value <= 0) == true)
                    {
                        throw new ArgumentException("Per-image pricing requires a baseRate greater than zero and positive multipliers.");
                    }
                    break;

                case PricingModel.RulesBased:
                    using (var document = JsonDocument.Parse(pricingConfiguration))
                    {
                        if (document.RootElement.ValueKind != JsonValueKind.Object)
                            throw new ArgumentException("Rules-based pricing configuration must be a JSON object.");
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(pricingModel), pricingModel, "Unsupported pricing model.");
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid {pricingModel} pricing configuration JSON: {ex.Message}",
                nameof(pricingConfiguration), ex);
        }
    }

    private static T Deserialize<T>(string json) where T : class
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new ArgumentException($"Pricing configuration could not be parsed as {typeof(T).Name}.");
    }
}
