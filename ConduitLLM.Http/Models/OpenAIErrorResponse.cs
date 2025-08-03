using System.Text.Json.Serialization;

namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// OpenAI-compatible error response.
    /// </summary>
    public class OpenAIErrorResponse
    {
        [JsonPropertyName("error")]
        public required OpenAIError Error { get; set; }
    }

    /// <summary>
    /// OpenAI-compatible error details.
    /// </summary>
    public class OpenAIError
    {
        [JsonPropertyName("message")]
        public required string Message { get; set; }
        
        [JsonPropertyName("type")]
        public required string Type { get; set; }
        
        [JsonPropertyName("param")]
        public string? Param { get; set; }
        
        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}