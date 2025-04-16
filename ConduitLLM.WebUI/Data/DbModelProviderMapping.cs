using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.WebUI.Data;

/// <summary>
/// Represents the database entity for mapping a model alias to a provider and provider-specific model ID.
/// </summary>
public class DbModelProviderMapping
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// User-defined alias for the model (e.g., "gpt-4-turbo").
    /// Should be unique within the configuration.
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string ModelAlias { get; set; }

    /// <summary>
    /// Name of the provider configured in DbProviderCredentials (e.g., "openai", "anthropic").
    /// Links this mapping to the correct credentials.
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string ProviderName { get; set; }

    /// <summary>
    /// The actual model ID expected by the target provider (e.g., "gpt-4-turbo-preview").
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string ProviderModelId { get; set; }

    // Optional overrides (as comments in original class) could be added here if needed later.
    // [StringLength(500)]
    // public string? ApiKeyOverride { get; set; }
    // [StringLength(255)]
    // public string? ApiBaseOverride { get; set; }
}
