using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// DTO for requesting updates to an existing virtual key group
    /// </summary>
    public class UpdateVirtualKeyGroupRequestDto
    {
        /// <summary>
        /// Human-readable name for the group
        /// </summary>
        [MaxLength(200, ErrorMessage = "Group name cannot exceed 200 characters.")]
        public string? GroupName { get; set; }

        /// <summary>
        /// External identifier for integration with external systems
        /// </summary>
        [MaxLength(100, ErrorMessage = "External group ID cannot exceed 100 characters.")]
        public string? ExternalGroupId { get; set; }
    }
}