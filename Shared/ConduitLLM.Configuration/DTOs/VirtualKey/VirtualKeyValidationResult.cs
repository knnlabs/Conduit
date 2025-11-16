namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Result of validating a virtual key
    /// </summary>
    public class VirtualKeyValidationResult
    {
        /// <summary>
        /// Whether the key is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// ID of the virtual key if valid
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Name of the virtual key
        /// </summary>
        public string? KeyName { get; set; }

        /// <summary>
        /// Models allowed for this key
        /// </summary>
        public string? AllowedModels { get; set; }

        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
