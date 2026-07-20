using System.Text.RegularExpressions;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services;

/// <summary>Provider-owned prompt-cache policy contract.</summary>
public interface IPromptCachingProviderAdapter
{
    string Provider { get; }
    IReadOnlyList<PromptCachingCapability> Capabilities { get; }
    bool Supports(string model, PromptCachingStrategy strategy);
    IReadOnlyList<string> Validate(PromptCachingRule rule, string path);
}

/// <summary>
/// Registry of provider adapters. Capability declarations and validation deliberately live with
/// the provider contract instead of in a central matrix.
/// </summary>
public static class PromptCachingProviderAdapters
{
    private static readonly IReadOnlyList<IPromptCachingProviderAdapter> Registered =
        [new OpenRouterPromptCachingAdapter(), new OpenAIPromptCachingAdapter(), new GroqPromptCachingAdapter()];

    public static IReadOnlyList<PromptCachingCapability> Capabilities =>
        Registered.SelectMany(adapter => adapter.Capabilities).ToArray();

    public static IPromptCachingProviderAdapter? Find(string provider) => Registered.FirstOrDefault(adapter =>
        adapter.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));

    public static bool GlobMatches(string pattern, string value)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

internal sealed class OpenRouterPromptCachingAdapter : IPromptCachingProviderAdapter
{
    private static readonly HashSet<string> ExplicitAlibabaModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "deepseek/deepseek-v3.2", "qwen/qwen3-max", "qwen/qwen-plus", "qwen/qwen3.6-plus",
        "qwen/qwen3-coder-plus", "qwen/qwen3-coder-flash"
    };

    public string Provider => "OpenRouter";
    public IReadOnlyList<PromptCachingCapability> Capabilities { get; } =
    [
        new("OpenRouter", "anthropic/*", [PromptCachingStrategy.Automatic, PromptCachingStrategy.Explicit], ["5m", "1h"], null, 4, false),
        .. ExplicitAlibabaModels.OrderBy(x => x).Select(model =>
            new PromptCachingCapability("OpenRouter", model, [PromptCachingStrategy.Explicit], ["5m"], null, 4, false)),
        new("OpenRouter", "openai/*", [], [], 1024, 0, true),
        new("OpenRouter", "deepseek/*", [], [], null, 0, true),
        new("OpenRouter", "google/*", [], [], 2048, 0, true)
    ];

    public bool Supports(string model, PromptCachingStrategy strategy) =>
        model.StartsWith("anthropic/", StringComparison.OrdinalIgnoreCase) ||
        strategy == PromptCachingStrategy.Explicit && ExplicitAlibabaModels.Contains(model);

    public IReadOnlyList<string> Validate(PromptCachingRule rule, string path)
    {
        var errors = new List<string>();
        if (rule.Ttl is not null && rule.Ttl is not ("5m" or "1h")) errors.Add($"{path}.ttl must be 5m or 1h.");
        if (rule.Strategy == PromptCachingStrategy.Automatic)
        {
            if (!rule.ModelPattern.StartsWith("anthropic/", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{path}: automatic caching is supported only for OpenRouter anthropic/* routes.");
            if (rule.InjectionPoints.Count != 0) errors.Add($"{path}: automatic caching cannot define injection points.");
        }
        else if (rule.Strategy == PromptCachingStrategy.Explicit)
        {
            ValidateBreakpoints(rule, path, errors);
            if (!rule.ModelPattern.StartsWith("anthropic/", StringComparison.OrdinalIgnoreCase) &&
                (rule.ModelPattern.Contains('*') || !ExplicitAlibabaModels.Contains(rule.ModelPattern)))
                errors.Add($"{path}: Alibaba explicit caching requires an exact documented model identifier.");
            if (ExplicitAlibabaModels.Contains(rule.ModelPattern) && rule.Ttl == "1h")
                errors.Add($"{path}: Alibaba explicit caching supports only the 5m TTL.");
        }
        else errors.Add($"{path}.strategy is not supported by OpenRouter.");
        return errors;
    }

    internal static void ValidateBreakpoints(PromptCachingRule rule, string path, List<string> errors)
    {
        if (rule.InjectionPoints.Count is < 1 or > PromptCachingConstants.MaxExplicitBreakpoints)
            errors.Add($"{path}: explicit caching requires 1-4 injection points.");
        foreach (var point in rule.InjectionPoints)
        {
            if (point.Role is not null && !new[] { "system", "developer", "user", "assistant" }
                    .Contains(point.Role, StringComparer.OrdinalIgnoreCase))
                errors.Add($"{path}: breakpoint role must be system, developer, user, assistant, or null.");
            if (point.Index is < -100 or > 100) errors.Add($"{path}: breakpoint index must be between -100 and 100.");
        }
    }
}

public sealed class OpenAIPromptCachingAdapter : IPromptCachingProviderAdapter
{
    public string Provider => "OpenAI";
    public IReadOnlyList<PromptCachingCapability> Capabilities { get; } =
    [
        new("OpenAI", "*", [PromptCachingStrategy.Automatic], [], 1024, 0, true),
        new("OpenAI", "gpt-5.6*", [PromptCachingStrategy.Automatic, PromptCachingStrategy.Explicit], ["30m"], 1024, 4, false)
    ];

    public bool Supports(string model, PromptCachingStrategy strategy) => strategy == PromptCachingStrategy.Automatic ||
        strategy == PromptCachingStrategy.Explicit && IsExplicitModel(model);

    public IReadOnlyList<string> Validate(PromptCachingRule rule, string path)
    {
        var errors = new List<string>();
        if (rule.Ttl is not null && rule.Ttl != "30m") errors.Add($"{path}.ttl must be 30m for OpenAI.");
        if (rule.Strategy == PromptCachingStrategy.Automatic)
        {
            if (rule.InjectionPoints.Count != 0) errors.Add($"{path}: automatic caching cannot define injection points.");
        }
        else if (rule.Strategy == PromptCachingStrategy.Explicit)
        {
            if (!IsExplicitPattern(rule.ModelPattern)) errors.Add($"{path}: explicit caching requires GPT-5.6 or a later model family.");
            OpenRouterPromptCachingAdapter.ValidateBreakpoints(rule, path, errors);
        }
        else errors.Add($"{path}.strategy is not supported by OpenAI.");
        return errors;
    }

    public static bool IsExplicitModel(string model) => Regex.Match(model, @"^gpt-(?<major>\d+)(?:\.(?<minor>\d+))?", RegexOptions.IgnoreCase) is var match &&
        match.Success && (int.Parse(match.Groups["major"].Value) > 5 ||
        int.Parse(match.Groups["major"].Value) == 5 && int.TryParse(match.Groups["minor"].Value, out var minor) && minor >= 6);

    private static bool IsExplicitPattern(string pattern) => IsExplicitModel(pattern.TrimEnd('*'));
}

internal sealed class GroqPromptCachingAdapter : IPromptCachingProviderAdapter
{
    public string Provider => "Groq";
    public IReadOnlyList<PromptCachingCapability> Capabilities { get; } =
        [new("Groq", "openai/gpt-oss-*", [], [], null, 0, true)];
    public bool Supports(string model, PromptCachingStrategy strategy) => false;
    public IReadOnlyList<string> Validate(PromptCachingRule rule, string path) =>
        [$"{path}: Groq prompt caching is automatic and exposes no managed controls."];
}

public static class PromptCachingCapabilityCatalog
{
    [Obsolete("Use PromptCachingProviderAdapters.Capabilities.")]
    public static IReadOnlyList<PromptCachingCapability> All => PromptCachingProviderAdapters.Capabilities;
    public static bool IsEligible(string provider, string model) => PromptCachingProviderAdapters.Capabilities.Any(c =>
        c.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase) && PromptCachingProviderAdapters.GlobMatches(c.ModelPattern, model));
    public static bool GlobMatches(string pattern, string value) => PromptCachingProviderAdapters.GlobMatches(pattern, value);
}

public static class PromptCachingPolicyResolver
{
    /// <summary>Upgrades the persisted v2 OpenRouter strategy names without changing rule order.</summary>
    public static PromptCachingConfig Migrate(PromptCachingConfig config)
    {
        if (config.SchemaVersion != 2) return config;
        config.SchemaVersion = PromptCachingConstants.SchemaVersion;
#pragma warning disable CS0618
        foreach (var rule in config.Rules)
            rule.Strategy = rule.Strategy == PromptCachingStrategy.OpenRouterAutomatic
                ? PromptCachingStrategy.Automatic
                : rule.Strategy == PromptCachingStrategy.OpenRouterExplicit ? PromptCachingStrategy.Explicit : rule.Strategy;
#pragma warning restore CS0618
        return config;
    }

    public static IReadOnlyList<string> Validate(PromptCachingConfig config)
    {
        config = Migrate(config);
        var errors = new List<string>();
        if (config.SchemaVersion != PromptCachingConstants.SchemaVersion)
            errors.Add($"Unsupported prompt caching schema version {config.SchemaVersion}; expected {PromptCachingConstants.SchemaVersion}.");
        if (config.Rules is null) return [.. errors, "rules is required."];
        for (var i = 0; i < config.Rules.Count; i++)
        {
            var rule = config.Rules[i];
            var path = $"rules[{i}]";
            if (rule is null) { errors.Add($"{path} is required."); continue; }
            if (string.IsNullOrWhiteSpace(rule.Name)) errors.Add($"{path}.name is required.");
            if (string.IsNullOrWhiteSpace(rule.ModelPattern)) errors.Add($"{path}.model_pattern is required.");
            if (rule.InjectionPoints is null) { errors.Add($"{path}.injection_points is required."); continue; }
            var adapter = PromptCachingProviderAdapters.Find(rule.Provider);
            if (adapter is null) errors.Add($"{path}.provider does not expose managed prompt-cache controls.");
            else errors.AddRange(adapter.Validate(rule, path));
        }
        return errors;
    }

    public static PromptCachingIntent? Resolve(PromptCachingConfig config, string provider, string model, string? promptCacheKey = null)
    {
        config = Migrate(config);
        if (!config.Enabled || Validate(config).Count != 0) return null;
        var rule = config.Rules.FirstOrDefault(r => r.Enabled &&
            string.Equals(r.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            PromptCachingProviderAdapters.GlobMatches(r.ModelPattern, model));
        var adapter = PromptCachingProviderAdapters.Find(provider);
        if (rule is null || adapter is null || !adapter.Supports(model, rule.Strategy)) return null;
        return new PromptCachingIntent
        {
            Strategy = rule.Strategy,
            Ttl = rule.Ttl,
            InjectionPoints = rule.InjectionPoints.ToArray(),
            PromptCacheKey = promptCacheKey
        };
    }
}
