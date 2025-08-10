using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.VirtualKey;

/// <summary>
/// DTO for requesting updates to an existing virtual key.
/// All properties are optional; only provided values will be updated.
/// </summary>
public class UpdateVirtualKeyRequestDto
{
    [MaxLength(100, ErrorMessage = "Key name cannot exceed 100 characters.")]
    public string? KeyName { get; set; }

    public string? AllowedModels { get; set; } // Comma-separated. Empty string clears the list, null leaves unchanged.

    /// <summary>
    /// Optional ID of a different virtual key group to move this key to.
    /// </summary>
    public int? VirtualKeyGroupId { get; set; }

    public bool? IsEnabled { get; set; }

    // To clear expiration, potentially pass a specific value or use a separate endpoint/flag?
    // For now, passing null leaves it unchanged, passing a date sets/updates it.
    public DateTime? ExpiresAt { get; set; }

    public string? Metadata { get; set; } // Optional JSON metadata. Empty string clears, null leaves unchanged.

    public int? RateLimitRpm { get; set; }
    public int? RateLimitRpd { get; set; }
}
