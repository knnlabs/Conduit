using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Represents a request to convert text into speech audio.
    /// </summary>
    public class TextToSpeechRequest : AudioRequestBase
    {
        /// <summary>
        /// The text to convert to speech.
        /// </summary>
        /// <remarks>
        /// This can be plain text or SSML markup, depending on provider support
        /// and the EnableSSML flag.
        /// </remarks>
        [Required]
        [MaxLength(10000)]
        public string Input { get; set; } = string.Empty;

        /// <summary>
        /// The TTS model to use (e.g., "tts-1", "tts-1-hd").
        /// </summary>
        /// <remarks>
        /// Different models may offer different quality levels,
        /// latency characteristics, or voice options.
        /// </remarks>
        public string? Model { get; set; }

        /// <summary>
        /// The voice ID to use for synthesis.
        /// </summary>
        /// <remarks>
        /// Voice IDs are provider-specific. Common examples include
        /// "alloy", "echo", "fable" for OpenAI, or specific voice IDs
        /// for ElevenLabs and other providers.
        /// </remarks>
        [Required]
        public string Voice { get; set; } = string.Empty;

        /// <summary>
        /// The desired audio output format.
        /// </summary>
        /// <remarks>
        /// Common formats include mp3, wav, flac, ogg, aac.
        /// Default varies by provider.
        /// </remarks>
        public AudioFormat? ResponseFormat { get; set; }

        /// <summary>
        /// The speed of the generated speech (0.25-4.0).
        /// </summary>
        /// <remarks>
        /// 1.0 is normal speed, < 1.0 is slower, > 1.0 is faster.
        /// Not all providers support speed adjustment.
        /// </remarks>
        [Range(0.25, 4.0)]
        public double? Speed { get; set; }

        /// <summary>
        /// The pitch adjustment for the voice.
        /// </summary>
        /// <remarks>
        /// Provider-specific. Often a percentage or semitone adjustment.
        /// May not be supported by all providers.
        /// </remarks>
        public double? Pitch { get; set; }

        /// <summary>
        /// The volume/gain adjustment (0-1).
        /// </summary>
        /// <remarks>
        /// 1.0 is normal volume. Some providers may support values > 1.0
        /// for amplification.
        /// </remarks>
        [Range(0.0, 2.0)]
        public double? Volume { get; set; }

        /// <summary>
        /// Advanced voice settings for providers that support them.
        /// </summary>
        /// <remarks>
        /// Includes provider-specific parameters like emotion, style,
        /// or voice characteristics.
        /// </remarks>
        public VoiceSettings? VoiceSettings { get; set; }

        /// <summary>
        /// The language code for synthesis (ISO-639-1).
        /// </summary>
        /// <remarks>
        /// Some voices support multiple languages. This ensures
        /// proper pronunciation for the target language.
        /// </remarks>
        [RegularExpression(@"^[a-z]{2}(-[A-Z]{2})?$", ErrorMessage = "Language must be in ISO-639-1 format")]
        public string? Language { get; set; }

        /// <summary>
        /// Whether the input contains SSML markup.
        /// </summary>
        /// <remarks>
        /// When true, the input is interpreted as SSML (Speech Synthesis Markup Language)
        /// allowing fine control over pronunciation, pauses, emphasis, etc.
        /// </remarks>
        public bool? EnableSSML { get; set; }

        /// <summary>
        /// Sample rate for the output audio in Hz.
        /// </summary>
        /// <remarks>
        /// Common values: 8000 (telephone), 16000 (wideband), 24000 (high quality).
        /// Provider may override based on format selection.
        /// </remarks>
        public int? SampleRate { get; set; }

        /// <summary>
        /// Whether to optimize for streaming playback.
        /// </summary>
        /// <remarks>
        /// When true, the provider may optimize chunk sizes and
        /// encoding for progressive playback.
        /// </remarks>
        public bool? OptimizeStreaming { get; set; }

        /// <summary>
        /// Validates the request.
        /// </summary>
        public override bool IsValid(out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(Input))
            {
                errorMessage = "Input text is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Voice))
            {
                errorMessage = "Voice selection is required";
                return false;
            }

            if (Input.Length > 10000)
            {
                errorMessage = "Input text exceeds maximum length of 10000 characters";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Advanced voice settings for TTS.
    /// </summary>
    public class VoiceSettings
    {
        /// <summary>
        /// Emotional tone (provider-specific scale).
        /// </summary>
        /// <remarks>
        /// For ElevenLabs, this might be "stability" (0-1).
        /// For other providers, it could be emotion names.
        /// </remarks>
        public double? Emotion { get; set; }

        /// <summary>
        /// Voice style preset.
        /// </summary>
        /// <remarks>
        /// Examples: "news", "conversational", "narrative", "cheerful".
        /// Support varies by provider and voice.
        /// </remarks>
        public string? Style { get; set; }

        /// <summary>
        /// Emphasis level for the speech.
        /// </summary>
        /// <remarks>
        /// Controls how much emphasis or expressiveness to add.
        /// Scale and support vary by provider.
        /// </remarks>
        public double? Emphasis { get; set; }

        /// <summary>
        /// Voice similarity/consistency (ElevenLabs specific).
        /// </summary>
        public double? SimilarityBoost { get; set; }

        /// <summary>
        /// Voice stability (ElevenLabs specific).
        /// </summary>
        public double? Stability { get; set; }

        /// <summary>
        /// Additional provider-specific settings.
        /// </summary>
        public Dictionary<string, object>? CustomSettings { get; set; }
    }

    /// <summary>
    /// Audio format options for TTS output.
    /// </summary>
    public enum AudioFormat
    {
        /// <summary>
        /// MP3 format (most compatible).
        /// </summary>
        Mp3,

        /// <summary>
        /// WAV format (uncompressed).
        /// </summary>
        Wav,

        /// <summary>
        /// FLAC format (lossless compression).
        /// </summary>
        Flac,

        /// <summary>
        /// OGG Vorbis format.
        /// </summary>
        Ogg,

        /// <summary>
        /// AAC format.
        /// </summary>
        Aac,

        /// <summary>
        /// Opus format (optimized for speech).
        /// </summary>
        Opus,

        /// <summary>
        /// PCM raw audio data.
        /// </summary>
        Pcm,

        /// <summary>
        /// μ-law format (telephony).
        /// </summary>
        Ulaw,

        /// <summary>
        /// A-law format (telephony).
        /// </summary>
        Alaw
    }
}