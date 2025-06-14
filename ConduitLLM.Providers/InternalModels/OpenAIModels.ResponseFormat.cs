using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.InternalModels
{
    /// <summary>
    /// Represents the OpenAI response format specification.
    /// </summary>
    internal record ResponseFormat
    {
        /// <summary>
        /// Gets or sets the type of response format.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; init; } = "text";
    }
}
