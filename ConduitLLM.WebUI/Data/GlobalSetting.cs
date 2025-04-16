using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.WebUI.Data;

/// <summary>
/// Represents a global key-value setting stored in the database.
/// </summary>
public class GlobalSetting
{
    /// <summary>
    /// The unique key for the setting (e.g., "ConduitProxyBaseUrl").
    /// </summary>
    [Key]
    [Required]
    [MaxLength(100)] // Added reasonable max length
    public required string Key { get; set; }

    /// <summary>
    /// The value of the setting.
    /// </summary>
    [Required]
    public required string Value { get; set; }

    /// <summary>
    /// The hash of the master key.
    /// </summary>
    [NotMapped]
    public string? MasterKeyHash { get; set; }

    /// <summary>
    /// The algorithm used to generate the master key hash (e.g., "SHA256", "SHA512").
    /// </summary>
    [NotMapped]
    public string? MasterKeyHashAlgorithm { get; set; }
}
