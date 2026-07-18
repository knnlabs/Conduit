using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Configuration for automatic prompt caching injection.
/// Stored as a GlobalSetting with key "PromptCaching.Config".
/// </summary>
public class PromptCachingConfig
{
    /// <summary>
    /// Whether automatic cache_control injection is enabled.
    /// </summary>
    [JsonPropertyName("auto_inject_enabled")]
    public bool AutoInjectEnabled { get; set; }

    /// <summary>
    /// The injection points defining which messages get cache_control directives.
    /// </summary>
    [JsonPropertyName("injection_points")]
    public List<CacheInjectionPoint> InjectionPoints { get; set; } = new();
}

/// <summary>
/// Defines a target for automatic cache_control injection.
/// </summary>
public class CacheInjectionPoint
{
    /// <summary>
    /// Target by role: "system", "user", "assistant". Null matches any role.
    /// </summary>
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    /// <summary>
    /// Target by index: 0 = first matching, -1 = last matching, -2 = second-to-last.
    /// Null means all messages matching the role filter.
    /// </summary>
    [JsonPropertyName("index")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Index { get; set; }
}
