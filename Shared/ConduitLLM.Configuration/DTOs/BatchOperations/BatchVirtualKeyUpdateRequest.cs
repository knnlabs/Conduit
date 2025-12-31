using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.BatchOperations
{
    /// <summary>
    /// Request to update multiple virtual keys
    /// </summary>
    public class BatchVirtualKeyUpdateRequest
    {
        /// <summary>
        /// List of virtual key updates
        /// </summary>
        [Required]
        public List<VirtualKeyUpdateDto> Updates { get; set; } = new();
    }
}