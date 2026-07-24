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

        /// <summary>
        /// Number of media files that could not be deleted and remain tracked for retry.
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Whether the operation only simulated deletion.
        /// </summary>
        public bool IsDryRun { get; set; }

        /// <summary>
        /// Number of files that matched during a dry run.
        /// </summary>
        public int WouldDeleteCount { get; set; }

        /// <summary>
        /// Bytes that would be freed during a dry run.
        /// </summary>
        public long BytesWouldFree { get; set; }

        /// <summary>
        /// Source that triggered the cleanup.
        /// </summary>
        public string TriggeredBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Non-destructive preview of a manual cleanup scope.
    /// </summary>
    public class MediaCleanupPreviewDto
    {
        public int FileCount { get; set; }
        public long SizeBytes { get; set; }
        public string ConfirmationPhrase { get; set; } = string.Empty;
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
