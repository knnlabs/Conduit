namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// DTO for function credential information
/// API key is masked for security
/// Aligns with ProviderKeyCredentialDto pattern
/// </summary>
public class FunctionCredentialDto
{
    /// <summary>
    /// Unique identifier for the credential
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The function configuration ID this credential belongs to
    /// </summary>
    public int FunctionConfigurationId { get; set; }

    /// <summary>
    /// Masked API key (always masked in responses for security)
    /// </summary>
    public string? MaskedApiKey { get; set; }

    /// <summary>
    /// Base URL for the function provider API (optional, overrides configuration default)
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Organization or project ID (optional, overrides configuration default)
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// The function account group (0-32)
    /// Renamed from CredentialGroup to align with ProviderAccountGroup
    /// </summary>
    public short FunctionAccountGroup { get; set; }

    /// <summary>
    /// Whether this credential is the primary credential for the configuration
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Whether this credential is enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Optional human-readable name for this credential
    /// Renamed from CredentialName to align with KeyName pattern
    /// </summary>
    public string? KeyName { get; set; }

    /// <summary>
    /// Date when the credential was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date when the credential was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
