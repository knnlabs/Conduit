using System;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.Audio
{
    /// <summary>
    /// DTO for audio provider configuration.
    /// </summary>
    public class AudioProviderConfigDto
    {
        /// <summary>
        /// Unique identifier for the configuration.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Associated provider credential ID.
        /// </summary>
        public int ProviderId { get; set; }

        /// <summary>
        /// Provider type from the credential.
        /// </summary>
        public ProviderType? ProviderType { get; set; }

        /// <summary>
        /// Whether transcription is enabled for this provider.
        /// </summary>
        public bool TranscriptionEnabled { get; set; } = true;

        /// <summary>
        /// Default transcription model.
        /// </summary>
        [MaxLength(100)]
        public string? DefaultTranscriptionModel { get; set; }

        /// <summary>
        /// Whether text-to-speech is enabled for this provider.
        /// </summary>
        public bool TextToSpeechEnabled { get; set; } = true;

        /// <summary>
        /// Default TTS model.
        /// </summary>
        [MaxLength(100)]
        public string? DefaultTTSModel { get; set; }

        /// <summary>
        /// Default TTS voice.
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
        /// WebSocket endpoint for real-time audio.
        /// </summary>
        [MaxLength(500)]
        public string? RealtimeEndpoint { get; set; }

        /// <summary>
        /// JSON configuration for provider-specific settings.
        /// </summary>
        public string? CustomSettings { get; set; }

        /// <summary>
        /// Priority for audio routing (higher = preferred).
        /// </summary>
        public int RoutingPriority { get; set; } = 100;

        /// <summary>
        /// When the configuration was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the configuration was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
        
    }

    /// <summary>
    /// DTO for creating a new audio provider configuration.
    /// </summary>
    public class CreateAudioProviderConfigDto
    {
        /// <summary>
        /// Associated provider credential ID.
        /// </summary>
        [Required]
        public int ProviderId { get; set; }

        /// <summary>
        /// Whether transcription is enabled for this provider.
        /// </summary>
        public bool TranscriptionEnabled { get; set; } = true;

        /// <summary>
        /// Default transcription model.
        /// </summary>
        [MaxLength(100)]
        public string? DefaultTranscriptionModel { get; set; }

        /// <summary>
        /// Whether text-to-speech is enabled for this provider.
        /// </summary>
        public bool TextToSpeechEnabled { get; set; } = true;

        /// <summary>
        /// Default TTS model.
        /// </summary>
        [MaxLength(100)]
        public string? DefaultTTSModel { get; set; }

        /// <summary>
        /// Default TTS voice.
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
        /// WebSocket endpoint for real-time audio.
        /// </summary>
        [MaxLength(500)]
        public string? RealtimeEndpoint { get; set; }

        /// <summary>
        /// JSON configuration for provider-specific settings.
        /// </summary>
        public string? CustomSettings { get; set; }

        /// <summary>
        /// Priority for audio routing (higher = preferred).
        /// </summary>
        public int RoutingPriority { get; set; } = 100;
    }

    /// <summary>
    /// DTO for updating an audio provider configuration.
    /// </summary>
    public class UpdateAudioProviderConfigDto : CreateAudioProviderConfigDto
    {
    }
}
