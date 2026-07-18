using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.PromptCaching;

/// <summary>
/// Response DTO for the current prompt caching configuration.
/// </summary>
public class PromptCachingConfigDto
{
    /// <summary>
    /// Whether automatic cache_control injection is enabled.
    /// </summary>
    public bool AutoInjectEnabled { get; set; }

    /// <summary>
    /// The injection points defining which messages get cache_control directives.
    /// </summary>
    public List<CacheInjectionPointDto> InjectionPoints { get; set; } = new();
}

/// <summary>
/// Request DTO for updating the prompt caching configuration.
/// </summary>
public class UpdatePromptCachingConfigDto
{
    /// <summary>
    /// Whether automatic cache_control injection is enabled.
    /// </summary>
    [Required]
    public bool AutoInjectEnabled { get; set; }

    /// <summary>
    /// The injection points defining which messages get cache_control directives.
    /// Anthropic allows a maximum of 4 cache breakpoints per request.
    /// </summary>
    [Required]
    [MaxLength(4, ErrorMessage = "Maximum 4 injection points (Anthropic limit)")]
    public List<CacheInjectionPointDto> InjectionPoints { get; set; } = new();
}

/// <summary>
/// DTO representing a single cache injection point target.
/// </summary>
public class CacheInjectionPointDto
{
    /// <summary>
    /// Target by role: "system", "user", or "assistant". Null matches any role.
    /// </summary>
    [RegularExpression("^(system|user|assistant)$", ErrorMessage = "Role must be system, user, or assistant")]
    public string? Role { get; set; }

    /// <summary>
    /// Target by index: 0 = first matching, -1 = last matching, -2 = second-to-last.
    /// Null means all messages matching the role filter.
    /// </summary>
    [Range(-100, 100)]
    public int? Index { get; set; }
}
