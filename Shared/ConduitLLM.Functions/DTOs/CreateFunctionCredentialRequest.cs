using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// Request DTO for creating a new function credential
/// Aligns with CreateProviderKeyCredentialDto pattern
/// </summary>
public class CreateFunctionCredentialRequest
{
    /// <summary>
    /// API key for authentication
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string ApiKey { get; set; }

    /// <summary>
    /// Optional base URL override (overrides FunctionConfiguration.BaseUrl if set)
    /// </summary>
    [MaxLength(500)]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Optional organization identifier (if applicable to provider)
    /// </summary>
    [MaxLength(200)]
    public string? Organization { get; set; }

    /// <summary>
    /// Function account group for load balancing and failover (0-32)
    /// Renamed from CredentialGroup to align with ProviderAccountGroup pattern
    /// Credentials with the same group number share the same external account/quota
    /// </summary>
    [Range(0, 32)]
    public short FunctionAccountGroup { get; set; } = 0;

    /// <summary>
    /// Whether to set this credential as the primary credential
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// Whether this credential is enabled (default: true)
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Optional human-readable name for this credential (e.g., "Production Key 1")
    /// Renamed from CredentialName to align with KeyName pattern
    /// </summary>
    [MaxLength(200)]
    public string? KeyName { get; set; }
}
