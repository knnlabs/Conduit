using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Configuration for audio-specific provider settings.
    /// </summary>
    public class AudioProviderConfig
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to Provider.
        /// </summary>
        public int ProviderId { get; set; }

        /// <summary>
        /// Navigation property to provider.
        /// </summary>
        [ForeignKey(nameof(ProviderId))]
        public virtual Provider Provider { get; set; } = null!;

        /// <summary>
        /// Whether transcription is enabled for this provider.
        /// </summary>
        public bool TranscriptionEnabled { get; set; } = true;

        /// <summary>
        /// Default transcription model (e.g., "whisper-1").
        /// </summary>
        [MaxLength(100)]
        public string? DefaultTranscriptionModel { get; set; }

        /// <summary>
        /// Whether text-to-speech is enabled for this provider.
        /// </summary>
        public bool TextToSpeechEnabled { get; set; } = true;

        /// <summary>
        /// Default TTS model (e.g., "tts-1").
        /// </summary>
        [MaxLength(100)]
        public string? DefaultTTSModel { get; set; }

        /// <summary>
        /// Default TTS voice (e.g., "alloy").
        /// </summary>
        [MaxLength(100)]
        public string? DefaultTTSVoice { get; set; }

        /// <summary>
        /// Whether real-time audio is enabled.
        /// </summary>
        public bool RealtimeEnabled { get; set; } = false;

        /// <summary>
        /// Default real-time model.
        /// </summary>
        [MaxLength(100)]
        public string? DefaultRealtimeModel { get; set; }

        /// <summary>
        /// WebSocket endpoint for real-time audio (if different from base).
        /// </summary>
        [MaxLength(500)]
        public string? RealtimeEndpoint { get; set; }

        /// <summary>
        /// JSON configuration for provider-specific audio settings.
        /// </summary>
        public string? CustomSettings { get; set; }

        /// <summary>
        /// Priority for audio routing (higher = preferred).
        /// </summary>
        public int RoutingPriority { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
