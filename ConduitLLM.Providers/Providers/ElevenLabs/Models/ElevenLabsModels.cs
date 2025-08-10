using System.Collections.Generic;

namespace ConduitLLM.Providers.ElevenLabs.Models
{
    /// <summary>
    /// Response model for ElevenLabs voices endpoint.
    /// </summary>
    internal class ElevenLabsVoicesResponse
    {
        public List<ElevenLabsVoice>? Voices { get; set; }
    }

    /// <summary>
    /// Represents a voice from ElevenLabs.
    /// </summary>
    internal class ElevenLabsVoice
    {
        public string VoiceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? PreviewUrl { get; set; }
        public ElevenLabsVoiceLabels? Labels { get; set; }
    }

    /// <summary>
    /// Labels associated with an ElevenLabs voice.
    /// </summary>
    internal class ElevenLabsVoiceLabels
    {
        public string? Language { get; set; }
        public string? Gender { get; set; }
    }
}