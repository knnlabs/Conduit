using ConduitLLM.Configuration.Enums;

namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// DTO representing a virtual key group transaction
    /// </summary>
    public class VirtualKeyGroupTransactionDto
    {
        /// <summary>
        /// Transaction ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Virtual key group ID
        /// </summary>
        public int VirtualKeyGroupId { get; set; }

        /// <summary>
        /// Type of transaction
        /// </summary>
        public TransactionType TransactionType { get; set; }

        /// <summary>
        /// Transaction amount (always positive)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Balance after this transaction
        /// </summary>
        public decimal BalanceAfter { get; set; }

        /// <summary>
        /// Type of reference that triggered this transaction
        /// </summary>
        public ReferenceType ReferenceType { get; set; }

        /// <summary>
        /// Reference ID
        /// </summary>
        public string? ReferenceId { get; set; }

        /// <summary>
        /// Transaction description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// What initiated this transaction
        /// </summary>
        public string InitiatedBy { get; set; } = "System";

        /// <summary>
        /// User ID if initiated by an admin
        /// </summary>
        public string? InitiatedByUserId { get; set; }

        /// <summary>
        /// When the transaction was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}