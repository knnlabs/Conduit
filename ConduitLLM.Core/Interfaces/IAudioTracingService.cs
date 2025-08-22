namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for distributed tracing of audio operations.
    /// </summary>
    public interface IAudioTracingService
    {
        /// <summary>
        /// Starts a new trace for an audio operation.
        /// </summary>
        /// <param name="operationName">The operation name.</param>
        /// <param name="operationType">The operation type.</param>
        /// <param name="tags">Optional tags.</param>
        /// <returns>The trace context.</returns>
        IAudioTraceContext StartTrace(
            string operationName,
            AudioOperation operationType,
            Dictionary<string, string>? tags = null);

        /// <summary>
        /// Creates a child span within an existing trace.
        /// </summary>
        /// <param name="parentContext">The parent trace context.</param>
        /// <param name="spanName">The span name.</param>
        /// <param name="tags">Optional tags.</param>
        /// <returns>The span context.</returns>
        IAudioSpanContext CreateSpan(
            IAudioTraceContext parentContext,
            string spanName,
            Dictionary<string, string>? tags = null);

        /// <summary>
        /// Gets a trace by ID.
        /// </summary>
        /// <param name="traceId">The trace ID.</param>
        /// <returns>The trace details.</returns>
        Task<AudioTrace?> GetTraceAsync(string traceId);

        /// <summary>
        /// Searches for traces.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <returns>List of matching traces.</returns>
        Task<List<AudioTrace>> SearchTracesAsync(TraceSearchQuery query);

        /// <summary>
        /// Gets trace statistics.
        /// </summary>
        /// <param name="startTime">Start time.</param>
        /// <param name="endTime">End time.</param>
        /// <returns>Trace statistics.</returns>
        Task<TraceStatistics> GetStatisticsAsync(
            DateTime startTime,
            DateTime endTime);
    }

    /// <summary>
    /// Audio trace context.
    /// </summary>
    public interface IAudioTraceContext : IDisposable
    {
        /// <summary>
        /// Gets the trace ID.
        /// </summary>
        string TraceId { get; }

        /// <summary>
        /// Gets the span ID.
        /// </summary>
        string SpanId { get; }

        /// <summary>
        /// Adds a tag to the trace.
        /// </summary>
        /// <param name="key">Tag key.</param>
        /// <param name="value">Tag value.</param>
        void AddTag(string key, string value);

        /// <summary>
        /// Adds an event to the trace.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="attributes">Event attributes.</param>
        void AddEvent(string eventName, Dictionary<string, object>? attributes = null);

        /// <summary>
        /// Sets the status of the trace.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <param name="description">Optional description.</param>
        void SetStatus(TraceStatus status, string? description = null);

        /// <summary>
        /// Records an exception.
        /// </summary>
        /// <param name="exception">The exception.</param>
        void RecordException(Exception exception);

        /// <summary>
        /// Gets the trace propagation headers.
        /// </summary>
        /// <returns>Headers for trace propagation.</returns>
        Dictionary<string, string> GetPropagationHeaders();
    }

    /// <summary>
    /// Audio span context.
    /// </summary>
    public interface IAudioSpanContext : IAudioTraceContext
    {
        /// <summary>
        /// Gets the parent span ID.
        /// </summary>
        string? ParentSpanId { get; }
    }

    /// <summary>
    /// Audio trace details.
    /// </summary>
    public class AudioTrace
    {
        /// <summary>
        /// Gets or sets the trace ID.
        /// </summary>
        public string TraceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation name.
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public AudioOperation OperationType { get; set; }

        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets or sets the duration in milliseconds.
        /// </summary>
        public double? DurationMs { get; set; }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public TraceStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the status description.
        /// </summary>
        public string? StatusDescription { get; set; }

        /// <summary>
        /// Gets or sets the tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>
        /// Gets or sets the spans.
        /// </summary>
        public List<AudioSpan> Spans { get; set; } = new();

        /// <summary>
        /// Gets or sets the events.
        /// </summary>
        public List<TraceEvent> Events { get; set; } = new();

        /// <summary>
        /// Gets or sets the virtual key.
        /// </summary>
        public string? VirtualKey { get; set; }

        /// <summary>
        /// Gets or sets the provider used.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets error information.
        /// </summary>
        public TraceError? Error { get; set; }
    }

    /// <summary>
    /// Audio span within a trace.
    /// </summary>
    public class AudioSpan
    {
        /// <summary>
        /// Gets or sets the span ID.
        /// </summary>
        public string SpanId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parent span ID.
        /// </summary>
        public string? ParentSpanId { get; set; }

        /// <summary>
        /// Gets or sets the span name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets or sets the duration in milliseconds.
        /// </summary>
        public double? DurationMs { get; set; }

        /// <summary>
        /// Gets or sets the tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>
        /// Gets or sets the events.
        /// </summary>
        public List<TraceEvent> Events { get; set; } = new();

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public TraceStatus Status { get; set; }
    }

    /// <summary>
    /// Trace event.
    /// </summary>
    public class TraceEvent
    {
        /// <summary>
        /// Gets or sets the event name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the attributes.
        /// </summary>
        public Dictionary<string, object> Attributes { get; set; } = new();
    }

    /// <summary>
    /// Trace error information.
    /// </summary>
    public class TraceError
    {
        /// <summary>
        /// Gets or sets the error type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stack trace.
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Gets or sets when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Trace status.
    /// </summary>
    public enum TraceStatus
    {
        /// <summary>
        /// Unset status.
        /// </summary>
        Unset,

        /// <summary>
        /// Operation succeeded.
        /// </summary>
        Ok,

        /// <summary>
        /// Operation failed.
        /// </summary>
        Error
    }

    /// <summary>
    /// Trace search query.
    /// </summary>
    public class TraceSearchQuery
    {
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets or sets the operation type filter.
        /// </summary>
        public AudioOperation? OperationType { get; set; }

        /// <summary>
        /// Gets or sets the status filter.
        /// </summary>
        public TraceStatus? Status { get; set; }

        /// <summary>
        /// Gets or sets the provider filter.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the virtual key filter.
        /// </summary>
        public string? VirtualKey { get; set; }

        /// <summary>
        /// Gets or sets the minimum duration in ms.
        /// </summary>
        public double? MinDurationMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum duration in ms.
        /// </summary>
        public double? MaxDurationMs { get; set; }

        /// <summary>
        /// Gets or sets tag filters.
        /// </summary>
        public Dictionary<string, string> TagFilters { get; set; } = new();

        /// <summary>
        /// Gets or sets the maximum results.
        /// </summary>
        public int MaxResults { get; set; } = 100;
    }

    /// <summary>
    /// Trace statistics.
    /// </summary>
    public class TraceStatistics
    {
        /// <summary>
        /// Gets or sets the total traces.
        /// </summary>
        public long TotalTraces { get; set; }

        /// <summary>
        /// Gets or sets successful traces.
        /// </summary>
        public long SuccessfulTraces { get; set; }

        /// <summary>
        /// Gets or sets failed traces.
        /// </summary>
        public long FailedTraces { get; set; }

        /// <summary>
        /// Gets or sets average duration.
        /// </summary>
        public double AverageDurationMs { get; set; }

        /// <summary>
        /// Gets or sets P95 duration.
        /// </summary>
        public double P95DurationMs { get; set; }

        /// <summary>
        /// Gets or sets P99 duration.
        /// </summary>
        public double P99DurationMs { get; set; }

        /// <summary>
        /// Gets or sets operation breakdown.
        /// </summary>
        public Dictionary<AudioOperation, long> OperationBreakdown { get; set; } = new();

        /// <summary>
        /// Gets or sets provider breakdown.
        /// </summary>
        public Dictionary<string, long> ProviderBreakdown { get; set; } = new();

        /// <summary>
        /// Gets or sets error breakdown.
        /// </summary>
        public Dictionary<string, long> ErrorBreakdown { get; set; } = new();

        /// <summary>
        /// Gets or sets trace timeline.
        /// </summary>
        public List<TraceTimelinePoint> Timeline { get; set; } = new();
    }

    /// <summary>
    /// Point in trace timeline.
    /// </summary>
    public class TraceTimelinePoint
    {
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the trace count.
        /// </summary>
        public int TraceCount { get; set; }

        /// <summary>
        /// Gets or sets the error count.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the average duration.
        /// </summary>
        public double AverageDurationMs { get; set; }
    }
}
