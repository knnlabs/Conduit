using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// OpenAI-compatible error response model for standardized error handling.
    /// </summary>
    public class OpenAIErrorResponse
    {
        /// <summary>
        /// Gets or sets the error details.
        /// </summary>
        [JsonPropertyName("error")]
        public required OpenAIError Error { get; set; }
    }

    /// <summary>
    /// OpenAI-compatible error details.
    /// </summary>
    public class OpenAIError
    {
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        [JsonPropertyName("message")]
        public required string Message { get; set; }
        
        /// <summary>
        /// Gets or sets the error type.
        /// </summary>
        [JsonPropertyName("type")]
        public required string Type { get; set; }
        
        /// <summary>
        /// Gets or sets the parameter that caused the error, if applicable.
        /// </summary>
        [JsonPropertyName("param")]
        public string? Param { get; set; }
        
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}