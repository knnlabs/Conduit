using System.ComponentModel.DataAnnotations;
using ConduitLLM.Configuration.Constants;

namespace ConduitLLM.Configuration.DTOs.IpFilter;

/// <summary>
/// Data Transfer Object for creating a new IP filter
/// </summary>
public class CreateIpFilterDto
{
    /// <summary>
    /// Type of the IP filter (whitelist or blacklist)
    /// </summary>
    [Required]
    [StringLength(10)]
    public string FilterType { get; set; } = IpFilterConstants.BLACKLIST;
    
    /// <summary>
    /// The IP address or subnet in CIDR notation (e.g., "192.168.1.1" or "192.168.1.0/24")
    /// </summary>
    [Required]
    [StringLength(50)]
    public string IpAddressOrCidr { get; set; } = string.Empty;

    /// <summary>
    /// Alias for IpAddressOrCidr for backward compatibility
    /// </summary>
    public string IpAddress
    {
        get => IpAddressOrCidr;
        set => IpAddressOrCidr = value;
    }

    /// <summary>
    /// Name of the IP filter rule
    /// </summary>
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description of the filter
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Whether the filter is currently active
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}