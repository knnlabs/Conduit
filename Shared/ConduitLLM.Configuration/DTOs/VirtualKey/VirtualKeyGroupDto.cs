namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Data Transfer Object representing a Virtual Key Group
    /// </summary>
    public class VirtualKeyGroupDto
    {
        /// <summary>
        /// Unique identifier for the virtual key group
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// External identifier for integration with external systems
        /// </summary>
        public string? ExternalGroupId { get; set; }

        /// <summary>
        /// Human-readable name for the group
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Current balance available in the group (in USD)
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// Total credits ever added to this group
        /// </summary>
        public decimal LifetimeCreditsAdded { get; set; }

        /// <summary>
        /// Total amount spent from this group
        /// </summary>
        public decimal LifetimeSpent { get; set; }

        /// <summary>
        /// Date and time when the group was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Date and time when the group was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Number of virtual keys in this group
        /// </summary>
        public int VirtualKeyCount { get; set; }
    }
}