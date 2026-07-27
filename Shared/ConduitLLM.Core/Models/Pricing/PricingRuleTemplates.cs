namespace ConduitLLM.Core.Models.Pricing;

/// <summary>Canonical starter configurations for the pricing rules engine.</summary>
public static class PricingRuleTemplates
{
    private static readonly IReadOnlyDictionary<string, PricingRulesConfig> Templates =
        new Dictionary<string, PricingRulesConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["per_unit"] = new()
            {
                PricingType = "per_unit",
                DefaultRate = 0.05m,
                UnitField = "ImageCount",
                Rules =
                [
                    Rule(1, "HD quality", 0.08m, ("quality", "hd")),
                    Rule(2, "Standard quality", 0.05m, ("quality", "standard"))
                ]
            },
            ["per_second"] = new()
            {
                PricingType = "per_second",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds",
                Rules =
                [
                    Rule(1, "1080p video with audio", 0.15m,
                        ("resolution", "1080p"), ("with_audio", true)),
                    Rule(2, "1080p video without audio", 0.06m, ("resolution", "1080p")),
                    Rule(3, "720p video", 0.025m, ("resolution", "720p")),
                    Rule(4, "480p video", 0.015m, ("resolution", "480p"))
                ]
            },
            ["per_step"] = new()
            {
                PricingType = "per_step",
                DefaultRate = 0.00013m,
                UnitField = "InferenceSteps",
                Rules =
                [
                    Rule(1, "High quality (50+ steps)", 0.00015m,
                        ("inference_steps_gte", 50)),
                    Rule(2, "Standard quality", 0.00013m)
                ]
            }
        };

    /// <summary>
    /// Creates a mutable copy of a canonical template. Unknown types fall back to
    /// <c>per_second</c>, matching the endpoint's default.
    /// </summary>
    public static PricingRulesConfig Create(string? pricingType)
    {
        var key = string.IsNullOrWhiteSpace(pricingType) ? "per_second" : pricingType;
        var template = Templates.TryGetValue(key, out var configured)
            ? configured
            : Templates["per_second"];

        return new PricingRulesConfig
        {
            Version = template.Version,
            PricingType = template.PricingType,
            UnitField = template.UnitField,
            DefaultRate = template.DefaultRate,
            Constraints = template.Constraints,
            Rules = template.Rules.Select(rule => new PricingRule
            {
                Priority = rule.Priority,
                Description = rule.Description,
                Rate = rule.Rate,
                Conditions = new Dictionary<string, object>(
                    rule.Conditions,
                    StringComparer.OrdinalIgnoreCase)
            }).ToList()
        };
    }

    private static PricingRule Rule(
        int priority,
        string description,
        decimal rate,
        params (string Key, object Value)[] conditions) =>
        new()
        {
            Priority = priority,
            Description = description,
            Rate = rate,
            Conditions = conditions.ToDictionary(
                condition => condition.Key,
                condition => condition.Value,
                StringComparer.OrdinalIgnoreCase)
        };
}
