using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.BatchOperations
{
    /// <summary>
    /// Individual virtual key update
    /// </summary>
    public class VirtualKeyUpdateDto
    {
        /// <summary>
        /// Virtual key ID to update
        /// </summary>
        [Required]
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// New budget limit (optional)
        /// </summary>
        [Range(0, 1000000)]
        public decimal? MaxBudget { get; set; }

        /// <summary>
        /// New allowed models list (optional)
        /// </summary>
        public List<string>? AllowedModels { get; set; }

        /// <summary>
        /// New rate limits (optional)
        /// </summary>
        public Dictionary<string, object>? RateLimits { get; set; }

        /// <summary>
        /// Enable/disable key (optional)
        /// </summary>
        public bool? IsEnabled { get; set; }

        /// <summary>
        /// New expiry date (optional)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Notes about the update
        /// </summary>
        public string? Notes { get; set; }
    }
}