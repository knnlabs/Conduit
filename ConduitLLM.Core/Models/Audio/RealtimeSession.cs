using System.Net.WebSockets;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Represents an active real-time audio conversation session.
    /// </summary>
    public class RealtimeSession : IDisposable
    {
        /// <summary>
        /// Unique identifier for the session.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The provider hosting this session.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// The WebSocket connection for this session.
        /// </summary>
        /// <remarks>
        /// Internal use only. The actual WebSocket is managed by the client implementation.
        /// </remarks>
        internal WebSocket? WebSocket { get; set; }

        /// <summary>
        /// The configuration used to create this session.
        /// </summary>
        public RealtimeSessionConfig Config { get; set; } = new();

        /// <summary>
        /// When the session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Current state of the session.
        /// </summary>
        public SessionState State { get; set; } = SessionState.Connecting;

        /// <summary>
        /// Session metadata from the provider.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Connection information.
        /// </summary>
        public ConnectionInfo? Connection { get; set; }

        /// <summary>
        /// Statistics for the current session.
        /// </summary>
        public SessionStatistics Statistics { get; set; } = new();

        /// <summary>
        /// Disposes of the session resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the session resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (WebSocket?.State == WebSocketState.Open)
                {
                    try
                    {
                        WebSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Session disposed",
                            CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                        // Best effort cleanup
                    }
                }
                WebSocket?.Dispose();
                State = SessionState.Closed;
            }
        }
    }

    /// <summary>
    /// Session state enumeration.
    /// </summary>
    public enum SessionState
    {
        /// <summary>
        /// Session is being established.
        /// </summary>
        Connecting,

        /// <summary>
        /// Session is connected and ready.
        /// </summary>
        Connected,

        /// <summary>
        /// Session is actively in a conversation.
        /// </summary>
        Active,

        /// <summary>
        /// Session is temporarily disconnected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// Session is reconnecting.
        /// </summary>
        Reconnecting,

        /// <summary>
        /// Session has been closed.
        /// </summary>
        Closed,

        /// <summary>
        /// Session ended due to an error.
        /// </summary>
        Error
    }

    /// <summary>
    /// Connection information for a real-time session.
    /// </summary>
    public class ConnectionInfo
    {
        /// <summary>
        /// The endpoint URL for the connection.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Connection protocol version.
        /// </summary>
        public string? ProtocolVersion { get; set; }

        /// <summary>
        /// Measured latency in milliseconds.
        /// </summary>
        public double? LatencyMs { get; set; }

        /// <summary>
        /// Connection quality indicator.
        /// </summary>
        public ConnectionQuality Quality { get; set; } = ConnectionQuality.Good;
    }

    /// <summary>
    /// Connection quality levels.
    /// </summary>
    public enum ConnectionQuality
    {
        /// <summary>
        /// Excellent connection quality.
        /// </summary>
        Excellent,

        /// <summary>
        /// Good connection quality.
        /// </summary>
        Good,

        /// <summary>
        /// Fair connection quality.
        /// </summary>
        Fair,

        /// <summary>
        /// Poor connection quality.
        /// </summary>
        Poor
    }

    /// <summary>
    /// Statistics for a real-time session.
    /// </summary>
    public class SessionStatistics
    {
        /// <summary>
        /// Total duration of the session.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Total input audio duration.
        /// </summary>
        public TimeSpan InputAudioDuration { get; set; }

        /// <summary>
        /// Total output audio duration.
        /// </summary>
        public TimeSpan OutputAudioDuration { get; set; }

        /// <summary>
        /// Number of turns in the conversation.
        /// </summary>
        public int TurnCount { get; set; }

        /// <summary>
        /// Number of interruptions.
        /// </summary>
        public int InterruptionCount { get; set; }

        /// <summary>
        /// Number of function calls made.
        /// </summary>
        public int FunctionCallCount { get; set; }

        /// <summary>
        /// Total input tokens (if available).
        /// </summary>
        public int? InputTokens { get; set; }

        /// <summary>
        /// Total output tokens (if available).
        /// </summary>
        public int? OutputTokens { get; set; }

        /// <summary>
        /// Number of errors encountered.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Average response latency in milliseconds.
        /// </summary>
        public double? AverageLatencyMs { get; set; }
    }

    /// <summary>
    /// Update configuration for an active session.
    /// </summary>
    public class RealtimeSessionUpdate
    {
        /// <summary>
        /// Updated system prompt.
        /// </summary>
        public string? SystemPrompt { get; set; }

        /// <summary>
        /// Updated voice settings.
        /// </summary>
        public RealtimeVoiceSettings? VoiceSettings { get; set; }

        /// <summary>
        /// Updated turn detection settings.
        /// </summary>
        public TurnDetectionConfig? TurnDetection { get; set; }

        /// <summary>
        /// Updated temperature.
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// Updated tool definitions.
        /// </summary>
        public List<Models.Tool>? Tools { get; set; }

        /// <summary>
        /// Provider-specific updates.
        /// </summary>
        public Dictionary<string, object>? ProviderUpdates { get; set; }
    }
}
