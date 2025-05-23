// This file provides backward compatibility - use ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto instead

using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// Type alias for the VirtualKeyDto from ConduitLLM.Configuration.DTOs.VirtualKey
/// This exists to maintain backward compatibility while consolidating duplicate definitions.
/// </summary>
public class VirtualKeyDto : ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto
{
    // Compatibility properties for tests and older code

    /// <summary>
    /// Name of the key - compatibility alias for KeyName
    /// </summary>
    public new string Name
    {
        get => KeyName;
        set => KeyName = value;
    }

    /// <summary>
    /// Whether the key is active - compatibility alias for IsEnabled
    /// </summary>
    public new bool IsActive
    {
        get => IsEnabled;
        set => IsEnabled = value;
    }

    /// <summary>
    /// Usage limit - compatibility alias for MaxBudget
    /// </summary>
    public new decimal? UsageLimit
    {
        get => MaxBudget;
        set => MaxBudget = value;
    }

    /// <summary>
    /// Rate limit - compatibility alias for RateLimitRpm
    /// </summary>
    public new int? RateLimit
    {
        get => RateLimitRpm;
        set => RateLimitRpm = value;
    }
}
