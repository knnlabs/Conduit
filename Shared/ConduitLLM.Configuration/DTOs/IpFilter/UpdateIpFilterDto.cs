using System.ComponentModel.DataAnnotations;

using ConduitLLM.Configuration.Constants;

namespace ConduitLLM.Configuration.DTOs.IpFilter;

/// <summary>
/// Data Transfer Object for updating an existing IP filter
/// </summary>
public class UpdateIpFilterDto
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int Id { get; set; }
    /// <summary>
    /// Unique identifier for the IP filter to update
    /// </summary>
    /// <summary>
    /// Type of the IP filter (whitelist or blacklist)
    /// </summary>
    [StringLength(10)]
    public string? FilterType { get; set; }

    /// <summary>
    /// The IP address or subnet in CIDR notation (e.g., "192.168.1.1" or "192.168.1.0/24")
    /// </summary>
    [StringLength(50)]
    public string? IpAddressOrCidr { get; set; }


    /// <summary>
    /// Name of the IP filter rule
    /// </summary>
    [StringLength(100)]
    public string? Name { get; set; }

    /// <summary>
    /// Optional description of the filter
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether the filter is currently active
    /// </summary>
    public bool? IsEnabled { get; set; }
}
