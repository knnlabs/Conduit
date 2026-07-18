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
}
