using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.VirtualKey;

/// <summary>
/// Data Transfer Object representing a Virtual Key for API responses, excluding sensitive hash.
/// </summary>
public class VirtualKeyDto
{
    /// <summary>
    /// Unique identifier for the virtual key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Human-readable name for the virtual key.
    /// </summary>
    [Required]
    public string KeyName { get; set; } = string.Empty;

    // Note: KeyHash is intentionally excluded for security reasons

    /// <summary>
    /// A prefix of the virtual key (e.g., "condt_...") for display purposes. 
    /// The full key is not exposed after creation.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Comma-separated list of model IDs that this key is allowed to access.
    /// Empty or null means all models are allowed.
    /// </summary>
    public string? AllowedModels { get; set; }

    /// <summary>
    /// ID of the virtual key group this key belongs to.
    /// </summary>
    public int VirtualKeyGroupId { get; set; }

    /// <summary>
    /// Indicates whether the key is currently active and can be used for API calls.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Optional expiration date for the key.
    /// After this date, the key will no longer be valid even if IsEnabled is true.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Date and time when the key was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date and time when the key was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Optional JSON-formatted metadata associated with this key.
    /// Can be used to store additional information about the key's purpose or owner.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Optional rate limit in requests per minute.
    /// </summary>
    public int? RateLimitRpm { get; set; }

    /// <summary>
    /// Optional rate limit in requests per day.
    /// </summary>
    public int? RateLimitRpd { get; set; }

    /// <summary>
    /// Optional description of the key's purpose
    /// </summary>
    public string? Description { get; set; }

}
