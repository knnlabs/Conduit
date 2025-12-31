using System.Text.Json.Serialization;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Represents the result categories for API key testing
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ApiKeyTestResult
    {
        Success,
        InvalidKey,
        Ignored,
        ProviderDown,
        RateLimited,
        UnknownError
    }

    /// <summary>
    /// Additional details about the API key test result
    /// </summary>
    public class ApiKeyTestDetails
    {
        /// <summary>
        /// Response time in milliseconds
        /// </summary>
        public double? ResponseTimeMs { get; set; }

        /// <summary>
        /// List of models available from the provider
        /// </summary>
        public string[]? ModelsAvailable { get; set; }

        /// <summary>
        /// Raw provider message for debugging
        /// </summary>
        public string? ProviderMessage { get; set; }

        /// <summary>
        /// Provider-specific error code
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// HTTP status code from the provider
        /// </summary>
        public int? StatusCode { get; set; }
    }

    /// <summary>
    /// Standardized response for API key testing operations
    /// </summary>
    public class StandardApiKeyTestResponse
    {
        /// <summary>
        /// The result category of the test
        /// </summary>
        public ApiKeyTestResult Result { get; set; }

        /// <summary>
        /// User-friendly message describing the result
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Additional details about the test result
        /// </summary>
        public ApiKeyTestDetails? Details { get; set; }
    }
}