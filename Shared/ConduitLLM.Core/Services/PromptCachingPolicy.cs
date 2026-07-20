using System.Text.RegularExpressions;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services;

public static class PromptCachingCapabilityCatalog
{
    private static readonly HashSet<string> ExplicitAlibabaModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "deepseek/deepseek-v3.2",
        "qwen/qwen3-max",
        "qwen/qwen-plus",
        "qwen/qwen3.6-plus",
        "qwen/qwen3-coder-plus",
        "qwen/qwen3-coder-flash"
    };

    public static IReadOnlyList<PromptCachingCapability> All { get; } =
    [
        new("OpenRouter", "anthropic/*",
            [PromptCachingStrategy.OpenRouterAutomatic, PromptCachingStrategy.OpenRouterExplicit],
            ["5m", "1h"], null, PromptCachingConstants.MaxExplicitBreakpoints, false),
        .. ExplicitAlibabaModels.OrderBy(x => x).Select(model =>
            new PromptCachingCapability("OpenRouter", model,
                [PromptCachingStrategy.OpenRouterExplicit], ["5m"], null,
                PromptCachingConstants.MaxExplicitBreakpoints, false)),
        new("OpenAI", "*", Array.Empty<PromptCachingStrategy>(), Array.Empty<string>(), 1024, 0, true),
        new("Groq", "openai/gpt-oss-*", Array.Empty<PromptCachingStrategy>(), Array.Empty<string>(), null, 0, true),
        new("OpenRouter", "openai/*", Array.Empty<PromptCachingStrategy>(), Array.Empty<string>(), 1024, 0, true),
        new("OpenRouter", "deepseek/*", Array.Empty<PromptCachingStrategy>(), Array.Empty<string>(), null, 0, true),
        new("OpenRouter", "google/*", Array.Empty<PromptCachingStrategy>(), Array.Empty<string>(), 2048, 0, true)
    ];

    public static bool IsExplicitAlibabaModel(string model) => ExplicitAlibabaModels.Contains(model);

    public static bool IsManagedStrategySupported(string provider, string model, PromptCachingStrategy strategy)
    {
        if (!provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase)) return false;
        if (model.StartsWith("anthropic/", StringComparison.OrdinalIgnoreCase)) return true;
        return strategy == PromptCachingStrategy.OpenRouterExplicit && IsExplicitAlibabaModel(model);
    }

    public static bool IsEligible(string provider, string model) => All.Any(c =>
        c.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase) && GlobMatches(c.ModelPattern, model));

    public static bool GlobMatches(string pattern, string value)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

public static class PromptCachingPolicyResolver
{
    public static IReadOnlyList<string> Validate(PromptCachingConfig config)
    {
        var errors = new List<string>();
        if (config.SchemaVersion != PromptCachingConstants.SchemaVersion)
            errors.Add($"Unsupported prompt caching schema version {config.SchemaVersion}; expected {PromptCachingConstants.SchemaVersion}.");

        if (config.Rules is null)
        {
            errors.Add("rules is required.");
            return errors;
        }

        for (var i = 0; i < config.Rules.Count; i++)
        {
            var rule = config.Rules[i];
            var prefix = $"rules[{i}]";
            if (rule is null)
            {
                errors.Add($"{prefix} is required.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(rule.Name)) errors.Add($"{prefix}.name is required.");
            if (!string.Equals(rule.Provider, "OpenRouter", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{prefix}.provider must be OpenRouter for managed caching.");
            if (string.IsNullOrWhiteSpace(rule.ModelPattern)) errors.Add($"{prefix}.model_pattern is required.");
            if (rule.Ttl is not null && rule.Ttl is not ("5m" or "1h")) errors.Add($"{prefix}.ttl must be 5m or 1h.");

            if (rule.InjectionPoints is null)
            {
                errors.Add($"{prefix}.injection_points is required.");
                continue;
            }

            if (rule.Strategy == PromptCachingStrategy.OpenRouterAutomatic)
            {
                if (!rule.ModelPattern.StartsWith("anthropic/", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{prefix}: automatic caching is supported only for OpenRouter anthropic/* routes.");
                if (rule.InjectionPoints.Count != 0) errors.Add($"{prefix}: automatic caching cannot define injection points.");
            }
            else
            {
                if (rule.InjectionPoints.Count is < 1 or > PromptCachingConstants.MaxExplicitBreakpoints)
                    errors.Add($"{prefix}: explicit caching requires 1-4 injection points.");
                if (!rule.ModelPattern.StartsWith("anthropic/", StringComparison.OrdinalIgnoreCase) &&
                    (rule.ModelPattern.Contains('*') || !PromptCachingCapabilityCatalog.IsExplicitAlibabaModel(rule.ModelPattern)))
                    errors.Add($"{prefix}: Alibaba explicit caching requires an exact documented model identifier.");
                if (PromptCachingCapabilityCatalog.IsExplicitAlibabaModel(rule.ModelPattern) && rule.Ttl == "1h")
                    errors.Add($"{prefix}: Alibaba explicit caching supports only the 5m TTL.");
            }

            foreach (var point in rule.InjectionPoints)
            {
                if (point.Role is not null &&
                    !point.Role.Equals("system", StringComparison.OrdinalIgnoreCase) &&
                    !point.Role.Equals("user", StringComparison.OrdinalIgnoreCase) &&
                    !point.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{prefix}: breakpoint role must be system, user, assistant, or null.");
                if (point.Index is < -100 or > 100) errors.Add($"{prefix}: breakpoint index must be between -100 and 100.");
            }
        }
        return errors;
    }

    public static PromptCachingIntent? Resolve(PromptCachingConfig config, string provider, string model)
    {
        if (!config.Enabled || Validate(config).Count != 0) return null;
        var rule = config.Rules.FirstOrDefault(r => r.Enabled &&
            string.Equals(r.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            PromptCachingCapabilityCatalog.GlobMatches(r.ModelPattern, model));
        if (rule is null || !PromptCachingCapabilityCatalog.IsManagedStrategySupported(provider, model, rule.Strategy)) return null;
        return new PromptCachingIntent
        {
            Strategy = rule.Strategy,
            Ttl = rule.Ttl ?? "5m",
            InjectionPoints = rule.InjectionPoints.ToArray()
        };
    }
}
