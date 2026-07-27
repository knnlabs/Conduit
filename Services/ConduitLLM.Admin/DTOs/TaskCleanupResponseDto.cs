using System.Text.Json.Serialization;

namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Response returned when old asynchronous tasks are cleaned up.
    /// </summary>
    public class TaskCleanupResponseDto
    {
        /// <summary>
        /// Number of tasks that were cleaned up.
        /// </summary>
        [JsonPropertyName("cleaned_up")]
        public int CleanedUp { get; set; }

        [JsonPropertyName("archived")]
        public int Archived { get; set; }

        [JsonPropertyName("deleted")]
        public int Deleted { get; set; }

        /// <summary>
        /// The age threshold, in hours, used for the cleanup.
        /// </summary>
        [JsonPropertyName("older_than_hours")]
        public int OlderThanHours { get; set; }
    }
}
