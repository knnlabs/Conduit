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
    }
}
