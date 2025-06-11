using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides distributed tracing for audio operations.
    /// </summary>
    public class AudioTracingService : IAudioTracingService
    {
        private readonly ILogger<AudioTracingService> _logger;
        private readonly AudioTracingOptions _options;
        private readonly ConcurrentDictionary<string, AudioTrace> _activeTraces = new();
        private readonly ConcurrentDictionary<string, List<AudioTrace>> _completedTraces = new();
        private readonly Timer _cleanupTimer;
        private readonly ActivitySource _activitySource;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioTracingService"/> class.
        /// </summary>
        public AudioTracingService(
            ILogger<AudioTracingService> logger,
            IOptions<AudioTracingOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));

            // Initialize OpenTelemetry activity source
            _activitySource = new ActivitySource("ConduitLLM.Audio", "1.0.0");

            // Start cleanup timer
            _cleanupTimer = new Timer(
                CleanupOldTraces,
                null,
                _options.CleanupInterval,
                _options.CleanupInterval);
        }

        /// <inheritdoc />
        public IAudioTraceContext StartTrace(
            string operationName,
            AudioOperation operationType,
            Dictionary<string, string>? tags = null)
        {
            var activity = _activitySource.StartActivity(
                operationName,
                ActivityKind.Server);

            if (activity == null)
            {
                // Tracing is disabled or sampled out
                return new NoOpTraceContext();
            }

            var trace = new AudioTrace
            {
                TraceId = activity.TraceId.ToString(),
                OperationName = operationName,
                OperationType = operationType,
                StartTime = DateTime.UtcNow,
                Status = TraceStatus.Unset,
                Tags = tags ?? new Dictionary<string, string>()
            };

            // Add default tags
            trace.Tags["operation.type"] = operationType.ToString();
            trace.Tags["service.name"] = "conduit.audio";
            trace.Tags["service.version"] = "1.0.0";

            _activeTraces[trace.TraceId] = trace;

            var context = new AudioTraceContext(
                trace,
                activity,
                () => CompleteTrace(trace));

            _logger.LogDebug(
                "Started trace {TraceId} for operation {OperationName} ({OperationType})",
                trace.TraceId, operationName, operationType);

            return context;
        }

        /// <inheritdoc />
        public IAudioSpanContext CreateSpan(
            IAudioTraceContext parentContext,
            string spanName,
            Dictionary<string, string>? tags = null)
        {
            if (parentContext is NoOpTraceContext)
            {
                return new NoOpTraceContext();
            }

            var traceContext = (AudioTraceContext)parentContext;
            var parentActivity = traceContext.Activity;

            var activity = _activitySource.StartActivity(
                spanName,
                ActivityKind.Internal,
                parentActivity.Context);

            if (activity == null)
            {
                return new NoOpTraceContext();
            }

            var span = new AudioSpan
            {
                SpanId = activity.SpanId.ToString(),
                ParentSpanId = parentActivity.SpanId.ToString(),
                Name = spanName,
                StartTime = DateTime.UtcNow,
                Status = TraceStatus.Unset,
                Tags = tags ?? new Dictionary<string, string>()
            };

            // Add to parent trace
            if (_activeTraces.TryGetValue(traceContext.TraceId, out var trace))
            {
                trace.Spans.Add(span);
            }

            var spanContext = new AudioSpanContext(
                span,
                activity,
                traceContext.TraceId,
                parentActivity.SpanId.ToString(),
                () => CompleteSpan(span));

            _logger.LogDebug(
                "Created span {SpanId} under trace {TraceId}",
                span.SpanId, traceContext.TraceId);

            return spanContext;
        }

        /// <inheritdoc />
        public Task<AudioTrace?> GetTraceAsync(string traceId)
        {
            if (_activeTraces.TryGetValue(traceId, out var activeTrace))
            {
                return Task.FromResult<AudioTrace?>(CloneTrace(activeTrace));
            }

            if (_completedTraces.TryGetValue(traceId, out var completedList))
            {
                var trace = completedList.FirstOrDefault();
                return Task.FromResult<AudioTrace?>(trace != null ? CloneTrace(trace) : null);
            }

            return Task.FromResult<AudioTrace?>(null);
        }

        /// <inheritdoc />
        public Task<List<AudioTrace>> SearchTracesAsync(TraceSearchQuery query)
        {
            var allTraces = _completedTraces.Values
                .SelectMany(list => list)
                .Concat(_activeTraces.Values)
                .Where(t => MatchesQuery(t, query))
                .OrderByDescending(t => t.StartTime)
                .Take(query.MaxResults)
                .Select(CloneTrace)
                .ToList();

            return Task.FromResult(allTraces);
        }

        /// <inheritdoc />
        public Task<TraceStatistics> GetStatisticsAsync(
            DateTime startTime,
            DateTime endTime)
        {
            var relevantTraces = _completedTraces.Values
                .SelectMany(list => list)
                .Where(t => t.StartTime >= startTime && t.StartTime <= endTime)
                .ToList();

            var statistics = new TraceStatistics
            {
                TotalTraces = relevantTraces.Count,
                SuccessfulTraces = relevantTraces.Count(t => t.Status == TraceStatus.Ok),
                FailedTraces = relevantTraces.Count(t => t.Status == TraceStatus.Error)
            };

            if (relevantTraces.Any())
            {
                var durations = relevantTraces
                    .Where(t => t.DurationMs.HasValue)
                    .Select(t => t.DurationMs!.Value)
                    .OrderBy(d => d)
                    .ToList();

                if (durations.Any())
                {
                    statistics.AverageDurationMs = durations.Average();
                    statistics.P95DurationMs = GetPercentile(durations, 0.95);
                    statistics.P99DurationMs = GetPercentile(durations, 0.99);
                }
            }

            // Operation breakdown
            statistics.OperationBreakdown = relevantTraces
                .GroupBy(t => t.OperationType)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            // Provider breakdown
            statistics.ProviderBreakdown = relevantTraces
                .Where(t => !string.IsNullOrEmpty(t.Provider))
                .GroupBy(t => t.Provider!)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            // Error breakdown
            statistics.ErrorBreakdown = relevantTraces
                .Where(t => t.Error != null)
                .GroupBy(t => t.Error!.Type)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            // Timeline
            statistics.Timeline = GenerateTimeline(relevantTraces, startTime, endTime);

            return Task.FromResult(statistics);
        }

        private void CompleteTrace(AudioTrace trace)
        {
            trace.EndTime = DateTime.UtcNow;
            trace.DurationMs = (trace.EndTime.Value - trace.StartTime).TotalMilliseconds;

            if (trace.Status == TraceStatus.Unset)
            {
                trace.Status = TraceStatus.Ok;
            }

            // Move from active to completed
            if (_activeTraces.TryRemove(trace.TraceId, out _))
            {
                var list = _completedTraces.GetOrAdd(trace.TraceId, _ => new List<AudioTrace>());
                lock (list)
                {
                    list.Add(trace);
                }

                _logger.LogDebug(
                    "Completed trace {TraceId} with status {Status} in {Duration}ms",
                    trace.TraceId, trace.Status, trace.DurationMs);
            }
        }

        private void CompleteSpan(AudioSpan span)
        {
            span.EndTime = DateTime.UtcNow;
            span.DurationMs = (span.EndTime.Value - span.StartTime).TotalMilliseconds;

            if (span.Status == TraceStatus.Unset)
            {
                span.Status = TraceStatus.Ok;
            }
        }

        private bool MatchesQuery(AudioTrace trace, TraceSearchQuery query)
        {
            if (query.StartTime.HasValue && trace.StartTime < query.StartTime.Value)
                return false;

            if (query.EndTime.HasValue && trace.StartTime > query.EndTime.Value)
                return false;

            if (query.OperationType.HasValue && trace.OperationType != query.OperationType.Value)
                return false;

            if (query.Status.HasValue && trace.Status != query.Status.Value)
                return false;

            if (!string.IsNullOrEmpty(query.Provider) && trace.Provider != query.Provider)
                return false;

            if (!string.IsNullOrEmpty(query.VirtualKey) && trace.VirtualKey != query.VirtualKey)
                return false;

            if (query.MinDurationMs.HasValue && (!trace.DurationMs.HasValue || trace.DurationMs.Value < query.MinDurationMs.Value))
                return false;

            if (query.MaxDurationMs.HasValue && (!trace.DurationMs.HasValue || trace.DurationMs.Value > query.MaxDurationMs.Value))
                return false;

            if (query.TagFilters.Any())
            {
                foreach (var filter in query.TagFilters)
                {
                    if (!trace.Tags.TryGetValue(filter.Key, out var value) || value != filter.Value)
                        return false;
                }
            }

            return true;
        }

        private AudioTrace CloneTrace(AudioTrace trace)
        {
            return new AudioTrace
            {
                TraceId = trace.TraceId,
                OperationName = trace.OperationName,
                OperationType = trace.OperationType,
                StartTime = trace.StartTime,
                EndTime = trace.EndTime,
                DurationMs = trace.DurationMs,
                Status = trace.Status,
                StatusDescription = trace.StatusDescription,
                Tags = new Dictionary<string, string>(trace.Tags),
                Spans = trace.Spans.Select(CloneSpan).ToList(),
                Events = trace.Events.Select(CloneEvent).ToList(),
                VirtualKey = trace.VirtualKey,
                Provider = trace.Provider,
                Error = trace.Error != null ? CloneError(trace.Error) : null
            };
        }

        private AudioSpan CloneSpan(AudioSpan span)
        {
            return new AudioSpan
            {
                SpanId = span.SpanId,
                ParentSpanId = span.ParentSpanId,
                Name = span.Name,
                StartTime = span.StartTime,
                EndTime = span.EndTime,
                DurationMs = span.DurationMs,
                Tags = new Dictionary<string, string>(span.Tags),
                Events = span.Events.Select(CloneEvent).ToList(),
                Status = span.Status
            };
        }

        private TraceEvent CloneEvent(TraceEvent evt)
        {
            return new TraceEvent
            {
                Name = evt.Name,
                Timestamp = evt.Timestamp,
                Attributes = new Dictionary<string, object>(evt.Attributes)
            };
        }

        private TraceError CloneError(TraceError error)
        {
            return new TraceError
            {
                Type = error.Type,
                Message = error.Message,
                StackTrace = error.StackTrace,
                Timestamp = error.Timestamp
            };
        }

        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (!sortedValues.Any()) return 0;

            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }

        private List<TraceTimelinePoint> GenerateTimeline(
            List<AudioTrace> traces,
            DateTime startTime,
            DateTime endTime)
        {
            var timeline = new List<TraceTimelinePoint>();
            var interval = TimeSpan.FromMinutes(5);

            for (var timestamp = startTime; timestamp <= endTime; timestamp = timestamp.Add(interval))
            {
                var windowEnd = timestamp.Add(interval);
                var windowTraces = traces
                    .Where(t => t.StartTime >= timestamp && t.StartTime < windowEnd)
                    .ToList();

                if (windowTraces.Any())
                {
                    timeline.Add(new TraceTimelinePoint
                    {
                        Timestamp = timestamp,
                        TraceCount = windowTraces.Count,
                        ErrorCount = windowTraces.Count(t => t.Status == TraceStatus.Error),
                        AverageDurationMs = windowTraces
                            .Where(t => t.DurationMs.HasValue)
                            .Select(t => t.DurationMs!.Value)
                            .DefaultIfEmpty(0)
                            .Average()
                    });
                }
            }

            return timeline;
        }

        private void CleanupOldTraces(object? state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.Subtract(_options.RetentionPeriod);

                // Clean up completed traces
                foreach (var kvp in _completedTraces.ToList())
                {
                    lock (kvp.Value)
                    {
                        kvp.Value.RemoveAll(t => t.StartTime < cutoff);
                        if (!kvp.Value.Any())
                        {
                            _completedTraces.TryRemove(kvp.Key, out _);
                        }
                    }
                }

                // Clean up stale active traces
                var staleTraces = _activeTraces.Values
                    .Where(t => t.StartTime < cutoff.AddHours(-1))
                    .ToList();

                foreach (var trace in staleTraces)
                {
                    trace.Status = TraceStatus.Error;
                    trace.StatusDescription = "Trace timed out";
                    CompleteTrace(trace);
                }

                _logger.LogDebug("Cleaned up traces older than {Cutoff}", cutoff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during trace cleanup");
            }
        }

        /// <summary>
        /// Disposes the tracing service.
        /// </summary>
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _activitySource?.Dispose();
        }
    }

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
