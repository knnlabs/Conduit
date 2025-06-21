namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents a capability test for a model.
/// </summary>
public class CapabilityTest
{
    /// <summary>
    /// Gets or sets the model name to test.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the capability to test (e.g., "Chat", "ImageGeneration", "Vision").
    /// </summary>
    public string Capability { get; set; } = string.Empty;
}

/// <summary>
/// Represents the result of a capability test.
/// </summary>
public class CapabilityTestResult
{
    /// <summary>
    /// Gets or sets the model name that was tested.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the capability that was tested.
    /// </summary>
    public string Capability { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the model supports this capability.
    /// </summary>
    public bool Supported { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the capability.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the error message if the test failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Represents the result of model discovery.
/// </summary>
public class ModelDiscoveryResult
{
    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the model was found.
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// Gets or sets the provider that hosts this model.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Gets or sets the available capabilities for this model.
    /// </summary>
    public IEnumerable<string>? Capabilities { get; set; }

    /// <summary>
    /// Gets or sets the model metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the error message if discovery failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Represents a bulk capability test request.
/// </summary>
public class BulkCapabilityTestRequest
{
    /// <summary>
    /// Gets or sets the array of model-capability pairs to test.
    /// </summary>
    public IEnumerable<CapabilityTest> Tests { get; set; } = new List<CapabilityTest>();

    /// <summary>
    /// Gets or sets the optional virtual key for authentication.
    /// </summary>
    public string? VirtualKey { get; set; }
}

/// <summary>
/// Represents a bulk model discovery request.
/// </summary>
public class BulkModelDiscoveryRequest
{
    /// <summary>
    /// Gets or sets the array of model names to discover.
    /// </summary>
    public IEnumerable<string> Models { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets whether to include capability information in results.
    /// </summary>
    public bool? IncludeCapabilities { get; set; }

    /// <summary>
    /// Gets or sets the filter to only include models with these capabilities.
    /// </summary>
    public IEnumerable<string>? FilterByCapabilities { get; set; }

    /// <summary>
    /// Gets or sets the optional virtual key for authentication.
    /// </summary>
    public string? VirtualKey { get; set; }
}

/// <summary>
/// Represents a model discovery request.
/// </summary>
public class ModelDiscoveryRequest
{
    /// <summary>
    /// Gets or sets the array of model names to discover.
    /// </summary>
    public IEnumerable<string> Models { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the required capabilities that discovered models must have.
    /// </summary>
    public IEnumerable<string>? RequiredCapabilities { get; set; }

    /// <summary>
    /// Gets or sets the optional virtual key for authentication.
    /// </summary>
    public string? VirtualKey { get; set; }
}

/// <summary>
/// Represents the response from bulk capability testing.
/// </summary>
public class BulkCapabilityTestResponse
{
    /// <summary>
    /// Gets or sets the array of capability test results.
    /// </summary>
    public IEnumerable<CapabilityTestResult> Results { get; set; } = new List<CapabilityTestResult>();

    /// <summary>
    /// Gets or sets the total number of tests performed.
    /// </summary>
    public int TotalTests { get; set; }

    /// <summary>
    /// Gets or sets the number of successful tests.
    /// </summary>
    public int SuccessfulTests { get; set; }

    /// <summary>
    /// Gets or sets the number of failed tests.
    /// </summary>
    public int FailedTests { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the tests were performed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public int ExecutionTimeMs { get; set; }
}

/// <summary>
/// Represents the response from bulk model discovery.
/// </summary>
public class BulkModelDiscoveryResponse
{
    /// <summary>
    /// Gets or sets the array of model discovery results.
    /// </summary>
    public IEnumerable<ModelDiscoveryResult> Results { get; set; } = new List<ModelDiscoveryResult>();

    /// <summary>
    /// Gets or sets the total number of models requested.
    /// </summary>
    public int TotalRequested { get; set; }

    /// <summary>
    /// Gets or sets the number of models found.
    /// </summary>
    public int FoundModels { get; set; }

    /// <summary>
    /// Gets or sets the number of models not found.
    /// </summary>
    public int NotFoundModels { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when discovery was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public int ExecutionTimeMs { get; set; }
}

/// <summary>
/// Represents the response from capability testing.
/// </summary>
public class CapabilityTestResponse
{
    /// <summary>
    /// Gets or sets the model name that was tested.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the capability that was tested.
    /// </summary>
    public string Capability { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the model supports this capability.
    /// </summary>
    public bool Supported { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the capability.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the test was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Represents a discovered model.
/// </summary>
public class DiscoveryModel
{
    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider that hosts this model.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the available capabilities.
    /// </summary>
    public IEnumerable<string> Capabilities { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the model metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets whether the model is currently available.
    /// </summary>
    public bool Available { get; set; }
}

/// <summary>
/// Represents the response from discovering all models.
/// </summary>
public class DiscoveryModelsResponse
{
    /// <summary>
    /// Gets or sets the array of all discovered models.
    /// </summary>
    public IEnumerable<DiscoveryModel> Models { get; set; } = new List<DiscoveryModel>();

    /// <summary>
    /// Gets or sets the total number of models.
    /// </summary>
    public int TotalModels { get; set; }

    /// <summary>
    /// Gets or sets the number of available models.
    /// </summary>
    public int AvailableModels { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when discovery was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Represents the response from discovering models for a specific provider.
/// </summary>
public class DiscoveryProviderModelsResponse
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the array of models from this provider.
    /// </summary>
    public IEnumerable<DiscoveryModel> Models { get; set; } = new List<DiscoveryModel>();

    /// <summary>
    /// Gets or sets the total number of models for this provider.
    /// </summary>
    public int TotalModels { get; set; }

    /// <summary>
    /// Gets or sets the number of available models for this provider.
    /// </summary>
    public int AvailableModels { get; set; }

    /// <summary>
    /// Gets or sets whether the provider is currently available.
    /// </summary>
    public bool ProviderAvailable { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when discovery was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Represents common capability types for discovery.
/// </summary>
public static class DiscoveryCapabilities
{
    /// <summary>
    /// Chat completion capability.
    /// </summary>
    public const string Chat = "Chat";

    /// <summary>
    /// Image generation capability.
    /// </summary>
    public const string ImageGeneration = "ImageGeneration";

    /// <summary>
    /// Vision/image understanding capability.
    /// </summary>
    public const string Vision = "Vision";

    /// <summary>
    /// Audio transcription capability.
    /// </summary>
    public const string AudioTranscription = "AudioTranscription";

    /// <summary>
    /// Text-to-speech capability.
    /// </summary>
    public const string TextToSpeech = "TextToSpeech";

    /// <summary>
    /// Text embeddings capability.
    /// </summary>
    public const string Embeddings = "Embeddings";

    /// <summary>
    /// Code generation capability.
    /// </summary>
    public const string CodeGeneration = "CodeGeneration";

    /// <summary>
    /// Function calling capability.
    /// </summary>
    public const string FunctionCalling = "FunctionCalling";

    /// <summary>
    /// Streaming response capability.
    /// </summary>
    public const string Streaming = "Streaming";
}

/// <summary>
/// Represents statistics about the discovery system.
/// </summary>
public class DiscoveryStats
{
    /// <summary>
    /// Gets or sets the total number of models available.
    /// </summary>
    public int TotalModels { get; set; }

    /// <summary>
    /// Gets or sets the number of providers available.
    /// </summary>
    public int TotalProviders { get; set; }

    /// <summary>
    /// Gets or sets the models grouped by capability.
    /// </summary>
    public Dictionary<string, IEnumerable<string>> ModelsByCapability { get; set; } = new();

    /// <summary>
    /// Gets or sets the providers grouped by capability.
    /// </summary>
    public Dictionary<string, IEnumerable<string>> ProvidersByCapability { get; set; } = new();

    /// <summary>
    /// Gets or sets the cache statistics.
    /// </summary>
    public CacheStats? CacheStats { get; set; }
}

/// <summary>
/// Represents cache statistics.
/// </summary>
public class CacheStats
{
    /// <summary>
    /// Gets or sets the cache hit rate as a percentage.
    /// </summary>
    public double HitRate { get; set; }

    /// <summary>
    /// Gets or sets the total number of cache requests.
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the current cache size.
    /// </summary>
    public int CacheSize { get; set; }
}

/// <summary>
/// Represents a discovery error.
/// </summary>
public class DiscoveryError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}