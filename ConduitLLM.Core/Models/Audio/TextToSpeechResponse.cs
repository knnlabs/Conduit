namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Represents the response from a text-to-speech synthesis request.
    /// </summary>
    public class TextToSpeechResponse
    {
        /// <summary>
        /// The generated audio data as raw bytes.
        /// </summary>
        /// <remarks>
        /// Contains the complete audio file in the requested format.
        /// For streaming responses, use the streaming API instead.
        /// </remarks>
        public byte[] AudioData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// The audio format of the generated data.
        /// </summary>
        /// <remarks>
        /// Indicates the actual format of the audio data,
        /// which may differ from the requested format if the
        /// provider performed conversion.
        /// </remarks>
        public string? Format { get; set; }

        /// <summary>
        /// The sample rate of the audio in Hz.
        /// </summary>
        /// <remarks>
        /// Common values: 8000, 16000, 22050, 24000, 44100, 48000.
        /// </remarks>
        public int? SampleRate { get; set; }

        /// <summary>
        /// The duration of the generated audio in seconds.
        /// </summary>
        public double? Duration { get; set; }

        /// <summary>
        /// The number of audio channels (1 = mono, 2 = stereo).
        /// </summary>
        public int? Channels { get; set; }

        /// <summary>
        /// The bit depth of the audio (e.g., 16, 24, 32).
        /// </summary>
        /// <remarks>
        /// Applicable for uncompressed formats like WAV or PCM.
        /// </remarks>
        public int? BitDepth { get; set; }

        /// <summary>
        /// Character count of the input text.
        /// </summary>
        /// <remarks>
        /// Used for usage tracking and billing purposes.
        /// </remarks>
        public int? CharacterCount { get; set; }

        /// <summary>
        /// The voice ID that was actually used.
        /// </summary>
        /// <remarks>
        /// May differ from requested if fallback occurred.
        /// </remarks>
        public string? VoiceUsed { get; set; }

        /// <summary>
        /// The model that was actually used.
        /// </summary>
        /// <remarks>
        /// Indicates which TTS model processed the request.
        /// </remarks>
        public string? ModelUsed { get; set; }

        /// <summary>
        /// Usage information for billing purposes.
        /// </summary>
        public TextToSpeechUsage? Usage { get; set; }

        /// <summary>
        /// Provider-specific metadata.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Represents a chunk of audio data for streaming TTS.
    /// </summary>
    public class AudioChunk
    {
        /// <summary>
        /// The audio data chunk.
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// The index of this chunk in the stream.
        /// </summary>
        public int ChunkIndex { get; set; }

        /// <summary>
        /// Whether this is the final chunk.
        /// </summary>
        public bool IsFinal { get; set; }

        /// <summary>
        /// The text portion this chunk corresponds to.
        /// </summary>
        /// <remarks>
        /// Some providers include text alignment information
        /// to sync audio with text display.
        /// </remarks>
        public string? TextSegment { get; set; }

        /// <summary>
        /// Timestamp information for this chunk.
        /// </summary>
        public ChunkTimestamp? Timestamp { get; set; }
    }

    /// <summary>
    /// Timing information for an audio chunk.
    /// </summary>
    public class ChunkTimestamp
    {
        /// <summary>
        /// Start time of this chunk in the overall audio (seconds).
        /// </summary>
        public double Start { get; set; }

        /// <summary>
        /// End time of this chunk in the overall audio (seconds).
        /// </summary>
        public double End { get; set; }

        /// <summary>
        /// Character offset in the original text.
        /// </summary>
        public int? TextOffset { get; set; }
    }

    /// <summary>
    /// Usage information for TTS operations.
    /// </summary>
    public class TextToSpeechUsage
    {
        /// <summary>
        /// Number of characters processed.
        /// </summary>
        public int Characters { get; set; }

        /// <summary>
        /// Duration of audio generated in seconds.
        /// </summary>
        public double AudioSeconds { get; set; }

        /// <summary>
        /// Provider-specific usage metrics.
        /// </summary>
        public Dictionary<string, object>? AdditionalMetrics { get; set; }
    }

    /// <summary>
    /// Information about an available TTS voice.
    /// </summary>
    public class VoiceInfo
    {
        /// <summary>
        /// Unique identifier for the voice.
        /// </summary>
        public string VoiceId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the voice.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the voice characteristics.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gender of the voice.
        /// </summary>
        public VoiceGender? Gender { get; set; }

        /// <summary>
        /// Age group of the voice.
        /// </summary>
        public VoiceAge? Age { get; set; }

        /// <summary>
        /// Languages supported by this voice.
        /// </summary>
        /// <remarks>
        /// ISO-639-1 language codes.
        /// </remarks>
        public List<string> SupportedLanguages { get; set; } = new();

        /// <summary>
        /// Primary accent or locale of the voice.
        /// </summary>
        public string? Accent { get; set; }

        /// <summary>
        /// Voice style capabilities.
        /// </summary>
        /// <remarks>
        /// Examples: "news", "conversational", "cheerful", "sad".
        /// </remarks>
        public List<string>? SupportedStyles { get; set; }

        /// <summary>
        /// Whether this is a premium voice.
        /// </summary>
        /// <remarks>
        /// Premium voices may have higher quality or cost.
        /// </remarks>
        public bool? IsPremium { get; set; }

        /// <summary>
        /// Whether this is a custom/cloned voice.
        /// </summary>
        public bool? IsCustom { get; set; }

        /// <summary>
        /// Sample audio URL for this voice.
        /// </summary>
        public string? SampleUrl { get; set; }

        /// <summary>
        /// Provider-specific voice metadata.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Voice gender categories.
    /// </summary>
    public enum VoiceGender
    {
        /// <summary>
        /// Male voice.
        /// </summary>
        Male,

        /// <summary>
        /// Female voice.
        /// </summary>
        Female,

        /// <summary>
        /// Neutral/non-binary voice.
        /// </summary>
        Neutral
    }

    /// <summary>
    /// Voice age categories.
    /// </summary>
    public enum VoiceAge
    {
        /// <summary>
        /// Child voice.
        /// </summary>
        Child,

        /// <summary>
        /// Young adult voice.
        /// </summary>
        YoungAdult,

        /// <summary>
        /// Middle-aged voice.
        /// </summary>
        MiddleAge,

        /// <summary>
        /// Senior voice.
        /// </summary>
        Senior
    }
}
