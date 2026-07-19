namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// A text-to-speech synthesis response carrying the raw audio bytes.
    /// </summary>
    public class TextToSpeechResponse
    {
        /// <summary>The synthesized audio bytes.</summary>
        public required byte[] AudioData { get; set; }

        /// <summary>The audio content type, e.g. <c>audio/mpeg</c> or <c>audio/pcm</c>.</summary>
        public required string ContentType { get; set; }

        /// <summary>The model that produced the audio.</summary>
        public string? Model { get; set; }

        /// <summary>Usage/cost information (character count is the billable unit for TTS).</summary>
        public Usage? Usage { get; set; }
    }
}
