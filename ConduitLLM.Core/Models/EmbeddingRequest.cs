using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents a request to create embeddings for input text.
    /// </summary>
    /// <remarks>
    /// Embeddings are vector representations of text that capture semantic meaning,
    /// allowing for semantic search, clustering, and other machine learning operations.
    /// This class is compatible with OpenAI's embeddings API but can be mapped to other providers.
    /// </remarks>
    public class EmbeddingRequest
    {
        /// <summary>
        /// The input text to get embeddings for.
        /// Can be a string, an array of strings, or an array of tokens.
        /// Each provider may have different limits on the maximum input length.
        /// </summary>
        /// <remarks>
        /// When using batch mode with an array of inputs, each input should represent
        /// a separate piece of text to be embedded independently.
        /// </remarks>
        [JsonPropertyName("input")]
        public required object Input { get; set; }

        /// <summary>
        /// The ID of the model to use for generating embeddings.
        /// Each provider offers different embedding models with various dimensions
        /// and performance characteristics.
        /// </summary>
        /// <remarks>
        /// Common models include "text-embedding-ada-002" from OpenAI,
        /// "text-embedding-3-small" and "text-embedding-3-large" from newer OpenAI models.
        /// </remarks>
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        /// <summary>
        /// The format in which the embeddings are returned.
        /// </summary>
        /// <remarks>
        /// Most commonly "float" for floating-point values.
        /// Some providers also support other formats like "base64" or "integer".
        /// </remarks>
        [JsonPropertyName("encoding_format")]
        public required string EncodingFormat { get; set; } = "float";

        /// <summary>
        /// The number of dimensions the resulting output embeddings should have.
        /// </summary>
        /// <remarks>
        /// This is only supported by some providers and models.
        /// When not specified, the model's default dimensions are used.
        /// For example, "text-embedding-3-small" supports adjustable dimensions 
        /// from 256 to 1536, while other models have fixed dimensions.
        /// </remarks>
        [JsonPropertyName("dimensions")]
        public int? Dimensions { get; set; }

        /// <summary>
        /// A unique identifier representing the end-user, which can help
        /// to monitor and detect abuse.
        /// </summary>
        [JsonPropertyName("user")]
        public string? User { get; set; }
    }
}
