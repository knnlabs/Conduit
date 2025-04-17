using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    public class ImageGenerationResponse
    {
        [JsonPropertyName("created")]
        public required long Created { get; set; }

        [JsonPropertyName("data")]
        public required List<ImageData> Data { get; set; }
        
        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    public class ImageData
    {
        [JsonPropertyName("url")]
        public required string? Url { get; set; }

        [JsonPropertyName("b64_json")]
        public required string? B64Json { get; set; }
    }
}
