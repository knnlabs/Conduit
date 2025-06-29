using System.Text.Json.Serialization;

namespace ConduitLLM.CoreClient.Models
{
    /// <summary>
    /// Represents a discovered model with its capabilities.
    /// </summary>
    public class DiscoveredModel
    {
        /// <summary>
        /// The model identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// The provider that hosts this model.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the model.
        /// </summary>
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Model capabilities.
        /// </summary>
        [JsonPropertyName("capabilities")]
        public ModelCapabilities Capabilities { get; set; } = new();

        /// <summary>
        /// Additional metadata about the model.
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// When this model's capabilities were last verified.
        /// </summary>
        [JsonPropertyName("last_verified")]
        public DateTime LastVerified { get; set; }
    }

    /// <summary>
    /// Model capabilities that match the ILLMClient interface.
    /// </summary>
    public class ModelCapabilities
    {
        /// <summary>
        /// Supports chat completions.
        /// </summary>
        [JsonPropertyName("chat")]
        public bool Chat { get; set; }

        /// <summary>
        /// Supports streaming chat completions.
        /// </summary>
        [JsonPropertyName("chat_stream")]
        public bool ChatStream { get; set; }

        /// <summary>
        /// Supports embeddings.
        /// </summary>
        [JsonPropertyName("embeddings")]
        public bool Embeddings { get; set; }

        /// <summary>
        /// Supports image generation.
        /// </summary>
        [JsonPropertyName("image_generation")]
        public bool ImageGeneration { get; set; }

        /// <summary>
        /// Supports vision/multimodal inputs.
        /// </summary>
        [JsonPropertyName("vision")]
        public bool Vision { get; set; }

        /// <summary>
        /// Supports video generation.
        /// </summary>
        [JsonPropertyName("video_generation")]
        public bool VideoGeneration { get; set; }

        /// <summary>
        /// Supports video understanding.
        /// </summary>
        [JsonPropertyName("video_understanding")]
        public bool VideoUnderstanding { get; set; }

        /// <summary>
        /// Supports function calling.
        /// </summary>
        [JsonPropertyName("function_calling")]
        public bool FunctionCalling { get; set; }

        /// <summary>
        /// Supports tool use.
        /// </summary>
        [JsonPropertyName("tool_use")]
        public bool ToolUse { get; set; }

        /// <summary>
        /// Supports JSON mode.
        /// </summary>
        [JsonPropertyName("json_mode")]
        public bool JsonMode { get; set; }

        /// <summary>
        /// Maximum context length in tokens.
        /// </summary>
        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Maximum output tokens.
        /// </summary>
        [JsonPropertyName("max_output_tokens")]
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Supported image sizes for generation.
        /// </summary>
        [JsonPropertyName("supported_image_sizes")]
        public List<string>? SupportedImageSizes { get; set; }

        /// <summary>
        /// Supported video resolutions.
        /// </summary>
        [JsonPropertyName("supported_video_resolutions")]
        public List<string>? SupportedVideoResolutions { get; set; }

        /// <summary>
        /// Maximum video duration in seconds.
        /// </summary>
        [JsonPropertyName("max_video_duration_seconds")]
        public int? MaxVideoDurationSeconds { get; set; }
    }

    /// <summary>
    /// Specific model capabilities to test.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ModelCapability
    {
        Chat,
        ChatStream,
        Embeddings,
        ImageGeneration,
        Vision,
        VideoGeneration,
        VideoUnderstanding,
        FunctionCalling,
        ToolUse,
        JsonMode
    }

    /// <summary>
    /// Response model for getting all models.
    /// </summary>
    public class ModelsDiscoveryResponse
    {
        /// <summary>
        /// List of discovered models.
        /// </summary>
        [JsonPropertyName("data")]
        public List<DiscoveredModel> Data { get; set; } = new();

        /// <summary>
        /// Total count of models.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// Response model for provider-specific models.
    /// </summary>
    public class ProviderModelsDiscoveryResponse
    {
        /// <summary>
        /// The provider name.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// List of models for this provider.
        /// </summary>
        [JsonPropertyName("data")]
        public List<DiscoveredModel> Data { get; set; } = new();

        /// <summary>
        /// Total count of models.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// Response model for capability testing.
    /// </summary>
    public class CapabilityTestResponse
    {
        /// <summary>
        /// The model that was tested.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// The capability that was tested.
        /// </summary>
        [JsonPropertyName("capability")]
        public string Capability { get; set; } = string.Empty;

        /// <summary>
        /// Whether the model supports the capability.
        /// </summary>
        [JsonPropertyName("supported")]
        public bool Supported { get; set; }
    }

    /// <summary>
    /// Request model for bulk capability testing.
    /// </summary>
    public class BulkCapabilityTestRequest
    {
        /// <summary>
        /// List of capability tests to perform.
        /// </summary>
        [JsonPropertyName("tests")]
        public List<CapabilityTest> Tests { get; set; } = new();
    }

    /// <summary>
    /// Individual capability test within a bulk request.
    /// </summary>
    public class CapabilityTest
    {
        /// <summary>
        /// The model to test.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// The capability to test.
        /// </summary>
        [JsonPropertyName("capability")]
        public string Capability { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for bulk capability testing.
    /// </summary>
    public class BulkCapabilityTestResponse
    {
        /// <summary>
        /// Results of all capability tests.
        /// </summary>
        [JsonPropertyName("results")]
        public List<CapabilityTestResult> Results { get; set; } = new();

        /// <summary>
        /// Total number of tests requested.
        /// </summary>
        [JsonPropertyName("totalTests")]
        public int TotalTests { get; set; }

        /// <summary>
        /// Number of successful tests.
        /// </summary>
        [JsonPropertyName("successfulTests")]
        public int SuccessfulTests { get; set; }

        /// <summary>
        /// Number of failed tests.
        /// </summary>
        [JsonPropertyName("failedTests")]
        public int FailedTests { get; set; }
    }

    /// <summary>
    /// Result of a single capability test.
    /// </summary>
    public class CapabilityTestResult
    {
        /// <summary>
        /// The model that was tested.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// The capability that was tested.
        /// </summary>
        [JsonPropertyName("capability")]
        public string Capability { get; set; } = string.Empty;

        /// <summary>
        /// Whether the model supports the capability.
        /// </summary>
        [JsonPropertyName("supported")]
        public bool Supported { get; set; }

        /// <summary>
        /// Error message if the test failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Request model for bulk model discovery.
    /// </summary>
    public class BulkModelDiscoveryRequest
    {
        /// <summary>
        /// List of model IDs to get discovery information for.
        /// </summary>
        [JsonPropertyName("models")]
        public List<string> Models { get; set; } = new();
    }

    /// <summary>
    /// Response model for bulk model discovery.
    /// </summary>
    public class BulkModelDiscoveryResponse
    {
        /// <summary>
        /// Discovery results for all requested models.
        /// </summary>
        [JsonPropertyName("results")]
        public List<ModelDiscoveryResult> Results { get; set; } = new();

        /// <summary>
        /// Total number of models requested.
        /// </summary>
        [JsonPropertyName("totalRequested")]
        public int TotalRequested { get; set; }

        /// <summary>
        /// Number of models found.
        /// </summary>
        [JsonPropertyName("foundModels")]
        public int FoundModels { get; set; }

        /// <summary>
        /// Number of models not found.
        /// </summary>
        [JsonPropertyName("notFoundModels")]
        public int NotFoundModels { get; set; }
    }

    /// <summary>
    /// Discovery result for a single model.
    /// </summary>
    public class ModelDiscoveryResult
    {
        /// <summary>
        /// The model ID.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// The provider name.
        /// </summary>
        [JsonPropertyName("provider")]
        public string? Provider { get; set; }

        /// <summary>
        /// The display name of the model.
        /// </summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Model capabilities as a dictionary.
        /// </summary>
        [JsonPropertyName("capabilities")]
        public Dictionary<string, bool> Capabilities { get; set; } = new();

        /// <summary>
        /// Whether the model was found.
        /// </summary>
        [JsonPropertyName("found")]
        public bool Found { get; set; }

        /// <summary>
        /// Error message if the model was not found or discovery failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}