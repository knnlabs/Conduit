using System.Diagnostics;

using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implementation of audio trace context.
    /// </summary>
    internal class AudioTraceContext : IAudioTraceContext
    {
        private readonly AudioTrace _trace;
        private readonly Action _onDispose;

        public Activity Activity { get; }
        public string TraceId => _trace.TraceId;
        public string SpanId => Activity.SpanId.ToString();

        public AudioTraceContext(AudioTrace trace, Activity activity, Action onDispose)
        {
            _trace = trace;
            Activity = activity;
            _onDispose = onDispose;
        }

        public void AddTag(string key, string value)
        {
            _trace.Tags[key] = value;
            Activity.SetTag(key, value);
        }

        public void AddEvent(string eventName, Dictionary<string, object>? attributes = null)
        {
            var evt = new TraceEvent
            {
                Name = eventName,
                Timestamp = DateTime.UtcNow,
                Attributes = attributes ?? new Dictionary<string, object>()
            };

            _trace.Events.Add(evt);

            var activityEvent = new ActivityEvent(
                eventName,
                DateTimeOffset.UtcNow,
                new ActivityTagsCollection(evt.Attributes.Select(kvp =>
                    new KeyValuePair<string, object?>(kvp.Key, kvp.Value))));

            Activity.AddEvent(activityEvent);
        }

        public void SetStatus(TraceStatus status, string? description = null)
        {
            _trace.Status = status;
            _trace.StatusDescription = description;

            var activityStatus = status switch
            {
                TraceStatus.Ok => ActivityStatusCode.Ok,
                TraceStatus.Error => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset
            };

            Activity.SetStatus(activityStatus, description);
        }

        public void RecordException(Exception exception)
        {
            _trace.Error = new TraceError
            {
                Type = exception.GetType().FullName ?? "Unknown",
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                Timestamp = DateTime.UtcNow
            };

            AddEvent("exception", new Dictionary<string, object>
            {
                ["exception.type"] = _trace.Error.Type,
                ["exception.message"] = _trace.Error.Message,
                ["exception.stacktrace"] = _trace.Error.StackTrace ?? string.Empty
            });

            SetStatus(TraceStatus.Error, exception.Message);
        }

        public Dictionary<string, string> GetPropagationHeaders()
        {
            var headers = new Dictionary<string, string>();

            // W3C Trace Context headers
            headers["traceparent"] = $"00-{Activity.TraceId}-{Activity.SpanId}-01"; // Always set as sampled

            if (Activity.TraceStateString != null)
            {
                headers["tracestate"] = Activity.TraceStateString;
            }

            // Add correlation ID from activity baggage
            var correlationId = Activity.GetBaggageItem("correlation.id");
            if (!string.IsNullOrEmpty(correlationId))
            {
                headers["X-Correlation-ID"] = correlationId;
                headers["X-Request-ID"] = correlationId;
            }

            // Add any other context baggage items
            foreach (var baggage in Activity.Baggage)
            {
                if (baggage.Key.StartsWith("context.") && !string.IsNullOrEmpty(baggage.Value))
                {
                    headers[$"X-Context-{baggage.Key}"] = baggage.Value;
                }
            }

            return headers;
        }

        public void Dispose()
        {
            Activity?.Dispose();
            _onDispose();
        }
    }

    /// <summary>
    /// Implementation of audio span context.
    /// </summary>
    internal class AudioSpanContext : AudioTraceContext, IAudioSpanContext
    {
        public string? ParentSpanId { get; }

        public AudioSpanContext(
            AudioSpan span,
            Activity activity,
            string traceId,
            string parentSpanId,
            Action onDispose)
            : base(new AudioTrace
            {
                TraceId = traceId,
                Tags = span.Tags,
                Events = span.Events
            }, activity, onDispose)
        {
            ParentSpanId = parentSpanId;
        }
    }

    /// <summary>
    /// No-op trace context for when tracing is disabled.
    /// </summary>
    internal class NoOpTraceContext : IAudioSpanContext
    {
        public string TraceId => "00000000000000000000000000000000";
        public string SpanId => "0000000000000000";
        public string? ParentSpanId => null;

        public void AddTag(string key, string value) { }
        public void AddEvent(string eventName, Dictionary<string, object>? attributes = null) { }
        public void SetStatus(TraceStatus status, string? description = null) { }
        public void RecordException(Exception exception) { }
        public Dictionary<string, string> GetPropagationHeaders() => new();
        public void Dispose() { }
    }

    /// <summary>
    /// Options for audio tracing service.
    /// </summary>
    public class AudioTracingOptions
    {
        /// <summary>
        /// Gets or sets the trace retention period.
        /// </summary>
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Gets or sets the cleanup interval.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets the sampling rate (0.0 to 1.0).
        /// </summary>
        public double SamplingRate { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets whether to export traces to external systems.
        /// </summary>
        public bool EnableExport { get; set; } = true;
    }
}