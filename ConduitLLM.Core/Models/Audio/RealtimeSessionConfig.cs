using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Configuration for establishing a real-time audio conversation session.
    /// </summary>
    public class RealtimeSessionConfig
    {
        /// <summary>
        /// The model to use for the conversation (e.g., "gpt-4o-realtime-preview").
        /// </summary>
        /// <remarks>
        /// Model availability varies by provider. Examples:
        /// - OpenAI: "gpt-4o-realtime-preview"
        /// - Ultravox: "ultravox-v1"
        /// - ElevenLabs: agent ID or "default-agent"
        /// </remarks>
        public string? Model { get; set; }

        /// <summary>
        /// The voice to use for AI responses.
        /// </summary>
        /// <remarks>
        /// Voice IDs are provider-specific. Examples:
        /// - OpenAI: "alloy", "echo", "shimmer"
        /// - ElevenLabs: specific voice IDs
        /// - Ultravox: voice names or IDs
        /// </remarks>
        [Required]
        public string Voice { get; set; } = string.Empty;

        /// <summary>
        /// Audio format for input (user speech).
        /// </summary>
        public RealtimeAudioFormat InputFormat { get; set; } = RealtimeAudioFormat.PCM16_24kHz;

        /// <summary>
        /// Audio format for output (AI speech).
        /// </summary>
        public RealtimeAudioFormat OutputFormat { get; set; } = RealtimeAudioFormat.PCM16_24kHz;

        /// <summary>
        /// The language for the conversation (ISO-639-1).
        /// </summary>
        [RegularExpression(@"^[a-z]{2}(-[A-Z]{2})?$", ErrorMessage = "Language must be in ISO-639-1 format")]
        public string? Language { get; set; } = "en";

        /// <summary>
        /// System prompt to set the AI's behavior and context.
        /// </summary>
        [MaxLength(2000)]
        public string? SystemPrompt { get; set; }

        /// <summary>
        /// Turn detection configuration.
        /// </summary>
        /// <remarks>
        /// Controls how the system detects when the user has finished speaking
        /// and when to start the AI response.
        /// </remarks>
        public TurnDetectionConfig TurnDetection { get; set; } = new();

        /// <summary>
        /// Function definitions for tool use during conversation.
        /// </summary>
        /// <remarks>
        /// Allows the AI to call functions during the conversation.
        /// Not supported by all providers.
        /// </remarks>
        public List<Models.Tool>? Tools { get; set; }

        /// <summary>
        /// Transcription settings for the session.
        /// </summary>
        public TranscriptionConfig? Transcription { get; set; }

        /// <summary>
        /// Voice customization settings.
        /// </summary>
        public RealtimeVoiceSettings? VoiceSettings { get; set; }

        /// <summary>
        /// Temperature for response generation (0-2).
        /// </summary>
        [Range(0.0, 2.0)]
        public double? Temperature { get; set; }

        /// <summary>
        /// Maximum response duration in seconds.
        /// </summary>
        /// <remarks>
        /// Limits how long the AI can speak in a single turn.
        /// </remarks>
        [Range(1, 300)]
        public int? MaxResponseDurationSeconds { get; set; }

        /// <summary>
        /// Conversation mode preset.
        /// </summary>
        public ConversationMode Mode { get; set; } = ConversationMode.Conversational;

        /// <summary>
        /// Provider-specific configuration options.
        /// </summary>
        public Dictionary<string, object>? ProviderConfig { get; set; }
    }

    /// <summary>
    /// Configuration for turn detection in real-time conversations.
    /// </summary>
    public class TurnDetectionConfig
    {
        /// <summary>
        /// Whether turn detection is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Type of turn detection to use.
        /// </summary>
        public TurnDetectionType Type { get; set; } = TurnDetectionType.ServerVAD;

        /// <summary>
        /// Silence duration in milliseconds before ending turn.
        /// </summary>
        /// <remarks>
        /// How long to wait after speech stops before considering
        /// the turn complete. Typical range: 300-1000ms.
        /// </remarks>
        [Range(100, 5000)]
        public int? SilenceThresholdMs { get; set; } = 500;

        /// <summary>
        /// Audio level threshold for voice activity detection.
        /// </summary>
        /// <remarks>
        /// Provider-specific. Often a value between 0-1 or in decibels.
        /// </remarks>
        public double? Threshold { get; set; }

        /// <summary>
        /// Padding to include before detected speech starts (ms).
        /// </summary>
        /// <remarks>
        /// Captures a bit of audio before speech is detected to avoid
        /// cutting off the beginning of utterances.
        /// </remarks>
        [Range(0, 1000)]
        public int? PrefixPaddingMs { get; set; } = 300;
    }

    /// <summary>
    /// Configuration for transcription during real-time sessions.
    /// </summary>
    public class TranscriptionConfig
    {
        /// <summary>
        /// Whether to enable transcription of user speech.
        /// </summary>
        public bool EnableUserTranscription { get; set; } = true;

        /// <summary>
        /// Whether to enable transcription of AI speech.
        /// </summary>
        public bool EnableAssistantTranscription { get; set; } = true;

        /// <summary>
        /// Whether to include partial (interim) transcriptions.
        /// </summary>
        public bool IncludePartialTranscriptions { get; set; } = true;

        /// <summary>
        /// Transcription model to use if different from conversation model.
        /// </summary>
        public string? TranscriptionModel { get; set; }
    }

    /// <summary>
    /// Voice settings for real-time conversations.
    /// </summary>
    public class RealtimeVoiceSettings
    {
        /// <summary>
        /// Speech speed adjustment (0.5-2.0, where 1.0 is normal).
        /// </summary>
        [Range(0.5, 2.0)]
        public double? Speed { get; set; }

        /// <summary>
        /// Pitch adjustment (provider-specific scale).
        /// </summary>
        public double? Pitch { get; set; }

        /// <summary>
        /// Voice stability (ElevenLabs specific, 0-1).
        /// </summary>
        [Range(0.0, 1.0)]
        public double? Stability { get; set; }

        /// <summary>
        /// Similarity boost (ElevenLabs specific, 0-1).
        /// </summary>
        [Range(0.0, 1.0)]
        public double? SimilarityBoost { get; set; }

        /// <summary>
        /// Emotional style or tone.
        /// </summary>
        public string? Style { get; set; }

        /// <summary>
        /// Provider-specific voice settings.
        /// </summary>
        public Dictionary<string, object>? CustomSettings { get; set; }
    }

    /// <summary>
    /// Real-time audio format specifications.
    /// </summary>
    public enum RealtimeAudioFormat
    {
        /// <summary>
        /// 16-bit PCM at 8kHz (telephone quality).
        /// </summary>
        PCM16_8kHz,

        /// <summary>
        /// 16-bit PCM at 16kHz (wideband).
        /// </summary>
        PCM16_16kHz,

        /// <summary>
        /// 16-bit PCM at 24kHz (high quality).
        /// </summary>
        PCM16_24kHz,

        /// <summary>
        /// 16-bit PCM at 48kHz (studio quality).
        /// </summary>
        PCM16_48kHz,

        /// <summary>
        /// G.711 μ-law at 8kHz (telephony).
        /// </summary>
        G711_ULAW,

        /// <summary>
        /// G.711 A-law at 8kHz (telephony).
        /// </summary>
        G711_ALAW,

        /// <summary>
        /// Opus codec (variable bitrate).
        /// </summary>
        Opus,

        /// <summary>
        /// MP3 format (compressed).
        /// </summary>
        MP3
    }

    /// <summary>
    /// Turn detection types.
    /// </summary>
    public enum TurnDetectionType
    {
        /// <summary>
        /// Server-side voice activity detection.
        /// </summary>
        ServerVAD,

        /// <summary>
        /// Manual turn control by the client.
        /// </summary>
        Manual,

        /// <summary>
        /// Push-to-talk mode.
        /// </summary>
        PushToTalk
    }

    /// <summary>
    /// Conversation mode presets.
    /// </summary>
    public enum ConversationMode
    {
        /// <summary>
        /// Natural conversational style with interruptions allowed.
        /// </summary>
        Conversational,

        /// <summary>
        /// Interview style with clear turn-taking.
        /// </summary>
        Interview,

        /// <summary>
        /// Command mode for short interactions.
        /// </summary>
        Command,

        /// <summary>
        /// Presentation mode with minimal interruptions.
        /// </summary>
        Presentation,

        /// <summary>
        /// Custom mode with manual settings.
        /// </summary>
        Custom
    }
}
