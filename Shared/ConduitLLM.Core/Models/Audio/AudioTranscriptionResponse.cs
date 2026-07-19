using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// A speech-to-text transcription response.
    /// </summary>
    public class AudioTranscriptionResponse
    {
        /// <summary>The transcribed text.</summary>
        [JsonPropertyName("text")]
        public required string Text { get; set; }

        /// <summary>Detected/echoed language, when the provider reports it.</summary>
        [JsonPropertyName("language")]
        public string? Language { get; set; }

        /// <summary>Audio duration in seconds, when the provider reports it (used for billing).</summary>
        [JsonPropertyName("duration")]
        public double? DurationSeconds { get; set; }

        /// <summary>The model that produced the transcription.</summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>Usage/cost information, when the provider reports it.</summary>
        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }

        /// <summary>Additional provider fields (e.g. segments/words for verbose_json).</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
