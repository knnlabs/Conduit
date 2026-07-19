namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// DTO for the result of clearing errors for a key
    /// </summary>
    public class ClearKeyErrorsResponseDto
    {
        /// <summary>
        /// Human-readable result message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// ID of the key whose errors were cleared
        /// </summary>
        public int KeyId { get; set; }

        /// <summary>
        /// Whether the key was re-enabled as part of the operation
        /// </summary>
        public bool Reenabled { get; set; }
    }

    /// <summary>
    /// DTO for the result of manually disabling a key
    /// </summary>
    public class DisableKeyResponseDto
    {
        /// <summary>
        /// Human-readable result message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// ID of the key that was disabled
        /// </summary>
        public int KeyId { get; set; }
    }
}
