namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Base class for async task events.
    /// </summary>
    public abstract class AsyncTaskEvent
    {
        /// <summary>
        /// Gets or sets the task ID.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the correlation ID for tracking related events.
        /// </summary>
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets when the event occurred.
        /// </summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event published when an async task is created.
    /// </summary>
    public class AsyncTaskCreated : AsyncTaskEvent
    {
        /// <summary>
        /// Gets or sets the task type.
        /// </summary>
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int VirtualKeyId { get; set; }
    }

    /// <summary>
    /// Event published when an async task is updated.
    /// </summary>
    public class AsyncTaskUpdated : AsyncTaskEvent
    {
        /// <summary>
        /// Gets or sets the new state.
        /// </summary>
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the progress percentage.
        /// </summary>
        public int? Progress { get; set; }

        /// <summary>
        /// Gets or sets whether the task is completed.
        /// </summary>
        public bool IsCompleted { get; set; }
    }

    /// <summary>
    /// Event published when an async task is deleted.
    /// </summary>
    public class AsyncTaskDeleted : AsyncTaskEvent
    {
    }

    /// <summary>
    /// Event published when async tasks are archived.
    /// </summary>
    public class AsyncTasksArchived : AsyncTaskEvent
    {
        /// <summary>
        /// Gets or sets the number of tasks archived.
        /// </summary>
        public int TaskCount { get; set; }

        /// <summary>
        /// Gets or sets the age threshold used for archival.
        /// </summary>
        public TimeSpan OlderThan { get; set; }
    }
}