using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Represents the response from an audio transcription request.
    /// </summary>
    public class AudioTranscriptionResponse
    {
        /// <summary>
        /// The primary transcribed text.
        /// </summary>
        /// <remarks>
        /// This contains the full transcription of the audio input,
        /// formatted according to the requested output format.
        /// </remarks>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// The detected or specified language of the audio.
        /// </summary>
        /// <remarks>
        /// ISO-639-1 language code (e.g., "en", "es", "fr").
        /// May be auto-detected if not specified in the request.
        /// </remarks>
        public string? Language { get; set; }

        /// <summary>
        /// The duration of the audio in seconds.
        /// </summary>
        /// <remarks>
        /// Total length of the processed audio file.
        /// </remarks>
        public double? Duration { get; set; }

        /// <summary>
        /// Segments of the transcription with timestamps.
        /// </summary>
        /// <remarks>
        /// Available when segment-level timestamps are requested.
        /// Each segment typically represents a sentence or phrase.
        /// </remarks>
        public List<TranscriptionSegment>? Segments { get; set; }

        /// <summary>
        /// Individual words with timestamps.
        /// </summary>
        /// <remarks>
        /// Available when word-level timestamps are requested.
        /// Provides fine-grained timing information.
        /// </remarks>
        public List<TranscriptionWord>? Words { get; set; }

        /// <summary>
        /// Alternative transcriptions with confidence scores.
        /// </summary>
        /// <remarks>
        /// Some providers return multiple possible transcriptions
        /// ranked by confidence. The primary transcription is in Text.
        /// </remarks>
        public List<TranscriptionAlternative>? Alternatives { get; set; }

        /// <summary>
        /// Overall confidence score for the transcription (0-1).
        /// </summary>
        /// <remarks>
        /// Indicates the provider's confidence in the accuracy
        /// of the transcription. Higher values indicate higher confidence.
        /// </remarks>
        public double? Confidence { get; set; }

        /// <summary>
        /// Provider-specific metadata or additional information.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// The model used for transcription.
        /// </summary>
        /// <remarks>
        /// Indicates which STT model was actually used,
        /// which may differ from the requested model.
        /// </remarks>
        public string? Model { get; set; }

        /// <summary>
        /// Usage information for billing purposes.
        /// </summary>
        public AudioUsage? Usage { get; set; }
    }

    /// <summary>
    /// Represents a segment of transcribed text with timing information.
    /// </summary>
    public class TranscriptionSegment
    {
        /// <summary>
        /// The segment identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Start time of the segment in seconds.
        /// </summary>
        public double Start { get; set; }

        /// <summary>
        /// End time of the segment in seconds.
        /// </summary>
        public double End { get; set; }

        /// <summary>
        /// The transcribed text for this segment.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score for this segment (0-1).
        /// </summary>
        public double? Confidence { get; set; }

        /// <summary>
        /// Speaker identifier if speaker diarization is enabled.
        /// </summary>
        public string? Speaker { get; set; }
    }

    /// <summary>
    /// Represents a single transcribed word with timing information.
    /// </summary>
    public class TranscriptionWord
    {
        /// <summary>
        /// The transcribed word.
        /// </summary>
        public string Word { get; set; } = string.Empty;

        /// <summary>
        /// Start time of the word in seconds.
        /// </summary>
        public double Start { get; set; }

        /// <summary>
        /// End time of the word in seconds.
        /// </summary>
        public double End { get; set; }

        /// <summary>
        /// Confidence score for this word (0-1).
        /// </summary>
        public double? Confidence { get; set; }

        /// <summary>
        /// Speaker identifier if speaker diarization is enabled.
        /// </summary>
        public string? Speaker { get; set; }
    }

    /// <summary>
    /// Represents an alternative transcription with confidence score.
    /// </summary>
    public class TranscriptionAlternative
    {
        /// <summary>
        /// The alternative transcription text.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score for this alternative (0-1).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Segments for this alternative, if available.
        /// </summary>
        public List<TranscriptionSegment>? Segments { get; set; }
    }

    /// <summary>
    /// Usage information for audio operations.
    /// </summary>
    public class AudioUsage
    {
        /// <summary>
        /// Duration of audio processed in seconds.
        /// </summary>
        public double AudioSeconds { get; set; }

        /// <summary>
        /// Number of characters in the transcription.
        /// </summary>
        public int? CharacterCount { get; set; }

        /// <summary>
        /// Provider-specific usage metrics.
        /// </summary>
        public Dictionary<string, object>? AdditionalMetrics { get; set; }
    }
}