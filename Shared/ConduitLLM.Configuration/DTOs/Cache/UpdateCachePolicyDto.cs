using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for updating cache policy
    /// </summary>
    public class UpdateCachePolicyDto
    {
        /// <summary>
        /// New TTL in seconds
        /// </summary>
        [Range(0, 86400, ErrorMessage = "TTL must be between 0 and 86400 seconds")]
        public int? TTL { get; set; }

        /// <summary>
        /// New maximum size
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Max size must be greater than 0")]
        public int? MaxSize { get; set; }

        /// <summary>
        /// New eviction strategy
        /// </summary>
        [RegularExpression("^(LRU|LFU|FIFO|Random|Priority|TTL)$", ErrorMessage = "Invalid eviction strategy")]
        public string? Strategy { get; set; }

        /// <summary>
        /// Enable or disable the policy
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Reason for the policy change
        /// </summary>
        [Required]
        [StringLength(500, ErrorMessage = "Reason must not exceed 500 characters")]
        public string Reason { get; set; } = string.Empty;
    }
}