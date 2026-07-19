namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Response returned when a media record is deleted.
    /// </summary>
    public class MediaDeletionResponseDto
    {
        /// <summary>
        /// Human-readable confirmation message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response returned by media cleanup and pruning operations.
    /// </summary>
    public class MediaCleanupResponseDto
    {
        /// <summary>
        /// Human-readable summary of the cleanup operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Number of media files deleted by the operation.
        /// </summary>
        public int DeletedCount { get; set; }
    }

    /// <summary>
    /// Response describing whether the media cleanup service is enabled.
    /// </summary>
    public class MediaCleanupEnabledDto
    {
        /// <summary>
        /// Whether the media cleanup service is currently enabled.
        /// </summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Response returned after enabling or disabling the media cleanup service.
    /// </summary>
    public class MediaCleanupEnabledChangedDto
    {
        /// <summary>
        /// The new enabled state of the media cleanup service.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Human-readable confirmation of the state change.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
