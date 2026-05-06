using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace ConduitLLM.Core.Metrics
{
    /// <summary>
    /// Shared OpenTelemetry instrumentation for LLM provider clients.
    /// Provides a single ActivitySource and Meter that all <c>BaseLLMClient</c>
    /// implementations emit to, so request count, latency, error rate, token usage,
    /// and streaming behavior can be observed uniformly across providers.
    /// </summary>
    public static class ProviderInstrumentation
    {
        /// <summary>
        /// Source name used for both the <see cref="ActivitySource"/> and the <see cref="Meter"/>.
        /// Must be added to OpenTelemetry registration via <c>AddSource</c> / <c>AddMeter</c>.
        /// </summary>
        public const string SourceName = "ConduitLLM.Providers";

        private const string Version = "1.0.0";

        /// <summary>
        /// Activity source for provider request spans.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new(SourceName, Version);

        private static readonly Meter Meter = new(SourceName, Version);

        private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
            "provider.requests",
            "requests",
            "Number of provider API requests");

        private static readonly Counter<long> ErrorCounter = Meter.CreateCounter<long>(
            "provider.errors",
            "errors",
            "Number of provider API requests that failed");

        private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
            "provider.request.duration",
            "ms",
            "Wall-clock duration of provider API requests");

        private static readonly Counter<long> PromptTokens = Meter.CreateCounter<long>(
            "provider.tokens.prompt",
            "tokens",
            "Prompt tokens consumed (as reported by the provider)");

        private static readonly Counter<long> CompletionTokens = Meter.CreateCounter<long>(
            "provider.tokens.completion",
            "tokens",
            "Completion tokens produced (as reported by the provider)");

        private static readonly Counter<long> TotalTokens = Meter.CreateCounter<long>(
            "provider.tokens.total",
            "tokens",
            "Total tokens reported by the provider (prompt + completion)");

        private static readonly Counter<long> StreamChunks = Meter.CreateCounter<long>(
            "provider.stream.chunks",
            "chunks",
            "SSE chunks received from streaming provider responses");

        private static readonly Histogram<double> FirstChunkLatency = Meter.CreateHistogram<double>(
            "provider.stream.first_chunk.duration",
            "ms",
            "Time from streaming request start to first chunk received");

        private static readonly Histogram<double> PollDuration = Meter.CreateHistogram<double>(
            "provider.poll.duration",
            "ms",
            "Wall-clock duration of async-job poll loops (submit → terminal state)");

        private static readonly Histogram<long> PollAttempts = Meter.CreateHistogram<long>(
            "provider.poll.attempts",
            "attempts",
            "Poll attempts executed before an async job reached a terminal state");

        private static readonly Counter<long> PollTransientErrors = Meter.CreateCounter<long>(
            "provider.poll.transient_errors",
            "errors",
            "Transient errors encountered while polling an async job's status");

        private static readonly Counter<long> PollOutcomes = Meter.CreateCounter<long>(
            "provider.poll.outcomes",
            "outcomes",
            "Terminal outcomes of async-job poll loops, tagged by outcome");

        /// <summary>
        /// Starts a span for a non-streaming provider API request.
        /// Returned activity may be null if no listener is subscribed; callers must
        /// dispose it (typically via <c>using</c>).
        /// </summary>
        public static Activity? StartRequestActivity(
            string operation,
            string providerName,
            string providerType,
            string model)
        {
            return ActivitySource.StartActivity(
                $"provider.{operation}",
                ActivityKind.Client,
                Activity.Current?.Context ?? default,
                new TagList
                {
                    { "provider", providerName },
                    { "provider.type", providerType },
                    { "provider.model", model },
                    { "provider.operation", operation }
                });
        }

        /// <summary>
        /// Records the outcome of a non-streaming provider API request.
        /// </summary>
        public static void RecordRequest(
            string operation,
            string providerName,
            string providerType,
            string model,
            double durationMs,
            bool success,
            string? errorType = null)
        {
            var tags = new TagList
            {
                { "provider", providerName },
                { "provider.type", providerType },
                { "provider.model", model },
                { "provider.operation", operation },
                { "success", success ? "true" : "false" }
            };

            RequestCounter.Add(1, tags);
            RequestDuration.Record(durationMs, tags);

            if (!success)
            {
                var errorTags = new TagList
                {
                    { "provider", providerName },
                    { "provider.type", providerType },
                    { "provider.model", model },
                    { "provider.operation", operation },
                    { "error_type", errorType ?? "Unknown" }
                };
                ErrorCounter.Add(1, errorTags);
            }
        }

        /// <summary>
        /// Records token usage reported by the provider for a single request or stream.
        /// Safe to call with null/zero values — counters are only incremented for
        /// dimensions that are actually populated.
        /// </summary>
        public static void RecordUsage(
            string operation,
            string providerName,
            string providerType,
            string model,
            int? promptTokens,
            int? completionTokens,
            int? totalTokens)
        {
            if ((promptTokens ?? 0) <= 0 && (completionTokens ?? 0) <= 0 && (totalTokens ?? 0) <= 0)
            {
                return;
            }

            var tags = new TagList
            {
                { "provider", providerName },
                { "provider.type", providerType },
                { "provider.model", model },
                { "provider.operation", operation }
            };

            if (promptTokens > 0)
            {
                PromptTokens.Add(promptTokens.Value, tags);
            }

            if (completionTokens > 0)
            {
                CompletionTokens.Add(completionTokens.Value, tags);
            }

            if (totalTokens > 0)
            {
                TotalTokens.Add(totalTokens.Value, tags);
            }

            if (Activity.Current is { } activity)
            {
                if (promptTokens > 0) activity.SetTag("provider.usage.prompt_tokens", promptTokens.Value);
                if (completionTokens > 0) activity.SetTag("provider.usage.completion_tokens", completionTokens.Value);
                if (totalTokens > 0) activity.SetTag("provider.usage.total_tokens", totalTokens.Value);
            }
        }

        /// <summary>
        /// Begins a polling scope for a long-running async job (submit → poll → terminal state).
        /// The returned scope tracks attempt count, transient errors, and terminal outcome.
        /// Caller owns disposal — typically via <c>using</c>. If the scope is disposed without
        /// an explicit failure/timeout/cancelled call, the outcome defaults to succeeded.
        /// </summary>
        public static PollingScope BeginPolling(
            string operation,
            string providerName,
            string providerType,
            string model)
        {
            var activity = ActivitySource.StartActivity(
                $"provider.{operation}.poll",
                ActivityKind.Internal,
                Activity.Current?.Context ?? default,
                new TagList
                {
                    { "provider", providerName },
                    { "provider.type", providerType },
                    { "provider.model", model },
                    { "provider.operation", operation }
                });

            return new PollingScope(activity, operation, providerName, providerType, model);
        }

        /// <summary>
        /// Begins a streaming-request scope. Use inside an async iterator with
        /// <c>try { ... } finally { scope.Dispose(); }</c>; report each chunk via
        /// <see cref="StreamingScope.RecordChunk"/> and call
        /// <see cref="StreamingScope.RecordFailure"/> before re-throwing on error.
        /// </summary>
        public static StreamingScope BeginStreaming(
            string operation,
            string providerName,
            string providerType,
            string model)
        {
            var activity = StartRequestActivity(operation, providerName, providerType, model);
            return new StreamingScope(activity, operation, providerName, providerType, model);
        }

        /// <summary>
        /// Disposable scope tracking duration, chunk count, first-chunk latency,
        /// and outcome of a streaming provider request.
        /// </summary>
        public sealed class StreamingScope : IDisposable
        {
            private readonly Activity? _activity;
            private readonly Stopwatch _stopwatch;
            private readonly string _operation;
            private readonly string _providerName;
            private readonly string _providerType;
            private readonly string _model;
            private long _chunkCount;
            private long _firstChunkMs = -1;
            private bool _failed;
            private string? _errorType;
            private bool _disposed;

            internal StreamingScope(
                Activity? activity,
                string operation,
                string providerName,
                string providerType,
                string model)
            {
                _activity = activity;
                _operation = operation;
                _providerName = providerName;
                _providerType = providerType;
                _model = model;
                _stopwatch = Stopwatch.StartNew();
            }

            /// <summary>
            /// Reports receipt of a single chunk. Captures first-chunk latency on the first call.
            /// </summary>
            public void RecordChunk()
            {
                var count = Interlocked.Increment(ref _chunkCount);
                if (count == 1)
                {
                    _firstChunkMs = _stopwatch.ElapsedMilliseconds;

                    var tags = new TagList
                    {
                        { "provider", _providerName },
                        { "provider.type", _providerType },
                        { "provider.model", _model },
                        { "provider.operation", _operation }
                    };
                    FirstChunkLatency.Record(_firstChunkMs, tags);
                }
            }

            /// <summary>
            /// Marks the stream as failed. Call this before re-throwing.
            /// </summary>
            public void RecordFailure(string errorType)
            {
                _failed = true;
                _errorType = errorType;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                _stopwatch.Stop();
                var elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;

                RecordRequest(_operation, _providerName, _providerType, _model, elapsedMs, !_failed, _errorType);

                var streamTags = new TagList
                {
                    { "provider", _providerName },
                    { "provider.type", _providerType },
                    { "provider.model", _model },
                    { "provider.operation", _operation }
                };
                StreamChunks.Add(_chunkCount, streamTags);

                if (_activity is { } activity)
                {
                    activity.SetTag("provider.stream.chunks", _chunkCount);
                    if (_firstChunkMs >= 0)
                    {
                        activity.SetTag("provider.stream.first_chunk_ms", _firstChunkMs);
                    }

                    if (_failed)
                    {
                        activity.SetStatus(ActivityStatusCode.Error, _errorType);
                    }
                    else
                    {
                        activity.SetStatus(ActivityStatusCode.Ok);
                    }

                    activity.Dispose();
                }
            }
        }

        /// <summary>
        /// Terminal outcome of a polling loop. Reported as the <c>outcome</c> tag on
        /// <c>provider.poll.*</c> metrics and as a span attribute.
        /// </summary>
        public enum PollOutcome
        {
            Succeeded,
            Failed,
            Timeout,
            Cancelled,
        }

        /// <summary>
        /// Disposable scope tracking attempt count, transient errors, elapsed time,
        /// and terminal outcome of a long-running async-job poll loop.
        /// </summary>
        public sealed class PollingScope : IDisposable
        {
            private readonly Activity? _activity;
            private readonly Stopwatch _stopwatch;
            private readonly string _operation;
            private readonly string _providerName;
            private readonly string _providerType;
            private readonly string _model;
            private long _attemptCount;
            private long _transientErrorCount;
            private string? _lastState;
            private PollOutcome _outcome = PollOutcome.Succeeded;
            private string? _failureType;
            private bool _disposed;

            internal PollingScope(
                Activity? activity,
                string operation,
                string providerName,
                string providerType,
                string model)
            {
                _activity = activity;
                _operation = operation;
                _providerName = providerName;
                _providerType = providerType;
                _model = model;
                _stopwatch = Stopwatch.StartNew();
            }

            /// <summary>
            /// Reports a single poll attempt with the state returned by the classifier.
            /// Safe to call with a null state (e.g., when the fetch itself failed).
            /// </summary>
            public void RecordAttempt(string? state)
            {
                Interlocked.Increment(ref _attemptCount);
                if (!string.IsNullOrEmpty(state))
                {
                    _lastState = state;
                }
            }

            /// <summary>
            /// Reports a transient fetch error during polling (counts toward the transient-error budget).
            /// </summary>
            public void RecordTransientError(string errorType)
            {
                Interlocked.Increment(ref _transientErrorCount);
                var tags = new TagList
                {
                    { "provider", _providerName },
                    { "provider.type", _providerType },
                    { "provider.model", _model },
                    { "provider.operation", _operation },
                    { "error_type", string.IsNullOrEmpty(errorType) ? "Unknown" : errorType }
                };
                PollTransientErrors.Add(1, tags);
            }

            /// <summary>Marks the poll as terminally failed (classifier returned Failed, or a domain exception was thrown).</summary>
            public void RecordFailure(string errorType)
            {
                _outcome = PollOutcome.Failed;
                _failureType = string.IsNullOrEmpty(errorType) ? "Unknown" : errorType;
            }

            /// <summary>Marks the poll as timed out.</summary>
            public void RecordTimeout()
            {
                _outcome = PollOutcome.Timeout;
                _failureType ??= nameof(PollOutcome.Timeout);
            }

            /// <summary>Marks the poll as cancelled.</summary>
            public void RecordCancelled()
            {
                _outcome = PollOutcome.Cancelled;
                _failureType ??= nameof(PollOutcome.Cancelled);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                _stopwatch.Stop();
                var elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;
                var outcomeTag = _outcome.ToString();

                var tags = new TagList
                {
                    { "provider", _providerName },
                    { "provider.type", _providerType },
                    { "provider.model", _model },
                    { "provider.operation", _operation },
                    { "outcome", outcomeTag }
                };

                PollDuration.Record(elapsedMs, tags);
                PollAttempts.Record(_attemptCount, tags);
                PollOutcomes.Add(1, tags);

                if (_activity is { } activity)
                {
                    activity.SetTag("provider.poll.attempts", _attemptCount);
                    activity.SetTag("provider.poll.transient_errors", _transientErrorCount);
                    activity.SetTag("provider.poll.outcome", outcomeTag);
                    if (!string.IsNullOrEmpty(_lastState))
                    {
                        activity.SetTag("provider.poll.last_state", _lastState);
                    }
                    if (_outcome == PollOutcome.Succeeded)
                    {
                        activity.SetStatus(ActivityStatusCode.Ok);
                    }
                    else
                    {
                        activity.SetStatus(ActivityStatusCode.Error, _failureType ?? outcomeTag);
                    }
                    activity.Dispose();
                }
            }
        }
    }
}
