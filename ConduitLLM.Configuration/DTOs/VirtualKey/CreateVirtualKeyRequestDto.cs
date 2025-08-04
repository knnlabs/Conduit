using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.VirtualKey; // Updated namespace

/// <summary>
/// DTO for requesting the creation of a new virtual key.
/// </summary>
public class CreateVirtualKeyRequestDto
{
    [Required(ErrorMessage = "Key name is required.")]
    [MaxLength(100, ErrorMessage = "Key name cannot exceed 100 characters.")]
    public string KeyName { get; set; } = string.Empty;

    public string? AllowedModels { get; set; } // Comma-separated

    /// <summary>
    /// Optional ID of an existing virtual key group to add this key to.
    /// If not provided, a new single-key group will be created.
    /// </summary>
    public int? VirtualKeyGroupId { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? Metadata { get; set; } // Optional JSON metadata

    public int? RateLimitRpm { get; set; }
    public int? RateLimitRpd { get; set; }
}
