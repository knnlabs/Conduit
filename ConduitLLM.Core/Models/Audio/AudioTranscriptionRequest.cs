using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Represents a request to transcribe audio content into text.
    /// </summary>
    /// <remarks>
    /// This model supports various audio input methods and transcription options
    /// that are common across different STT providers.
    /// </remarks>
    public class AudioTranscriptionRequest : AudioRequestBase
    {
        /// <summary>
        /// The audio data to transcribe, provided as raw bytes.
        /// </summary>
        /// <remarks>
        /// Either AudioData or AudioUrl must be provided, but not both.
        /// The audio format should match one of the provider's supported formats.
        /// </remarks>
        public byte[]? AudioData { get; set; }

        /// <summary>
        /// URL pointing to the audio file to transcribe.
        /// </summary>
        /// <remarks>
        /// Some providers support direct URL access to audio files.
        /// Either AudioData or AudioUrl must be provided, but not both.
        /// </remarks>
        [Url]
        public string? AudioUrl { get; set; }

        /// <summary>
        /// The audio file name, used to infer format if not explicitly specified.
        /// </summary>
        /// <remarks>
        /// Optional but recommended when providing AudioData to help
        /// providers determine the audio format from the file extension.
        /// </remarks>
        public string? FileName { get; set; }

        /// <summary>
        /// The format of the audio data.
        /// </summary>
        /// <remarks>
        /// If not specified, the format may be inferred from the FileName extension.
        /// </remarks>
        public AudioFormat? AudioFormat { get; set; }

        /// <summary>
        /// The model to use for transcription (e.g., "whisper-1").
        /// </summary>
        /// <remarks>
        /// If not specified, the provider's default transcription model will be used.
        /// </remarks>
        public string? Model { get; set; }

        /// <summary>
        /// The language of the audio in ISO-639-1 format (e.g., "en", "es", "fr").
        /// </summary>
        /// <remarks>
        /// Optional. If not specified, the provider will attempt to auto-detect
        /// the language. Specifying the language can improve accuracy.
        /// </remarks>
        [RegularExpression(@"^[a-z]{2}(-[A-Z]{2})?$", ErrorMessage = "Language must be in ISO-639-1 format")]
        public string? Language { get; set; }

        /// <summary>
        /// Optional prompt to guide the transcription style or provide context.
        /// </summary>
        /// <remarks>
        /// Some providers support prompts to improve transcription accuracy
        /// or maintain consistent formatting/spelling of specific terms.
        /// </remarks>
        [MaxLength(500)]
        public string? Prompt { get; set; }

        /// <summary>
        /// The sampling temperature for transcription (0-1).
        /// </summary>
        /// <remarks>
        /// Lower values make the transcription more deterministic,
        /// higher values allow more variation. Default is provider-specific.
        /// </remarks>
        [Range(0.0, 1.0)]
        public double? Temperature { get; set; }

        /// <summary>
        /// The desired output format for the transcription.
        /// </summary>
        /// <remarks>
        /// Common formats include "json", "text", "srt", "vtt".
        /// Default is typically "json" with full metadata.
        /// </remarks>
        public TranscriptionFormat? ResponseFormat { get; set; }

        /// <summary>
        /// The minimum quality score required for provider selection.
        /// </summary>
        /// <remarks>
        /// Used by quality-based routing strategies. Range: 0-100.
        /// Higher values may limit provider options but ensure better quality.
        /// </remarks>
        [Range(0, 100)]
        public double? RequiredQuality { get; set; }

        /// <summary>
        /// Whether to enable streaming for the transcription.
        /// </summary>
        /// <remarks>
        /// When true, the transcription service will stream partial results
        /// as they become available. Not all providers support streaming.
        /// </remarks>
        public bool EnableStreaming { get; set; } = false;

        /// <summary>
        /// The level of timestamp detail to include in the response.
        /// </summary>
        /// <remarks>
        /// Controls whether to include word-level or segment-level timestamps,
        /// if supported by the provider.
        /// </remarks>
        public TimestampGranularity? TimestampGranularity { get; set; }

        /// <summary>
        /// Whether to include punctuation in the transcription.
        /// </summary>
        /// <remarks>
        /// Some providers allow disabling punctuation for specific use cases.
        /// Default is true (include punctuation).
        /// </remarks>
        public bool? IncludePunctuation { get; set; } = true;

        /// <summary>
        /// Whether to filter profanity in the transcription.
        /// </summary>
        /// <remarks>
        /// When enabled, profane words may be censored or removed.
        /// Support varies by provider.
        /// </remarks>
        public bool? FilterProfanity { get; set; }

        /// <summary>
        /// Validates that the request has required data.
        /// </summary>
        public override bool IsValid(out string? errorMessage)
        {
            errorMessage = null;

            if (AudioData == null && string.IsNullOrWhiteSpace(AudioUrl))
            {
                errorMessage = "Either AudioData or AudioUrl must be provided";
                return false;
            }

            if (AudioData != null && !string.IsNullOrWhiteSpace(AudioUrl))
            {
                errorMessage = "Only one of AudioData or AudioUrl should be provided";
                return false;
            }

            if (AudioData?.Length == 0)
            {
                errorMessage = "AudioData cannot be empty";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Base class for audio-related requests.
    /// </summary>
    public abstract class AudioRequestBase
    {
        /// <summary>
        /// Optional user identifier for tracking and billing purposes.
        /// </summary>
        public string? User { get; set; }

        /// <summary>
        /// Provider-specific options that don't fit the standard model.
        /// </summary>
        public Dictionary<string, object>? ProviderOptions { get; set; }

        /// <summary>
        /// Validates that the request contains valid data.
        /// </summary>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public abstract bool IsValid(out string? errorMessage);
    }

    /// <summary>
    /// Supported transcription output formats.
    /// </summary>
    public enum TranscriptionFormat
    {
        /// <summary>
        /// JSON format with full metadata.
        /// </summary>
        Json,

        /// <summary>
        /// Plain text without metadata.
        /// </summary>
        Text,

        /// <summary>
        /// SubRip subtitle format.
        /// </summary>
        Srt,

        /// <summary>
        /// WebVTT subtitle format.
        /// </summary>
        Vtt,

        /// <summary>
        /// Verbose JSON with additional details.
        /// </summary>
        VerboseJson
    }

    /// <summary>
    /// Granularity of timestamps in transcription.
    /// </summary>
    public enum TimestampGranularity
    {
        /// <summary>
        /// No timestamps.
        /// </summary>
        None,

        /// <summary>
        /// Timestamps at segment/sentence level.
        /// </summary>
        Segment,

        /// <summary>
        /// Timestamps for each word.
        /// </summary>
        Word
    }
}