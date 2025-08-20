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
    /// Text-to-speech monitoring functionality for the monitoring audio service.
    /// </summary>
    public partial class MonitoringAudioService
    {
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
    }
}