using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.WebUI.Data;

/// <summary>
/// Represents the database entity for storing LLM provider credentials.
/// </summary>
public class DbProviderCredentials
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Friendly display name for the provider
    /// </summary>
    [StringLength(100)]
    public string? Name { get; set; }

    /// <summary>
    /// Unique name identifying the provider (e.g., "openai", "anthropic").
    /// Used as the link from DbModelProviderMapping.
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string ProviderName { get; set; }

    /// <summary>
    /// API key for the provider. Stored potentially encrypted or handled securely.
    /// For simplicity here, stored as plain text. Consider encryption in production.
    /// </summary>
    [StringLength(500)] // Increased length for potential future needs
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for the provider's API endpoint.
    /// </summary>
    [StringLength(255)]
    public string? ApiBase { get; set; }

    /// <summary>
    /// API version, relevant for providers like Azure OpenAI.
    /// </summary>
    [StringLength(50)]
    public string? ApiVersion { get; set; }

    // Potential future extension:
    // public Dictionary<string, string>? AdditionalSettings { get; set; }
    // EF Core requires specific handling for dictionaries, e.g., JSON serialization or separate table.
}
