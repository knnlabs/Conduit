using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

public static class PromptCachingConstants
{
    public const int SchemaVersion = 3;
    public const string SettingsKey = "PromptCaching.Config";
    public const int MaxExplicitBreakpoints = 4;
}

/// <summary>Provider-aware managed prompt caching configuration.</summary>
public sealed class PromptCachingConfig
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("rules")]
    public List<PromptCachingRule> Rules { get; set; } = new();
}

public sealed class PromptCachingRule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("model_pattern")]
    public string ModelPattern { get; set; } = string.Empty;

    [JsonPropertyName("strategy")]
    [JsonConverter(typeof(JsonStringEnumConverter<PromptCachingStrategy>))]
    public PromptCachingStrategy Strategy { get; set; }

    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ttl { get; set; }

    [JsonPropertyName("injection_points")]
    public List<CacheInjectionPoint> InjectionPoints { get; set; } = new();
}

public enum PromptCachingStrategy
{
    Automatic,
    Explicit,
    [Obsolete("Migrated to Automatic in schema v3.")]
    OpenRouterAutomatic = Automatic,
    [Obsolete("Migrated to Explicit in schema v3.")]
    OpenRouterExplicit = Explicit
}

public sealed class CacheInjectionPoint
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonPropertyName("index")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Index { get; set; }
}

/// <summary>Server-only resolved intent consumed by a provider adapter.</summary>
public sealed class PromptCachingIntent
{
    public required PromptCachingStrategy Strategy { get; init; }
    public string? Ttl { get; init; }
    public IReadOnlyList<CacheInjectionPoint> InjectionPoints { get; init; } = Array.Empty<CacheInjectionPoint>();
    public string? PromptCacheKey { get; init; }
}

public sealed record PromptCachingCapability(
    string Provider,
    string ModelPattern,
    IReadOnlyList<PromptCachingStrategy> Strategies,
    IReadOnlyList<string> Ttls,
    int? MinimumTokens,
    int MaxBreakpoints,
    bool ProviderManaged);
