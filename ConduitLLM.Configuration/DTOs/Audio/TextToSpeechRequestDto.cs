using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConduitLLM.Configuration.DTOs.Audio
{
    /// <summary>
    /// DTO for text-to-speech requests.
    /// </summary>
    public class TextToSpeechRequestDto
    {
        [Required]
        [JsonPropertyName("model")]
        public string Model { get; set; } = "tts-1";

        [Required]
        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("voice")]
        public string Voice { get; set; } = "alloy";

        [JsonPropertyName("response_format")]
        public string? ResponseFormat { get; set; }

        [JsonPropertyName("speed")]
        [Range(0.25, 4.0)]
        public double? Speed { get; set; }
    }
}