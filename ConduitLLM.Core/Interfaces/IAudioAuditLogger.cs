using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for audio operation audit logging.
    /// </summary>
    public interface IAudioAuditLogger
    {
        /// <summary>
        /// Logs an audio transcription operation.
        /// </summary>
        /// <param name="entry">The audit log entry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LogTranscriptionAsync(
            AudioAuditEntry entry,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs a text-to-speech operation.
        /// </summary>
        /// <param name="entry">The audit log entry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LogTextToSpeechAsync(
            AudioAuditEntry entry,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs a real-time audio session.
        /// </summary>
        /// <param name="entry">The audit log entry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LogRealtimeSessionAsync(
            AudioAuditEntry entry,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs a content filtering event.
        /// </summary>
        /// <param name="entry">The filtering audit entry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LogContentFilteringAsync(
            ContentFilterAuditEntry entry,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs a PII detection event.
        /// </summary>
        /// <param name="entry">The PII audit entry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LogPiiDetectionAsync(
            PiiAuditEntry entry,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Base audit entry for audio operations.
    /// </summary>
    public class AudioAuditEntry
    {
        /// <summary>
        /// Gets or sets the unique ID for this audit entry.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the timestamp of the operation.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the virtual key used.
        /// </summary>
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public AudioOperation Operation { get; set; }

        /// <summary>
        /// Gets or sets the provider used.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model used.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the duration in milliseconds.
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the language code.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the client IP address.
        /// </summary>
        public string? ClientIp { get; set; }

        /// <summary>
        /// Gets or sets the user agent.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Gets or sets custom metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Audit entry for content filtering events.
    /// </summary>
    public class ContentFilterAuditEntry : AudioAuditEntry
    {
        /// <summary>
        /// Gets or sets whether content was blocked.
        /// </summary>
        public bool WasBlocked { get; set; }

        /// <summary>
        /// Gets or sets whether content was modified.
        /// </summary>
        public bool WasModified { get; set; }

        /// <summary>
        /// Gets or sets the violation categories detected.
        /// </summary>
        public List<string> ViolationCategories { get; set; } = new();

        /// <summary>
        /// Gets or sets the filter confidence score.
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Gets or sets the original content hash.
        /// </summary>
        public string? ContentHash { get; set; }
    }

    /// <summary>
    /// Audit entry for PII detection events.
    /// </summary>
    public class PiiAuditEntry : AudioAuditEntry
    {
        /// <summary>
        /// Gets or sets whether PII was detected.
        /// </summary>
        public bool PiiDetected { get; set; }

        /// <summary>
        /// Gets or sets the types of PII found.
        /// </summary>
        public List<PiiType> PiiTypes { get; set; } = new();

        /// <summary>
        /// Gets or sets the number of PII entities found.
        /// </summary>
        public int EntityCount { get; set; }

        /// <summary>
        /// Gets or sets whether PII was redacted.
        /// </summary>
        public bool WasRedacted { get; set; }

        /// <summary>
        /// Gets or sets the redaction method used.
        /// </summary>
        public RedactionMethod? RedactionMethod { get; set; }

        /// <summary>
        /// Gets or sets the risk score.
        /// </summary>
        public double RiskScore { get; set; }
    }
}