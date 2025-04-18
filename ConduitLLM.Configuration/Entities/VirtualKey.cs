using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities;

/// <summary>
/// Represents a virtual API key for accessing LLM services
/// </summary>
public class VirtualKey
{
    /// <summary>
    /// Unique identifier for the virtual key
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Name of the virtual key
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// The hash of the key value used for authentication
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the key
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether the key is currently active
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The maximum spending budget for this key
    /// </summary>
    [Column(TypeName = "decimal(18, 8)")]
    public decimal? MaxBudget { get; set; }

    /// <summary>
    /// The current accumulated spend for this key
    /// </summary>
    [Column(TypeName = "decimal(18, 8)")]
    public decimal CurrentSpend { get; set; } = 0;

    /// <summary>
    /// The budget period for this key (daily, monthly, etc.)
    /// </summary>
    [MaxLength(20)]
    public string? BudgetDuration { get; set; }

    /// <summary>
    /// The start date of the current budget period
    /// </summary>
    public DateTime? BudgetStartDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional date when the key expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Date when the key was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when the key was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional metadata associated with the key
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Optional comma-separated list of allowed models
    /// </summary>
    public string? AllowedModels { get; set; }

    /// <summary>
    /// Requests per minute rate limit for this key
    /// </summary>
    public int? RateLimitRpm { get; set; }

    /// <summary>
    /// Requests per day rate limit for this key
    /// </summary>
    public int? RateLimitRpd { get; set; }

    /// <summary>
    /// Virtual collection of request logs
    /// </summary>
    public virtual ICollection<RequestLog>? RequestLogs { get; set; }

    /// <summary>
    /// Virtual collection of spend history
    /// </summary>
    public virtual ICollection<VirtualKeySpendHistory>? SpendHistory { get; set; }

    /// <summary>
    /// Virtual collection of notifications
    /// </summary>
    public virtual ICollection<Notification>? Notifications { get; set; }

    /// <summary>
    /// Concurrency token for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
