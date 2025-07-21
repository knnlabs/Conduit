using System;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Cache entry for TTS (Text-to-Speech) responses.
    /// </summary>
    public class TtsCacheEntry
    {
        /// <summary>
        /// Gets or sets the TTS response data.
        /// </summary>
        public TextToSpeechResponse Response { get; set; } = new();

        /// <summary>
        /// Gets or sets the UTC timestamp when this entry was cached.
        /// </summary>
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// Gets or sets the size of the audio data in bytes.
        /// </summary>
        public long SizeBytes { get; set; }
    }
}