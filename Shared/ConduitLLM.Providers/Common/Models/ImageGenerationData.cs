using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.Common.Models
{
    /// <summary>
    /// Represents common image generation data structures used across providers.
    /// </summary>
    public class ImageGenerationData
    {
        /// <summary>
        /// Gets or sets the generated image URL.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the base64-encoded image data.
        /// </summary>
        [JsonPropertyName("b64_json")]
        public string? Base64Json { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the image has been revised or filtered.
        /// </summary>
        [JsonPropertyName("revised_prompt")]
        public bool? RevisedPrompt { get; set; }
    }

    /// <summary>
    /// Collection of image generation data objects.
    /// </summary>
    public class ImageGenerationDataCollection
    {
        /// <summary>
        /// Gets or sets the list of image generation data items.
        /// </summary>
        [JsonPropertyName("data")]
        public List<ImageGenerationData>? Data { get; set; }
    }
}
