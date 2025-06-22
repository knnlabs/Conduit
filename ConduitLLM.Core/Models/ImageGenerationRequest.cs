using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents a request to generate images based on a text prompt.
    /// </summary>
    /// <remarks>
    /// This class is designed to be compatible with OpenAI's image generation API
    /// but can be mapped to other providers' image generation capabilities as well.
    /// </remarks>
    public class ImageGenerationRequest
    {
        /// <summary>
        /// The text prompt that describes what image to generate.
        /// More detailed prompts with clear descriptions tend to generate better results.
        /// </summary>
        [JsonPropertyName("prompt")]
        public required string Prompt { get; set; }

        /// <summary>
        /// The model to use for image generation (e.g., "dall-e-2", "dall-e-3").
        /// Defaults to "dall-e-2" if not specified.
        /// </summary>
        [JsonPropertyName("model")]
        public required string Model { get; set; } = "dall-e-2";

        /// <summary>
        /// The number of images to generate. Defaults to 1.
        /// Note that some providers may have different limits on the maximum number of images.
        /// </summary>
        [JsonPropertyName("n")]
        public int N { get; set; } = 1;

        /// <summary>
        /// The quality of the image to generate. 
        /// Options typically include "standard" or "hd" depending on the model.
        /// Not all providers or models support this parameter.
        /// </summary>
        [JsonPropertyName("quality")]
        public string? Quality { get; set; }

        /// <summary>
        /// The format in which the generated images are returned.
        /// Common values include "url" or "b64_json" (base64 encoded JSON).
        /// </summary>
        [JsonPropertyName("response_format")]
        public string? ResponseFormat { get; set; }

        /// <summary>
        /// The size of the generated image. 
        /// Common values include "256x256", "512x512", "1024x1024", and "1792x1024".
        /// Available sizes may vary by model and provider.
        /// </summary>
        [JsonPropertyName("size")]
        public string? Size { get; set; }

        /// <summary>
        /// The style of the generated image.
        /// Options typically include "vivid" or "natural" depending on the model.
        /// Not all providers or models support this parameter.
        /// </summary>
        [JsonPropertyName("style")]
        public string? Style { get; set; }

        /// <summary>
        /// A unique identifier representing your end-user, which can help
        /// monitor and detect abuse. Not all providers use this field.
        /// </summary>
        [JsonPropertyName("user")]
        public string? User { get; set; }

        /// <summary>
        /// Base64-encoded image to use as input for image-to-image generation.
        /// When provided, the prompt will be used to modify or enhance this image.
        /// Supported by providers like OpenAI (for edits/variations) and Replicate models.
        /// </summary>
        [JsonPropertyName("image")]
        public string? Image { get; set; }

        /// <summary>
        /// Base64-encoded mask image for image editing (PNG with transparency).
        /// Only the transparent areas will be edited when both image and mask are provided.
        /// Primarily used with OpenAI's image editing functionality.
        /// </summary>
        [JsonPropertyName("mask")]
        public string? Mask { get; set; }

        /// <summary>
        /// The operation type for image generation.
        /// - "generate": Standard text-to-image generation (default)
        /// - "edit": Edit existing image using prompt and optional mask
        /// - "variation": Create variations of existing image
        /// </summary>
        [JsonPropertyName("operation")]
        public string Operation { get; set; } = "generate";
    }
}
