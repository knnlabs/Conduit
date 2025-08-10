using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.OpenAI.Models
{
    /// <summary>
    /// OpenAI-specific audio transcription response model.
    /// </summary>
    public class TranscriptionResponse
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; }

        [JsonPropertyName("segments")]
        public List<TranscriptionSegment>? Segments { get; set; }

        [JsonPropertyName("words")]
        public List<TranscriptionWord>? Words { get; set; }
    }

    /// <summary>
    /// OpenAI-specific transcription segment.
    /// </summary>
    public class TranscriptionSegment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("tokens")]
        public List<int>? Tokens { get; set; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("avg_logprob")]
        public double? AvgLogprob { get; set; }

        [JsonPropertyName("compression_ratio")]
        public double? CompressionRatio { get; set; }

        [JsonPropertyName("no_speech_prob")]
        public double? NoSpeechProb { get; set; }
    }

    /// <summary>
    /// OpenAI-specific transcription word.
    /// </summary>
    public class TranscriptionWord
    {
        [JsonPropertyName("word")]
        public string Word { get; set; } = string.Empty;

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }
    }

    /// <summary>
    /// OpenAI text-to-speech request model.
    /// </summary>
    public class TextToSpeechRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "tts-1";

        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;

        [JsonPropertyName("voice")]
        public string Voice { get; set; } = string.Empty;

        [JsonPropertyName("response_format")]
        public string? ResponseFormat { get; set; }

        [JsonPropertyName("speed")]
        public double? Speed { get; set; }
    }
}
