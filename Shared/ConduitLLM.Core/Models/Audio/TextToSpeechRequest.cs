using System.Text.Json;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// A text-to-speech synthesis request (OpenAI <c>/audio/speech</c> compatible).
    /// </summary>
    public class TextToSpeechRequest
    {
        /// <summary>The model (alias) to synthesize with.</summary>
        public required string Model { get; set; }

        /// <summary>The text to synthesize.</summary>
        public required string Input { get; set; }

        /// <summary>The voice identifier (provider/model specific).</summary>
        public required string Voice { get; set; }

        /// <summary>
        /// Output audio format, e.g. <c>mp3</c> or <c>pcm</c>. Defaults to <c>mp3</c> (OpenAI-compatible
        /// default; OpenRouter's own default is pcm, so we normalize to mp3 for stored/played audio).
        /// </summary>
        public string? ResponseFormat { get; set; } = "mp3";

        /// <summary>Optional playback speed multiplier (provider support varies).</summary>
        public double? Speed { get; set; }

        /// <summary>Provider-specific passthrough fields.</summary>
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
