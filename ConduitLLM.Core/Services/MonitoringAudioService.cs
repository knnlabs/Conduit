using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Audio service wrapper that adds comprehensive monitoring and observability.
    /// </summary>
    public class MonitoringAudioService : IAudioTranscriptionClient, ITextToSpeechClient, IRealtimeAudioClient
    {
        protected readonly IAudioTranscriptionClient _transcriptionClient;
        protected readonly ITextToSpeechClient _ttsClient;
        protected readonly IRealtimeAudioClient _realtimeClient;
        protected readonly IAudioMetricsCollector _metricsCollector;
        protected readonly IAudioAlertingService _alertingService;
        protected readonly IAudioTracingService _tracingService;
        protected readonly ILogger<MonitoringAudioService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoringAudioService"/> class.
        /// </summary>
        public MonitoringAudioService(
            IAudioTranscriptionClient transcriptionClient,
            ITextToSpeechClient ttsClient,
            IRealtimeAudioClient realtimeClient,
            IAudioMetricsCollector metricsCollector,
            IAudioAlertingService alertingService,
            IAudioTracingService tracingService,
            ILogger<MonitoringAudioService> logger)
        {
            _transcriptionClient = transcriptionClient ?? throw new ArgumentNullException(nameof(transcriptionClient));
            _ttsClient = ttsClient ?? throw new ArgumentNullException(nameof(ttsClient));
            _realtimeClient = realtimeClient ?? throw new ArgumentNullException(nameof(realtimeClient));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region IAudioTranscriptionClient Implementation

        /// <inheritdoc />
        public virtual async Task<AudioTranscriptionResponse> TranscribeAudioAsync(
            AudioTranscriptionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using var trace = _tracingService.StartTrace(
                "audio.transcribe",
                AudioOperation.Transcription,
                new()
                {
                    ["audio.size_bytes"] = request.AudioData?.Length.ToString() ?? "0",
                    ["audio.language"] = request.Language ?? "auto",
                    ["api_key"] = apiKey ?? "default"
                });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var metric = new TranscriptionMetric
            {
                Provider = _transcriptionClient.GetType().Name,
                VirtualKey = apiKey ?? "default",
                AudioFormat = request.ResponseFormat?.ToString() ?? "unknown",
                FileSizeBytes = request.AudioData?.Length ?? 0,
                DetectedLanguage = request.Language
            };

            try
            {
                trace.AddEvent("transcription.start");

                var response = await _transcriptionClient.TranscribeAudioAsync(
                    request, apiKey, cancellationToken);

                stopwatch.Stop();
                metric.Success = true;
                metric.DurationMs = stopwatch.ElapsedMilliseconds;
                metric.AudioDurationSeconds = response.Duration ?? 0;
                metric.DetectedLanguage = response.Language ?? request.Language;
                metric.WordCount = response.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                trace.AddTag("transcription.words", metric.WordCount.ToString());
                trace.AddTag("transcription.duration_ms", metric.DurationMs.ToString());
                trace.SetStatus(TraceStatus.Ok);

                _logger.LogInformation(
                    "Transcription completed: {Words} words in {Duration}ms",
                    metric.WordCount, metric.DurationMs);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                metric.Success = false;
                metric.DurationMs = stopwatch.ElapsedMilliseconds;
                metric.ErrorCode = ex.GetType().Name;

                trace.RecordException(ex);

                _logger.LogError(ex,
                    "Transcription failed after {Duration}ms",
                    metric.DurationMs);

                throw;
            }
            finally
            {
                await _metricsCollector.RecordTranscriptionMetricAsync(metric);

                // Check alerts
                var snapshot = await _metricsCollector.GetCurrentSnapshotAsync();
                await _alertingService.EvaluateMetricsAsync(snapshot, CancellationToken.None);
            }
        }

        /// <inheritdoc />
        public Task<bool> SupportsTranscriptionAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return _transcriptionClient.SupportsTranscriptionAsync(apiKey, cancellationToken);
        }

        /// <inheritdoc />
        public Task<List<string>> GetSupportedFormatsAsync(CancellationToken cancellationToken = default)
        {
            return _transcriptionClient.GetSupportedFormatsAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<List<string>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
        {
            return _transcriptionClient.GetSupportedLanguagesAsync(cancellationToken);
        }

        #endregion

        #region ITextToSpeechClient Implementation

        /// <inheritdoc />
        public virtual async Task<TextToSpeechResponse> CreateSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using var trace = _tracingService.StartTrace(
                "audio.tts",
                AudioOperation.TextToSpeech,
                new()
                {
                    ["tts.character_count"] = request.Input.Length.ToString(),
                    ["tts.voice"] = request.Voice,
                    ["tts.model"] = request.Model ?? "default",
                    ["api_key"] = apiKey ?? "default"
                });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var metric = new TtsMetric
            {
                Provider = _ttsClient.GetType().Name,
                VirtualKey = apiKey ?? "default",
                Voice = request.Voice,
                CharacterCount = request.Input.Length,
                OutputFormat = request.ResponseFormat?.ToString() ?? "mp3"
            };

            try
            {
                trace.AddEvent("tts.start");

                var response = await _ttsClient.CreateSpeechAsync(
                    request, apiKey, cancellationToken);

                stopwatch.Stop();
                metric.Success = true;
                metric.DurationMs = stopwatch.ElapsedMilliseconds;
                metric.OutputSizeBytes = response.AudioData?.Length ?? 0;
                metric.GeneratedDurationSeconds = response.Duration ?? 0;

                trace.AddTag("tts.output_bytes", metric.OutputSizeBytes.ToString());
                trace.AddTag("tts.duration_ms", metric.DurationMs.ToString());
                trace.SetStatus(TraceStatus.Ok);

                _logger.LogInformation(
                    "TTS completed: {Characters} chars -> {Bytes} bytes in {Duration}ms",
                    metric.CharacterCount, metric.OutputSizeBytes, metric.DurationMs);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                metric.Success = false;
                metric.DurationMs = stopwatch.ElapsedMilliseconds;
                metric.ErrorCode = ex.GetType().Name;

                trace.RecordException(ex);

                _logger.LogError(ex,
                    "TTS failed after {Duration}ms",
                    metric.DurationMs);

                throw;
            }
            finally
            {
                await _metricsCollector.RecordTtsMetricAsync(metric);

                // Check alerts
                var snapshot = await _metricsCollector.GetCurrentSnapshotAsync();
                await _alertingService.EvaluateMetricsAsync(snapshot, CancellationToken.None);
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<AudioChunk> StreamSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var trace = _tracingService.StartTrace(
                "audio.tts.stream",
                AudioOperation.TextToSpeech,
                new()
                {
                    ["tts.character_count"] = request.Input.Length.ToString(),
                    ["tts.voice"] = request.Voice,
                    ["api_key"] = apiKey ?? "default"
                });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var totalBytes = 0;

            var chunks = _ttsClient.StreamSpeechAsync(request, apiKey, cancellationToken);
            var enumerator = chunks.GetAsyncEnumerator(cancellationToken);

            try
            {
                while (true)
                {
                    AudioChunk? chunk = null;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                            break;
                        chunk = enumerator.Current;
                    }
                    catch (Exception ex)
                    {
                        trace.RecordException(ex);
                        _logger.LogError(ex, "TTS streaming failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
                        throw;
                    }

                    if (chunk != null)
                    {
                        totalBytes += chunk.Data?.Length ?? 0;
                        trace.AddEvent("tts.chunk", new Dictionary<string, object>
                        {
                            ["chunk_size"] = chunk.Data?.Length ?? 0
                        });

                        yield return chunk;
                    }
                }

                trace.SetStatus(TraceStatus.Ok);
                _logger.LogInformation(
                    "TTS streaming completed: {TotalBytes} bytes in {Duration}ms",
                    totalBytes, stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
        }

        /// <inheritdoc />
        public async Task<List<VoiceInfo>> ListVoicesAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using var trace = _tracingService.StartTrace(
                "audio.list_voices",
                AudioOperation.TextToSpeech,
                new()
                {
                    ["api_key"] = apiKey ?? "default"
                });

            try
            {
                var voices = await _ttsClient.ListVoicesAsync(apiKey, cancellationToken);

                trace.AddTag("voice.count", voices.Count.ToString());
                trace.SetStatus(TraceStatus.Ok);

                return voices;
            }
            catch (Exception ex)
            {
                trace.RecordException(ex);
                throw;
            }
        }

        /// <inheritdoc />
        Task<List<string>> ITextToSpeechClient.GetSupportedFormatsAsync(CancellationToken cancellationToken)
        {
            return _ttsClient.GetSupportedFormatsAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<bool> SupportsTextToSpeechAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return _ttsClient.SupportsTextToSpeechAsync(apiKey, cancellationToken);
        }

        #endregion

        #region IRealtimeAudioClient Implementation

        /// <inheritdoc />
        public async Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using var trace = _tracingService.StartTrace(
                "audio.realtime.create_session",
                AudioOperation.Realtime,
                new()
                {
                    ["realtime.model"] = config.Model ?? "default",
                    ["realtime.voice"] = config.Voice ?? "default",
                    ["api_key"] = apiKey ?? "default"
                });

            try
            {
                trace.AddEvent("session.create");

                var session = await _realtimeClient.CreateSessionAsync(
                    config, apiKey, cancellationToken);

                // Store virtual key in session metadata
                if (session.Metadata == null)
                {
                    session.Metadata = new Dictionary<string, object>();
                }
                session.Metadata["VirtualKey"] = apiKey ?? "default";

                trace.AddTag("session.id", session.Id);
                trace.SetStatus(TraceStatus.Ok);

                _logger.LogInformation(
                    "Realtime session created: {SessionId} for virtual key: {VirtualKey}",
                    session.Id, apiKey ?? "default");

                return session;
            }
            catch (Exception ex)
            {
                trace.RecordException(ex);

                _logger.LogError(ex,
                    "Failed to create realtime session");

                throw;
            }
        }

        /// <inheritdoc />
        public IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> StreamAudioAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            var stream = _realtimeClient.StreamAudioAsync(session, cancellationToken);
            var virtualKey = session.Metadata?.GetValueOrDefault("VirtualKey")?.ToString() ?? "default";

            return new MonitoredDuplexStream(
                stream,
                _metricsCollector,
                _tracingService,
                apiKey: virtualKey,
                _realtimeClient.GetType().Name,
                session.Id);
        }

        /// <inheritdoc />
        public Task UpdateSessionAsync(
            RealtimeSession session,
            RealtimeSessionUpdate updates,
            CancellationToken cancellationToken = default)
        {
            return _realtimeClient.UpdateSessionAsync(session, updates, cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            await _realtimeClient.CloseSessionAsync(session, cancellationToken);

            // Record session completion metrics
            var sessionDuration = (DateTime.UtcNow - session.CreatedAt).TotalSeconds;
            var virtualKey = session.Metadata?.GetValueOrDefault("VirtualKey")?.ToString() ?? "default";

            await _metricsCollector.RecordRealtimeMetricAsync(new RealtimeMetric
            {
                Provider = _realtimeClient.GetType().Name,
                VirtualKey = virtualKey,
                SessionId = session.Id,
                SessionDurationSeconds = sessionDuration,
                Success = true,
                DurationMs = sessionDuration * 1000
            });
        }

        /// <inheritdoc />
        public Task<bool> SupportsRealtimeAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return _realtimeClient.SupportsRealtimeAsync(apiKey, cancellationToken);
        }

        /// <inheritdoc />
        public Task<RealtimeCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            return _realtimeClient.GetCapabilitiesAsync(cancellationToken);
        }

        #endregion
    }

    /// <summary>
    /// Monitored duplex stream wrapper.
    /// </summary>
    internal class MonitoredDuplexStream : IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse>
    {
        private readonly IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> _innerStream;
        private readonly IAudioMetricsCollector _metricsCollector;
        private readonly IAudioTracingService _tracingService;
        private readonly string _virtualKey;
        private readonly string _provider;
        private readonly string _sessionId;
        private readonly IAudioTraceContext _streamTrace;
        private int _framesSent;
        private int _framesReceived;

        public bool IsConnected => _innerStream.IsConnected;

        public MonitoredDuplexStream(
            IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> innerStream,
            IAudioMetricsCollector metricsCollector,
            IAudioTracingService tracingService,
            string apiKey,
            string provider,
            string sessionId)
        {
            _innerStream = innerStream;
            _metricsCollector = metricsCollector;
            _tracingService = tracingService;
            _virtualKey = apiKey;
            _provider = provider;
            _sessionId = sessionId;

            _streamTrace = _tracingService.StartTrace(
                $"audio.realtime.stream.{sessionId}",
                AudioOperation.Realtime,
                new()
                {
                    ["session.id"] = sessionId,
                    ["virtual_key"] = apiKey,
                    ["provider"] = provider
                });
        }

        public async ValueTask SendAsync(RealtimeAudioFrame item, CancellationToken cancellationToken = default)
        {
            using var span = _tracingService.CreateSpan(_streamTrace, "stream.send");

            try
            {
                await _innerStream.SendAsync(item, cancellationToken);
                _framesSent++;

                span.AddTag("frame.type", item.Type.ToString());
                span.AddTag("frame.size", item.AudioData?.Length.ToString() ?? "0");
                span.SetStatus(TraceStatus.Ok);
            }
            catch (Exception ex)
            {
                span.RecordException(ex);
                throw;
            }
        }

        public async IAsyncEnumerable<RealtimeResponse> ReceiveAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var response in _innerStream.ReceiveAsync(cancellationToken))
            {
                _framesReceived++;

                using var span = _tracingService.CreateSpan(_streamTrace, "stream.receive");
                span.AddTag("response.type", response.Type.ToString());
                span.SetStatus(TraceStatus.Ok);

                yield return response;
            }
        }

        public async ValueTask CompleteAsync()
        {
            await _innerStream.CompleteAsync();

            _streamTrace.AddTag("frames.sent", _framesSent.ToString());
            _streamTrace.AddTag("frames.received", _framesReceived.ToString());
            _streamTrace.SetStatus(TraceStatus.Ok);
            _streamTrace.Dispose();
        }
    }
}
