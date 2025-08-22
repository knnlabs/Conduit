using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Audio transcription monitoring functionality for the monitoring audio service.
    /// </summary>
    public partial class MonitoringAudioService
    {
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
        Task<List<string>> IAudioTranscriptionClient.GetSupportedFormatsAsync(CancellationToken cancellationToken)
        {
            return _transcriptionClient.GetSupportedFormatsAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<List<string>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
        {
            return _transcriptionClient.GetSupportedLanguagesAsync(cancellationToken);
        }

        #endregion
    }
}