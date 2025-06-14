using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Http.HealthChecks
{
    /// <summary>
    /// Health check for individual audio providers.
    /// </summary>
    public class AudioProviderHealthCheck : IHealthCheck
    {
        private readonly IAudioRouter _audioRouter;
        private readonly IAudioTranscriptionClient _transcriptionClient;
        private readonly ITextToSpeechClient _ttsClient;
        private readonly IOptions<AudioProviderHealthCheckOptions> _options;
        private readonly ILogger<AudioProviderHealthCheck> _logger;
        private readonly string _providerName;

        public AudioProviderHealthCheck(
            IAudioRouter audioRouter,
            IAudioTranscriptionClient transcriptionClient,
            ITextToSpeechClient ttsClient,
            IOptions<AudioProviderHealthCheckOptions> options,
            ILogger<AudioProviderHealthCheck> logger,
            string providerName)
        {
            _audioRouter = audioRouter;
            _transcriptionClient = transcriptionClient;
            _ttsClient = ttsClient;
            _options = options;
            _logger = logger;
            _providerName = providerName;
        }

        public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>
            {
                ["provider"] = _providerName,
                ["check_time"] = DateTime.UtcNow
            };

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var checks = new List<(string name, bool success, long latencyMs, string? error)>();

                // Check transcription capability
                if (_options.Value.CheckTranscription)
                {
                    var (success, latencyMs, error) = await CheckTranscriptionAsync(cancellationToken);
                    checks.Add(("transcription", success, latencyMs, error));
                    data["transcription_latency_ms"] = latencyMs;
                    if (!success)
                    {
                        data["transcription_error"] = error ?? "Unknown error";
                    }
                }

                // Check TTS capability
                if (_options.Value.CheckTextToSpeech)
                {
                    var (success, latencyMs, error) = await CheckTextToSpeechAsync(cancellationToken);
                    checks.Add(("text_to_speech", success, latencyMs, error));
                    data["tts_latency_ms"] = latencyMs;
                    if (!success)
                    {
                        data["tts_error"] = error ?? "Unknown error";
                    }
                }

                stopwatch.Stop();
                data["total_check_duration_ms"] = stopwatch.ElapsedMilliseconds;

                // Determine health status
                var failedChecks = checks.Where(c => !c.success).ToList();
                var avgLatency = checks.Any() ? checks.Average(c => c.latencyMs) : 0;
                data["average_latency_ms"] = avgLatency;

                if (failedChecks.Count == checks.Count && checks.Any())
                {
                    var errors = string.Join("; ", failedChecks.Select(c => $"{c.name}: {c.error}"));
                    _logger.LogError("Provider {Provider} health check failed: {Errors}", _providerName, errors);
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"All checks failed: {errors}", data: data);
                }

                if (failedChecks.Any())
                {
                    var errors = string.Join("; ", failedChecks.Select(c => $"{c.name}: {c.error}"));
                    _logger.LogWarning("Provider {Provider} partially degraded: {Errors}", _providerName, errors);
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded($"Some checks failed: {errors}", data: data);
                }

                if (avgLatency > _options.Value.LatencyThresholdMs)
                {
                    _logger.LogWarning(
                        "Provider {Provider} has high latency: {Latency}ms",
                        _providerName,
                        avgLatency);
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                        $"High latency detected: {avgLatency}ms",
                        data: data);
                }

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    $"Provider {_providerName} is healthy",
                    data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provider {Provider} health check failed with exception", _providerName);
                data["exception"] = ex.Message;
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    $"Health check failed: {ex.Message}",
                    ex,
                    data);
            }
        }

        private async Task<(bool success, long latencyMs, string? error)> CheckTranscriptionAsync(
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Use a small test audio sample
                var testAudio = GenerateTestAudioData();
                var request = new AudioTranscriptionRequest
                {
                    AudioData = testAudio,
                    Language = "en",
                    ResponseFormat = TranscriptionFormat.Json
                    // Provider property doesn't exist on AudioTranscriptionRequest
                };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.Value.TimeoutMs);

                var response = await _transcriptionClient.TranscribeAudioAsync(
                    request,
                    _options.Value.TestApiKey,
                    cts.Token);

                stopwatch.Stop();
                return (true, stopwatch.ElapsedMilliseconds, null);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return (false, stopwatch.ElapsedMilliseconds, "Timeout");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return (false, stopwatch.ElapsedMilliseconds, ex.Message);
            }
        }

        private async Task<(bool success, long latencyMs, string? error)> CheckTextToSpeechAsync(
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var request = new TextToSpeechRequest
                {
                    Input = "Health check test",
                    Voice = "alloy",
                    Model = "tts-1",
                    ResponseFormat = AudioFormat.Mp3
                    // Provider property doesn't exist on TextToSpeechRequest
                };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.Value.TimeoutMs);

                var response = await _ttsClient.CreateSpeechAsync(
                    request,
                    _options.Value.TestApiKey,
                    cts.Token);

                stopwatch.Stop();
                
                if (response.AudioData == null || response.AudioData.Length == 0)
                {
                    return (false, stopwatch.ElapsedMilliseconds, "Empty response");
                }

                return (true, stopwatch.ElapsedMilliseconds, null);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return (false, stopwatch.ElapsedMilliseconds, "Timeout");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return (false, stopwatch.ElapsedMilliseconds, ex.Message);
            }
        }

        private byte[] GenerateTestAudioData()
        {
            // Generate a small WAV header with silence
            // This is a minimal valid WAV file for testing
            var data = new byte[44 + 1000]; // Header + 1000 bytes of audio
            
            // RIFF header
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, data, 0, 4);
            Array.Copy(BitConverter.GetBytes(data.Length - 8), 0, data, 4, 4);
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, data, 8, 4);
            
            // fmt chunk
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, data, 12, 4);
            Array.Copy(BitConverter.GetBytes(16), 0, data, 16, 4); // Chunk size
            Array.Copy(BitConverter.GetBytes((short)1), 0, data, 20, 2); // PCM format
            Array.Copy(BitConverter.GetBytes((short)1), 0, data, 22, 2); // Channels
            Array.Copy(BitConverter.GetBytes(16000), 0, data, 24, 4); // Sample rate
            Array.Copy(BitConverter.GetBytes(32000), 0, data, 28, 4); // Byte rate
            Array.Copy(BitConverter.GetBytes((short)2), 0, data, 32, 2); // Block align
            Array.Copy(BitConverter.GetBytes((short)16), 0, data, 34, 2); // Bits per sample
            
            // data chunk
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("data"), 0, data, 36, 4);
            Array.Copy(BitConverter.GetBytes(1000), 0, data, 40, 4);
            
            return data;
        }
    }

    /// <summary>
    /// Options for audio provider health checks.
    /// </summary>
    public class AudioProviderHealthCheckOptions
    {
        /// <summary>
        /// Whether to check transcription capability.
        /// </summary>
        public bool CheckTranscription { get; set; } = true;

        /// <summary>
        /// Whether to check text-to-speech capability.
        /// </summary>
        public bool CheckTextToSpeech { get; set; } = true;

        /// <summary>
        /// Timeout for each health check operation in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Latency threshold in milliseconds above which the provider is considered degraded.
        /// </summary>
        public int LatencyThresholdMs { get; set; } = 2000;

        /// <summary>
        /// API key to use for health check requests.
        /// </summary>
        public string TestApiKey { get; set; } = "health-check-key";
    }
}