using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Models.Pricing;
using ConduitLLM.Core.Services;

using FsCheck;
using FsCheck.Xunit;

namespace ConduitLLM.BillingInvariantTests;

public sealed class PricingConfigurationProperties
{
    [Property(MaxTest = 500)]
    public void Increasing_tier_rates_are_accepted(PositiveInt boundary, NonNegativeInt inputRate, NonNegativeInt outputRate)
    {
        var config = new TieredTokensPricingConfig
        {
            Tiers =
            [
                new TokenPricingTier
                {
                    MaxContext = boundary.Get,
                    InputCost = inputRate.Get,
                    OutputCost = outputRate.Get
                },
                new TokenPricingTier
                {
                    MaxContext = null,
                    InputCost = inputRate.Get + 1m,
                    OutputCost = outputRate.Get + 1m
                }
            ]
        };

        ModelPricingConfigurationValidator.Validate(
            PricingModel.TieredTokens, JsonSerializer.Serialize(config));
    }

    [Fact]
    public void Decreasing_tier_rate_is_rejected()
    {
        var config = new TieredTokensPricingConfig
        {
            Tiers =
            [
                new TokenPricingTier { MaxContext = 100, InputCost = 2m, OutputCost = 3m },
                new TokenPricingTier { MaxContext = null, InputCost = 1m, OutputCost = 3m }
            ]
        };

        Assert.Throws<ArgumentException>(() => ModelPricingConfigurationValidator.Validate(
            PricingModel.TieredTokens, JsonSerializer.Serialize(config)));
    }

    [Fact]
    public void Longer_video_with_lower_flat_rate_is_rejected()
    {
        var config = new PerVideoPricingConfig
        {
            Rates = new Dictionary<string, decimal>
            {
                ["720p_6"] = 0.20m,
                ["720p_10"] = 0.10m
            }
        };

        Assert.Throws<ArgumentException>(() => ModelPricingConfigurationValidator.Validate(
            PricingModel.PerVideo, JsonSerializer.Serialize(config)));
    }
}
