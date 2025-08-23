using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class AudioTracingService
    {
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
            if (sortedValues.Count() == 0) return 0;

            var index = (int)Math.Ceiling(percentile * sortedValues.Count()) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count() - 1))];
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

                if (windowTraces.Count() > 0)
                {
                    timeline.Add(new TraceTimelinePoint
                    {
                        Timestamp = timestamp,
                        TraceCount = windowTraces.Count(),
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
                        if (kvp.Value.Count() == 0)
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
    }
}