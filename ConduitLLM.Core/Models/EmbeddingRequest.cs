using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    public class EmbeddingRequest
    {
        [JsonPropertyName("input")]
        public required object Input { get; set; } // string, or array of strings/tokens

        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("encoding_format")]
        public required string EncodingFormat { get; set; } = "float";

        [JsonPropertyName("dimensions")]
        public int? Dimensions { get; set; }

        [JsonPropertyName("user")]
        public string? User { get; set; }
    }
}
