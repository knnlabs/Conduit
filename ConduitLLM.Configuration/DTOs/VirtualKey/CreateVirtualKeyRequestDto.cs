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
    /// Required ID of an existing virtual key group to add this key to.
    /// Create a virtual key group first using POST /api/virtualkey-groups.
    /// </summary>
    [Required(ErrorMessage = "VirtualKeyGroupId is required. Create a virtual key group first using POST /api/virtualkey-groups.")]
    [Range(1, int.MaxValue, ErrorMessage = "VirtualKeyGroupId must be a valid positive number. Create a virtual key group first using POST /api/virtualkey-groups.")]
    public int VirtualKeyGroupId { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? Metadata { get; set; } // Optional JSON metadata

    public int? RateLimitRpm { get; set; }
    public int? RateLimitRpd { get; set; }
}
