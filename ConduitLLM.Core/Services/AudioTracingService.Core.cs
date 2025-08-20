using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides distributed tracing for audio operations.
    /// </summary>
    public partial class AudioTracingService : IAudioTracingService
    {
        private readonly ILogger<AudioTracingService> _logger;
        private readonly AudioTracingOptions _options;
        private readonly ConcurrentDictionary<string, AudioTrace> _activeTraces = new();
        private readonly ConcurrentDictionary<string, List<AudioTrace>> _completedTraces = new();
        private readonly Timer _cleanupTimer;
        private readonly ActivitySource _activitySource;
        private readonly ICorrelationContextService? _correlationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioTracingService"/> class.
        /// </summary>
        public AudioTracingService(
            ILogger<AudioTracingService> logger,
            IOptions<AudioTracingOptions> options,
            ICorrelationContextService? correlationService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _correlationService = correlationService;

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
            
            // Add correlation ID if available
            if (_correlationService != null)
            {
                var correlationId = _correlationService.CorrelationId;
                if (!string.IsNullOrEmpty(correlationId))
                {
                    trace.Tags["correlation.id"] = correlationId;
                    activity.SetTag("correlation.id", correlationId);
                    activity.SetBaggage("correlation.id", correlationId);
                }
            }

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

        /// <summary>
        /// Disposes the tracing service.
        /// </summary>
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _activitySource?.Dispose();
        }
    }
}