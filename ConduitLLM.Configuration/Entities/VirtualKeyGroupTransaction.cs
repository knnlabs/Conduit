using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ConduitLLM.Configuration.Enums;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Represents a transaction that modifies a virtual key group's balance
    /// </summary>
    public class VirtualKeyGroupTransaction
    {
        /// <summary>
        /// Primary key
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// The virtual key group this transaction belongs to
        /// </summary>
        public int VirtualKeyGroupId { get; set; }

        /// <summary>
        /// Navigation property to the virtual key group
        /// </summary>
        public virtual VirtualKeyGroup VirtualKeyGroup { get; set; } = null!;

        /// <summary>
        /// Type of transaction
        /// </summary>
        public TransactionType TransactionType { get; set; }

        /// <summary>
        /// Transaction amount (always positive, type determines if it's added or subtracted)
        /// </summary>
        [Column(TypeName = "decimal(18, 6)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Balance after this transaction
        /// </summary>
        [Column(TypeName = "decimal(18, 6)")]
        public decimal BalanceAfter { get; set; }

        /// <summary>
        /// Type of reference that triggered this transaction
        /// </summary>
        public ReferenceType ReferenceType { get; set; }

        /// <summary>
        /// Reference ID (e.g., virtual key ID, batch operation ID)
        /// </summary>
        [MaxLength(100)]
        public string? ReferenceId { get; set; }

        /// <summary>
        /// Description of the transaction
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// What initiated this transaction (e.g., "System", "Admin", "API")
        /// </summary>
        [MaxLength(50)]
        public string InitiatedBy { get; set; } = "System";

        /// <summary>
        /// Clerk user ID if initiated by an admin user
        /// </summary>
        [MaxLength(100)]
        public string? InitiatedByUserId { get; set; }

        /// <summary>
        /// When this transaction was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this transaction has been soft deleted
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// When this transaction was deleted (if applicable)
        /// </summary>
        public DateTime? DeletedAt { get; set; }
    }
}