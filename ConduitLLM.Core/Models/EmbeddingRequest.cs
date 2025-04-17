using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    public class EmbeddingRequest
    {
        [JsonPropertyName("input")]
        public object Input { get; set; } // string, or array of strings/tokens

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("encoding_format")]
        public string EncodingFormat { get; set; }

        [JsonPropertyName("dimensions")]
        public int? Dimensions { get; set; }

        [JsonPropertyName("user")]
        public string? User { get; set; }
    }
}
