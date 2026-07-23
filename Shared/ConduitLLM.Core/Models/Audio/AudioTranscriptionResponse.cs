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
        public string Language { get; set; } = "unknown";

        /// <summary>Audio duration in seconds, when the provider reports it (used for billing).</summary>
        [JsonPropertyName("duration")]
        public double DurationSeconds { get; set; }

        /// <summary>The transcription operation type.</summary>
        [JsonPropertyName("task")]
        public string Task { get; set; } = "transcribe";

        /// <summary>Timestamped transcription segments.</summary>
        [JsonPropertyName("segments")]
        public List<AudioTranscriptionSegment> Segments { get; set; } = [];

        /// <summary>Timestamped words when requested.</summary>
        [JsonPropertyName("words")]
        public List<AudioTranscriptionWord>? Words { get; set; }

        /// <summary>Token log probabilities when requested.</summary>
        [JsonPropertyName("logprobs")]
        public List<AudioTranscriptionLogprob>? Logprobs { get; set; }

        /// <summary>The model that produced the transcription.</summary>
        [JsonIgnore]
        public string? Model { get; set; }

        /// <summary>Usage/cost information, when the provider reports it.</summary>
        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }

        /// <summary>Additional provider fields (e.g. segments/words for verbose_json).</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    public sealed class AudioTranscriptionSegment
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("seek")] public int Seek { get; set; }
        [JsonPropertyName("start")] public double Start { get; set; }
        [JsonPropertyName("end")] public double End { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("tokens")] public List<int> Tokens { get; set; } = [];
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
        [JsonPropertyName("avg_logprob")] public double AverageLogprob { get; set; }
        [JsonPropertyName("compression_ratio")] public double CompressionRatio { get; set; }
        [JsonPropertyName("no_speech_prob")] public double NoSpeechProbability { get; set; }
    }

    public sealed class AudioTranscriptionWord
    {
        [JsonPropertyName("word")] public string Word { get; set; } = string.Empty;
        [JsonPropertyName("start")] public double Start { get; set; }
        [JsonPropertyName("end")] public double End { get; set; }
    }

    public sealed class AudioTranscriptionLogprob
    {
        [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
        [JsonPropertyName("logprob")] public double Logprob { get; set; }
        [JsonPropertyName("bytes")] public List<int>? Bytes { get; set; }
    }
}
