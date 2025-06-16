using System;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Wrapper service that adds security features to audio operations.
    /// </summary>
    public partial class SecureAudioService
    {
        private readonly ILogger<SecureAudioService> _logger;
        private readonly IAudioRouter _audioRouter;
        private readonly IAudioContentFilter _contentFilter;
        private readonly IAudioPiiDetector _piiDetector;
        private readonly IAudioEncryptionService _encryptionService;
        private readonly IAudioAuditLogger _auditLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecureAudioService"/> class.
        /// </summary>
        public SecureAudioService(
            ILogger<SecureAudioService> logger,
            IAudioRouter audioRouter,
            IAudioContentFilter contentFilter,
            IAudioPiiDetector piiDetector,
            IAudioEncryptionService encryptionService,
            IAudioAuditLogger auditLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _audioRouter = audioRouter ?? throw new ArgumentNullException(nameof(audioRouter));
            _contentFilter = contentFilter ?? throw new ArgumentNullException(nameof(contentFilter));
            _piiDetector = piiDetector ?? throw new ArgumentNullException(nameof(piiDetector));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        /// <summary>
        /// Performs secure audio transcription with content filtering and PII detection.
        /// </summary>
        public async Task<SecureTranscriptionResponse> TranscribeAudioSecurelyAsync(
            AudioTranscriptionRequest request,
            string virtualKey,
            SecurityOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var auditEntry = new AudioAuditEntry
            {
                VirtualKey = virtualKey,
                Operation = AudioOperation.Transcription,
                SizeBytes = request.AudioData?.Length ?? 0,
                Language = request.Language
            };

            try
            {
                options ??= new SecurityOptions();

                // Validate audio content
                if (options.ValidateAudioContent && request.AudioData != null)
                {
                    var isValid = await _contentFilter.ValidateAudioContentAsync(
                        request.AudioData,
                        request.AudioFormat ?? AudioFormat.Mp3,
                        virtualKey,
                        cancellationToken);

                    if (!isValid)
                    {
                        throw new InvalidOperationException("Audio content validation failed");
                    }
                }

                // Get transcription client
                var client = await _audioRouter.GetTranscriptionClientAsync(request, virtualKey, cancellationToken);
                if (client == null)
                {
                    throw new InvalidOperationException("No transcription provider available");
                }

                // Perform transcription
                var transcriptionResult = await client.TranscribeAudioAsync(request, cancellationToken: cancellationToken);

                var response = new SecureTranscriptionResponse
                {
                    OriginalText = transcriptionResult.Text,
                    ProcessedText = transcriptionResult.Text,
                    Language = transcriptionResult.Language,
                    Confidence = transcriptionResult.Confidence ?? 0,
                    Provider = client.GetType().Name
                };

                // Apply content filtering
                if (options.EnableContentFiltering)
                {
                    var filterResult = await _contentFilter.FilterTranscriptionAsync(
                        transcriptionResult.Text,
                        virtualKey,
                        cancellationToken);

                    response.ContentFilterResult = filterResult;
                    response.ProcessedText = filterResult.FilteredText;

                    if (!filterResult.IsApproved && options.BlockInappropriateContent)
                    {
                        throw new InvalidOperationException("Content blocked due to policy violations");
                    }
                }

                // Detect and redact PII
                if (options.EnablePiiDetection)
                {
                    var piiResult = await _piiDetector.DetectPiiAsync(
                        response.ProcessedText,
                        cancellationToken);

                    response.PiiDetectionResult = piiResult;

                    if (piiResult.ContainsPii && options.RedactPii)
                    {
                        response.ProcessedText = await _piiDetector.RedactPiiAsync(
                            response.ProcessedText,
                            piiResult,
                            options.PiiRedactionOptions);
                    }
                }

                // Update audit entry
                auditEntry.Success = true;
                auditEntry.Provider = response.Provider;
                auditEntry.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                return response;
            }
            catch (Exception ex)
            {
                auditEntry.Success = false;
                auditEntry.ErrorMessage = ex.Message;
                auditEntry.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogError(ex, "Secure transcription failed");
                throw;
            }
            finally
            {
                // Always log audit entry
                await _auditLogger.LogTranscriptionAsync(auditEntry, cancellationToken);
            }
        }

        /// <summary>
        /// Performs secure text-to-speech with content filtering.
        /// </summary>
        public async Task<SecureTtsResponse> GenerateSpeechSecurelyAsync(
            TextToSpeechRequest request,
            string virtualKey,
            SecurityOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var auditEntry = new AudioAuditEntry
            {
                VirtualKey = virtualKey,
                Operation = AudioOperation.TextToSpeech,
                Language = request.Language
            };

            try
            {
                options ??= new SecurityOptions();

                var processedText = request.Input;

                // Apply content filtering
                if (options.EnableContentFiltering)
                {
                    var filterResult = await _contentFilter.FilterTextToSpeechAsync(
                        request.Input,
                        virtualKey,
                        cancellationToken);

                    if (!filterResult.IsApproved && options.BlockInappropriateContent)
                    {
                        throw new InvalidOperationException("Content blocked due to policy violations");
                    }

                    processedText = filterResult.FilteredText;
                }

                // Detect and redact PII
                if (options.EnablePiiDetection)
                {
                    var piiResult = await _piiDetector.DetectPiiAsync(processedText, cancellationToken);

                    if (piiResult.ContainsPii && options.RedactPii)
                    {
                        processedText = await _piiDetector.RedactPiiAsync(
                            processedText,
                            piiResult,
                            options.PiiRedactionOptions);
                    }
                }

                // Create modified request
                var secureRequest = new TextToSpeechRequest
                {
                    Input = processedText,
                    Voice = request.Voice,
                    Language = request.Language,
                    Speed = request.Speed,
                    ResponseFormat = request.ResponseFormat,
                    VoiceSettings = request.VoiceSettings,
                    EnableSSML = request.EnableSSML
                };

                // Get TTS client
                var client = await _audioRouter.GetTextToSpeechClientAsync(request, virtualKey, cancellationToken);
                if (client == null)
                {
                    throw new InvalidOperationException("No TTS provider available");
                }

                // Generate speech
                var audioResult = await client.CreateSpeechAsync(secureRequest, cancellationToken: cancellationToken);

                var response = new SecureTtsResponse
                {
                    AudioData = audioResult.AudioData,
                    AudioFormat = DetermineAudioFormat(audioResult.Format),
                    Duration = audioResult.Duration,
                    Provider = client.GetType().Name,
                    WasModified = processedText != request.Input
                };

                // Encrypt audio if requested
                if (options.EncryptAudioAtRest)
                {
                    var metadata = new AudioEncryptionMetadata
                    {
                        Format = audioResult.Format ?? "mp3",
                        OriginalSize = audioResult.AudioData.Length,
                        DurationSeconds = audioResult.Duration,
                        VirtualKey = virtualKey
                    };

                    response.EncryptedAudio = await _encryptionService.EncryptAudioAsync(
                        audioResult.AudioData,
                        metadata,
                        cancellationToken);
                }

                // Update audit entry
                auditEntry.Success = true;
                auditEntry.Provider = response.Provider;
                auditEntry.SizeBytes = audioResult.AudioData.Length;
                auditEntry.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                return response;
            }
            catch (Exception ex)
            {
                auditEntry.Success = false;
                auditEntry.ErrorMessage = ex.Message;
                auditEntry.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogError(ex, "Secure TTS failed");
                throw;
            }
            finally
            {
                // Always log audit entry
                await _auditLogger.LogTextToSpeechAsync(auditEntry, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Security options for audio operations.
    /// </summary>
    public class SecurityOptions
    {
        /// <summary>
        /// Gets or sets whether to validate audio content before processing.
        /// </summary>
        public bool ValidateAudioContent { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable content filtering.
        /// </summary>
        public bool EnableContentFiltering { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to block inappropriate content.
        /// </summary>
        public bool BlockInappropriateContent { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable PII detection.
        /// </summary>
        public bool EnablePiiDetection { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to redact detected PII.
        /// </summary>
        public bool RedactPii { get; set; } = true;

        /// <summary>
        /// Gets or sets PII redaction options.
        /// </summary>
        public PiiRedactionOptions? PiiRedactionOptions { get; set; }

        /// <summary>
        /// Gets or sets whether to encrypt audio at rest.
        /// </summary>
        public bool EncryptAudioAtRest { get; set; } = false;
    }

    /// <summary>
    /// Response from secure transcription.
    /// </summary>
    public class SecureTranscriptionResponse
    {
        /// <summary>
        /// Gets or sets the original transcribed text.
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the processed text after filtering/redaction.
        /// </summary>
        public string ProcessedText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detected language.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the confidence score.
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets the provider used.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content filter result.
        /// </summary>
        public ContentFilterResult? ContentFilterResult { get; set; }

        /// <summary>
        /// Gets or sets the PII detection result.
        /// </summary>
        public PiiDetectionResult? PiiDetectionResult { get; set; }
    }

    /// <summary>
    /// Response from secure TTS.
    /// </summary>
    public class SecureTtsResponse
    {
        /// <summary>
        /// Gets or sets the generated audio data.
        /// </summary>
        public byte[] AudioData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the audio format.
        /// </summary>
        public AudioFormat AudioFormat { get; set; }

        /// <summary>
        /// Gets or sets the duration in seconds.
        /// </summary>
        public double? Duration { get; set; }

        /// <summary>
        /// Gets or sets the provider used.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether text was modified.
        /// </summary>
        public bool WasModified { get; set; }

        /// <summary>
        /// Gets or sets encrypted audio data if encryption was requested.
        /// </summary>
        public EncryptedAudioData? EncryptedAudio { get; set; }
    }
}

namespace ConduitLLM.Core.Services
{
    public partial class SecureAudioService
    {
        private AudioFormat DetermineAudioFormat(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return AudioFormat.Mp3;

            return format.ToLowerInvariant() switch
            {
                "mp3" => AudioFormat.Mp3,
                "wav" => AudioFormat.Wav,
                "flac" => AudioFormat.Flac,
                "ogg" => AudioFormat.Ogg,
                "opus" => AudioFormat.Opus,
                "aac" => AudioFormat.Aac,
                "pcm" => AudioFormat.Pcm,
                _ => AudioFormat.Mp3
            };
        }
    }
}
