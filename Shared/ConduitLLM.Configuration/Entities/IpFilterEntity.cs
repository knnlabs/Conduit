using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Entities;

/// <summary>
/// Represents an IP address or subnet filter used for API access control
/// </summary>
public class IpFilterEntity
{
    /// <summary>
    /// Unique identifier for the IP filter
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Type of the IP filter (whitelist or blacklist)
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string FilterType { get; set; } = "blacklist";

    /// <summary>
    /// The IP address or subnet in CIDR notation (e.g., "192.168.1.1" or "192.168.1.0/24")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string IpAddressOrCidr { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the filter
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether the filter is currently active
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Date when the filter was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when the filter was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Concurrency token for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
