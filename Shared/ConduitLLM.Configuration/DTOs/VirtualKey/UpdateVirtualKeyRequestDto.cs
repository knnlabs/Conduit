using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ConduitLLM.Configuration.DTOs.VirtualKey;

/// <summary>
/// DTO for requesting updates to an existing virtual key.
/// All properties are optional; only provided values will be updated.
/// </summary>
public class UpdateVirtualKeyRequestDto
{
    [MaxLength(100, ErrorMessage = "Key name cannot exceed 100 characters.")]
    public string? KeyName { get; set; }

    public List<string>? AllowedModels { get; set; }

    /// <summary>
    /// Optional ID of a different virtual key group to move this key to.
    /// </summary>
    public int? VirtualKeyGroupId { get; set; }

    public bool? IsEnabled { get; set; }

    // To clear expiration, potentially pass a specific value or use a separate endpoint/flag?
    // For now, passing null leaves it unchanged, passing a date sets/updates it.
    public DateTime? ExpiresAt { get; set; }

    public Dictionary<string, JsonElement>? Metadata { get; set; }

    public int? RateLimitRpm { get; set; }
    public int? RateLimitRpd { get; set; }

    /// <summary>
    /// Optional tokens-per-minute ceiling. Null leaves the existing value untouched.
    /// </summary>
    public int? RateLimitTpm { get; set; }
}
