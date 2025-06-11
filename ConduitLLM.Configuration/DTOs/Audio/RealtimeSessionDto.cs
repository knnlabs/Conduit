using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Audio
{
    /// <summary>
    /// DTO for real-time audio session information.
    /// </summary>
    public class RealtimeSessionDto
    {
        /// <summary>
        /// Unique session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Virtual key associated with the session.
        /// </summary>
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Provider handling the session.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Model being used for the session.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Current session state.
        /// </summary>
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// When the session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Duration of the session in seconds.
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Audio input format.
        /// </summary>
        public string? InputFormat { get; set; }

        /// <summary>
        /// Audio output format.
        /// </summary>
        public string? OutputFormat { get; set; }

        /// <summary>
        /// Language being used.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Voice being used for TTS.
        /// </summary>
        public string? Voice { get; set; }

        /// <summary>
        /// Number of turn exchanges.
        /// </summary>
        public int TurnCount { get; set; }

        /// <summary>
        /// Total input tokens used.
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Total output tokens used.
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Running cost estimate.
        /// </summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>
        /// Client IP address.
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// Client user agent.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Session metadata.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Summary metrics for real-time sessions.
    /// </summary>
    public class RealtimeSessionMetricsDto
    {
        /// <summary>
        /// Total number of active sessions.
        /// </summary>
        public int ActiveSessions { get; set; }

        /// <summary>
        /// Number of sessions by provider.
        /// </summary>
        public Dictionary<string, int> SessionsByProvider { get; set; } = new();

        /// <summary>
        /// Average session duration in seconds.
        /// </summary>
        public double AverageSessionDuration { get; set; }

        /// <summary>
        /// Total session time today in seconds.
        /// </summary>
        public double TotalSessionTimeToday { get; set; }

        /// <summary>
        /// Total estimated cost today.
        /// </summary>
        public decimal TotalCostToday { get; set; }

        /// <summary>
        /// Peak concurrent sessions today.
        /// </summary>
        public int PeakConcurrentSessions { get; set; }

        /// <summary>
        /// Time of peak concurrent sessions.
        /// </summary>
        public DateTime? PeakTime { get; set; }

        /// <summary>
        /// Session success rate percentage.
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average turns per session.
        /// </summary>
        public double AverageTurnsPerSession { get; set; }
    }
}
