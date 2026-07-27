using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConduitLLM.Configuration.DTOs.BatchOperations
{
    /// <summary>
    /// Individual virtual key update
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class VirtualKeyUpdateDto
    {
        /// <summary>
        /// Virtual key ID to update
        /// </summary>
        [Required]
        public int VirtualKeyId { get; set; }

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
