using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities;

/// <summary>
/// Represents a group of virtual keys that share a common balance
/// </summary>
public class VirtualKeyGroup
{
    /// <summary>
    /// Unique identifier for the virtual key group
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Optional external group identifier for integration with external systems
    /// </summary>
    [MaxLength(100)]
    public string? ExternalGroupId { get; set; }

    /// <summary>
    /// Name of the group
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Current balance available for spending (in USD)
    /// </summary>
    [Column(TypeName = "decimal(19, 8)")]
    public decimal Balance { get; set; } = 0;

    /// <summary>
    /// Total lifetime credits added to this group (in USD)
    /// </summary>
    [Column(TypeName = "decimal(19, 8)")]
    public decimal LifetimeCreditsAdded { get; set; } = 0;

    /// <summary>
    /// Total lifetime amount spent by this group (in USD)
    /// </summary>
    [Column(TypeName = "decimal(19, 8)")]
    public decimal LifetimeSpent { get; set; } = 0;

    /// <summary>
    /// Date when the group was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when the group was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID of the media retention policy for this group.
    /// Null means use the default policy.
    /// </summary>
    public int? MediaRetentionPolicyId { get; set; }

    /// <summary>
    /// Navigation property to the media retention policy.
    /// </summary>
    [ForeignKey(nameof(MediaRetentionPolicyId))]
    public virtual MediaRetentionPolicy? MediaRetentionPolicy { get; set; }

    /// <summary>
    /// Collection of virtual keys that belong to this group
    /// </summary>
    public virtual ICollection<VirtualKey> VirtualKeys { get; set; } = new List<VirtualKey>();

    /// <summary>
    /// Collection of transactions for this group
    /// </summary>
    public virtual ICollection<VirtualKeyGroupTransaction> Transactions { get; set; } = new List<VirtualKeyGroupTransaction>();

    /// <summary>
    /// Concurrency token for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}