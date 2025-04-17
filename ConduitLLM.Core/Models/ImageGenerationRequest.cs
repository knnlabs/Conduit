using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    public class ImageGenerationRequest
    {
        [JsonPropertyName("prompt")]
        public required string Prompt { get; set; }

        [JsonPropertyName("model")]
        public required string Model { get; set; } = "dall-e-2"; // Default to dall-e-2 if not specified

        [JsonPropertyName("n")]
        public int N { get; set; } = 1; // Default to 1 image

        [JsonPropertyName("quality")]
        public string? Quality { get; set; }

        [JsonPropertyName("response_format")]
        public string? ResponseFormat { get; set; }

        [JsonPropertyName("size")]
        public string? Size { get; set; }

        [JsonPropertyName("style")]
        public string? Style { get; set; }

        [JsonPropertyName("user")]
        public string? User { get; set; }
    }
}
