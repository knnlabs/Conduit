using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ConduitLLM.Functions.Entities.Interfaces;
using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.Entities;

/// <summary>
/// Represents an API credential (key) for a function provider type.
/// Supports multiple credentials per provider type for load balancing and failover.
/// Credentials are shared across all function configurations of the same provider type.
/// </summary>
[Table("FunctionCredentials")]
public class FunctionCredential : ICredentialEntity
{
    /// <summary>
    /// Unique identifier for this credential
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// The provider type this credential belongs to (e.g., Exa, Tavily)
    /// All function configurations with this provider type can use these credentials
    /// </summary>
    [Required]
    public FunctionProviderType ProviderType { get; set; }

    /// <summary>
    /// API key for authentication (stored encrypted at rest)
    /// </summary>
    [MaxLength(500)]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional base URL override for this specific credential
    /// Overrides FunctionConfiguration.BaseUrl if set
    /// </summary>
    [MaxLength(500)]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Optional organization identifier (if applicable to provider)
    /// </summary>
    [MaxLength(200)]
    public string? Organization { get; set; }

    /// <summary>
    /// Function account group identifier (0-32) for intelligent load balancing and failover
    /// Credentials with the same group number share the same external account/quota
    /// Renamed from CredentialGroup to align with ProviderAccountGroup pattern
    /// </summary>
    [Required]
    public short FunctionAccountGroup { get; set; } = 0;

    /// <summary>
    /// Whether this is the primary credential (only one per provider type should be primary)
    /// Primary credential is used by default; non-primary are for failover
    /// </summary>
    [Required]
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// Whether this credential is enabled
    /// </summary>
    [Required]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// User-friendly name for this credential (e.g., "Production Key 1")
    /// Renamed from CredentialName to align with ProviderKeyCredential.KeyName pattern
    /// </summary>
    [MaxLength(200)]
    public string? KeyName { get; set; }

    /// <summary>
    /// When this credential was created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this credential was last updated
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
