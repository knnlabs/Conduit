using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Request for validating a virtual key
    /// </summary>
    public class ValidateVirtualKeyRequest
    {
        /// <summary>
        /// The virtual key string to validate
        /// </summary>
        [Required]
        public string Key { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional. The model being requested, to check against allowed models.
        /// </summary>
        public string? RequestedModel { get; set; }
    }
}