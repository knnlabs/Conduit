using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Logs audio operation usage for tracking and billing.
    /// </summary>
    public class AudioUsageLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// Virtual key used for the request.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Provider that handled the request.
        /// </summary>
        [Required]
        public ProviderType Provider { get; set; }

        /// <summary>
        /// Type of audio operation.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Model used for the operation.
        /// </summary>
        [MaxLength(100)]
        public string? Model { get; set; }

        /// <summary>
        /// Request identifier for correlation.
        /// </summary>
        [MaxLength(100)]
        public string? RequestId { get; set; }

        /// <summary>
        /// Session ID for real-time sessions.
        /// </summary>
        [MaxLength(100)]
        public string? SessionId { get; set; }

        /// <summary>
        /// Duration in seconds (for audio operations).
        /// </summary>
        public double? DurationSeconds { get; set; }

        /// <summary>
        /// Character count (for TTS operations).
        /// </summary>
        public int? CharacterCount { get; set; }

        /// <summary>
        /// Input tokens (for real-time with LLM).
        /// </summary>
        public int? InputTokens { get; set; }

        /// <summary>
        /// Output tokens (for real-time with LLM).
        /// </summary>
        public int? OutputTokens { get; set; }

        /// <summary>
        /// Calculated cost in USD.
        /// </summary>
        [Column(TypeName = "decimal(10, 6)")]
        public decimal Cost { get; set; }

        /// <summary>
        /// Language code used.
        /// </summary>
        [MaxLength(10)]
        public string? Language { get; set; }

        /// <summary>
        /// Voice ID used (for TTS/realtime).
        /// </summary>
        [MaxLength(100)]
        public string? Voice { get; set; }

        /// <summary>
        /// HTTP status code of the response.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Error message if operation failed.
        /// </summary>
        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Client IP address.
        /// </summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent string.
        /// </summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Additional metadata as JSON.
        /// </summary>
        public string? Metadata { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
