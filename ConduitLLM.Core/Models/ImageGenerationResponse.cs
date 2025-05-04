using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents a response from an image generation request.
    /// </summary>
    /// <remarks>
    /// This class is designed to be compatible with OpenAI's image generation API response format
    /// but can be used with responses from other providers mapped to this structure.
    /// </remarks>
    public class ImageGenerationResponse
    {
        /// <summary>
        /// The Unix timestamp (in seconds) of when the image generation was created.
        /// </summary>
        [JsonPropertyName("created")]
        public required long Created { get; set; }

        /// <summary>
        /// A list of generated image data objects.
        /// The number of images matches the 'n' parameter in the request.
        /// </summary>
        [JsonPropertyName("data")]
        public required List<ImageData> Data { get; set; }
        
        /// <summary>
        /// Usage information for the request.
        /// This might include token counts for the prompt and other metrics
        /// depending on the provider.
        /// </summary>
        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    /// <summary>
    /// Represents data for a generated image, including access methods.
    /// </summary>
    /// <remarks>
    /// Depending on the request's response_format parameter, either the Url or B64Json 
    /// property will be populated, but typically not both.
    /// </remarks>
    public class ImageData
    {
        /// <summary>
        /// The URL where the generated image can be accessed.
        /// This is populated when response_format is "url" or not specified.
        /// The URL may be temporary and expire after a certain period.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Base64-encoded JSON string of the generated image.
        /// This is populated when response_format is "b64_json".
        /// Can be used to directly embed the image in applications without 
        /// requiring an additional network request.
        /// </summary>
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }
    }
}
