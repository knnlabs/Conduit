using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Base class for all real-time audio messages.
    /// </summary>
    public abstract class RealtimeMessage
    {
        /// <summary>
        /// The type of message.
        /// </summary>
        public abstract string Type { get; }

        /// <summary>
        /// Session identifier this message belongs to.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Sequence number for ordering messages.
        /// </summary>
        public long? SequenceNumber { get; set; }
    }

    /// <summary>
    /// Audio frame to be sent to the real-time service.
    /// </summary>
    public class RealtimeAudioFrame : RealtimeMessage
    {
        public override string Type => "audio.input";

        /// <summary>
        /// Raw audio data.
        /// </summary>
        public byte[] AudioData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Sample rate of the audio data.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Number of channels (1 = mono, 2 = stereo).
        /// </summary>
        public int Channels { get; set; } = 1;

        /// <summary>
        /// Whether this audio is output from the AI (vs input from user).
        /// </summary>
        public bool IsOutput { get; set; }

        /// <summary>
        /// Duration of this audio frame in milliseconds.
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// Tool response data if this frame contains a function result.
        /// </summary>
        public ToolResponse? ToolResponse { get; set; }
    }

    /// <summary>
    /// Response message from the real-time service.
    /// </summary>
    public class RealtimeResponse : RealtimeMessage
    {
        public override string Type => EventType.ToString().ToLowerInvariant();

        /// <summary>
        /// The type of event in this response.
        /// </summary>
        public RealtimeEventType EventType { get; set; }

        /// <summary>
        /// Audio output data if this is an audio event.
        /// </summary>
        public AudioDelta? Audio { get; set; }

        /// <summary>
        /// Transcription data if this is a transcription event.
        /// </summary>
        public TranscriptionDelta? Transcription { get; set; }

        /// <summary>
        /// Text response for non-audio responses.
        /// </summary>
        public string? TextResponse { get; set; }

        /// <summary>
        /// Tool call information if this is a function call.
        /// </summary>
        public RealtimeToolCall? ToolCall { get; set; }

        /// <summary>
        /// Turn event information.
        /// </summary>
        public TurnEvent? Turn { get; set; }

        /// <summary>
        /// Error information if this is an error event.
        /// </summary>
        public ErrorInfo? Error { get; set; }

        /// <summary>
        /// Session update confirmation.
        /// </summary>
        public SessionUpdateResult? SessionUpdate { get; set; }
    }

    /// <summary>
    /// Types of real-time events.
    /// </summary>
    public enum RealtimeEventType
    {
        /// <summary>
        /// Audio output from the AI.
        /// </summary>
        AudioDelta,

        /// <summary>
        /// Transcription update.
        /// </summary>
        TranscriptionDelta,

        /// <summary>
        /// Complete text response.
        /// </summary>
        TextResponse,

        /// <summary>
        /// Tool/function call request.
        /// </summary>
        ToolCallRequest,

        /// <summary>
        /// Turn has started.
        /// </summary>
        TurnStarted,

        /// <summary>
        /// Turn has ended.
        /// </summary>
        TurnEnded,

        /// <summary>
        /// Session configuration updated.
        /// </summary>
        SessionUpdated,

        /// <summary>
        /// Connection established.
        /// </summary>
        Connected,

        /// <summary>
        /// Error occurred.
        /// </summary>
        Error,

        /// <summary>
        /// Latency measurement.
        /// </summary>
        Ping,

        /// <summary>
        /// User interrupted the AI.
        /// </summary>
        Interrupted
    }

    /// <summary>
    /// Delta audio data from the AI.
    /// </summary>
    public class AudioDelta
    {
        /// <summary>
        /// The audio data chunk.
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Whether this completes the current audio response.
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// Duration of this chunk in milliseconds.
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// Item ID this audio belongs to.
        /// </summary>
        public string? ItemId { get; set; }

        /// <summary>
        /// Content index for multi-part responses.
        /// </summary>
        public int? ContentIndex { get; set; }
    }

    /// <summary>
    /// Incremental transcription update.
    /// </summary>
    public class TranscriptionDelta
    {
        /// <summary>
        /// The role of the speaker (user or assistant).
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// The transcribed text delta.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is a final transcription.
        /// </summary>
        public bool IsFinal { get; set; }

        /// <summary>
        /// Start time of this segment.
        /// </summary>
        public double? StartTime { get; set; }

        /// <summary>
        /// End time of this segment.
        /// </summary>
        public double? EndTime { get; set; }

        /// <summary>
        /// Item ID this transcription belongs to.
        /// </summary>
        public string? ItemId { get; set; }
    }

    /// <summary>
    /// Real-time tool/function call.
    /// </summary>
    public class RealtimeToolCall
    {
        /// <summary>
        /// Unique identifier for this tool call.
        /// </summary>
        public string CallId { get; set; } = string.Empty;

        /// <summary>
        /// The function name to call.
        /// </summary>
        public string FunctionName { get; set; } = string.Empty;

        /// <summary>
        /// JSON arguments for the function.
        /// </summary>
        public string Arguments { get; set; } = "{}";

        /// <summary>
        /// Type of tool (usually "function").
        /// </summary>
        public string Type { get; set; } = "function";
    }

    /// <summary>
    /// Response to a tool call.
    /// </summary>
    public class ToolResponse
    {
        /// <summary>
        /// The tool call ID this is responding to.
        /// </summary>
        public string CallId { get; set; } = string.Empty;

        /// <summary>
        /// The result of the tool call.
        /// </summary>
        public string Result { get; set; } = string.Empty;

        /// <summary>
        /// Whether the tool call was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Error message if the call failed.
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Turn event information.
    /// </summary>
    public class TurnEvent
    {
        /// <summary>
        /// Type of turn event.
        /// </summary>
        public TurnEventType EventType { get; set; }

        /// <summary>
        /// The role taking or ending the turn.
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Reason for turn end.
        /// </summary>
        public string? EndReason { get; set; }

        /// <summary>
        /// Turn identifier.
        /// </summary>
        public string? TurnId { get; set; }
    }

    /// <summary>
    /// Types of turn events.
    /// </summary>
    public enum TurnEventType
    {
        /// <summary>
        /// A turn has started.
        /// </summary>
        Started,

        /// <summary>
        /// A turn has ended.
        /// </summary>
        Ended,

        /// <summary>
        /// Turn was interrupted.
        /// </summary>
        Interrupted
    }

    /// <summary>
    /// Error information from real-time service.
    /// </summary>
    public class ErrorInfo
    {
        /// <summary>
        /// Error code.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Error severity level.
        /// </summary>
        public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;

        /// <summary>
        /// Whether this error is recoverable.
        /// </summary>
        public bool Recoverable { get; set; }

        /// <summary>
        /// Additional error details.
        /// </summary>
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// Error severity levels.
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// Informational message.
        /// </summary>
        Info,

        /// <summary>
        /// Warning that doesn't interrupt service.
        /// </summary>
        Warning,

        /// <summary>
        /// Error that may affect functionality.
        /// </summary>
        Error,

        /// <summary>
        /// Critical error requiring reconnection.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Result of a session update operation.
    /// </summary>
    public class SessionUpdateResult
    {
        /// <summary>
        /// Whether the update was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Updated fields.
        /// </summary>
        public List<string> UpdatedFields { get; set; } = new();

        /// <summary>
        /// Fields that failed to update.
        /// </summary>
        public Dictionary<string, string>? FailedFields { get; set; }

        /// <summary>
        /// New effective configuration.
        /// </summary>
        public Dictionary<string, object>? EffectiveConfig { get; set; }
    }

    /// <summary>
    /// Capabilities of a real-time audio provider.
    /// </summary>
    public class RealtimeCapabilities
    {
        /// <summary>
        /// Supported input audio formats.
        /// </summary>
        public List<RealtimeAudioFormat> SupportedInputFormats { get; set; } = new();

        /// <summary>
        /// Supported output audio formats.
        /// </summary>
        public List<RealtimeAudioFormat> SupportedOutputFormats { get; set; } = new();

        /// <summary>
        /// Available voices.
        /// </summary>
        public List<VoiceInfo> AvailableVoices { get; set; } = new();

        /// <summary>
        /// Supported languages.
        /// </summary>
        public List<string> SupportedLanguages { get; set; } = new();

        /// <summary>
        /// Turn detection options.
        /// </summary>
        public TurnDetectionCapabilities? TurnDetection { get; set; }

        /// <summary>
        /// Whether function calling is supported.
        /// </summary>
        public bool SupportsFunctionCalling { get; set; }

        /// <summary>
        /// Whether interruptions are supported.
        /// </summary>
        public bool SupportsInterruptions { get; set; }

        /// <summary>
        /// Maximum session duration in seconds.
        /// </summary>
        public int? MaxSessionDurationSeconds { get; set; }

        /// <summary>
        /// Maximum concurrent sessions.
        /// </summary>
        public int? MaxConcurrentSessions { get; set; }

        /// <summary>
        /// Provider-specific capabilities.
        /// </summary>
        public Dictionary<string, object>? ProviderCapabilities { get; set; }
    }

    /// <summary>
    /// Turn detection capability details.
    /// </summary>
    public class TurnDetectionCapabilities
    {
        /// <summary>
        /// Supported turn detection types.
        /// </summary>
        public List<TurnDetectionType> SupportedTypes { get; set; } = new();

        /// <summary>
        /// Minimum silence threshold in ms.
        /// </summary>
        public int MinSilenceThresholdMs { get; set; }

        /// <summary>
        /// Maximum silence threshold in ms.
        /// </summary>
        public int MaxSilenceThresholdMs { get; set; }

        /// <summary>
        /// Whether custom VAD parameters are supported.
        /// </summary>
        public bool SupportsCustomParameters { get; set; }
    }

}
