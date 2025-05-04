# Provider Client Migration Guide

This guide explains how to migrate existing provider clients to use the new base classes (`BaseLLMClient` and `OpenAICompatibleClient`) to reduce code duplication and improve maintainability.

## Table of Contents

1. [Overview](#overview)
2. [Base Classes](#base-classes)
3. [Migration Steps](#migration-steps)
4. [OpenAI-Compatible Migration Example](#openai-compatible-migration-example)
5. [Custom API Migration Example](#custom-api-migration-example)
6. [Client Selection Guide](#client-selection-guide)
7. [Testing Migrated Clients](#testing-migrated-clients)

## Overview

The new base classes provide common functionality for interacting with LLM providers, reducing code duplication and standardizing error handling, HTTP request handling, and response processing. By using these base classes, we can:

1. Reduce the amount of code in each provider client
2. Ensure consistent error handling and logging
3. Standardize authentication and API interaction
4. Make it easier to add new provider clients

## Base Classes

### BaseLLMClient

The `BaseLLMClient` abstract class provides:

- Implementation of `ILLMClient` interface with abstract methods
- Common HTTP client creation and configuration
- Standardized error handling through `ExceptionHandler`
- Consistent validation patterns

Use this for providers with unique APIs that don't follow the OpenAI-compatible pattern.

### OpenAICompatibleClient

The `OpenAICompatibleClient` class extends `BaseLLMClient` and adds:

- Specific implementation for OpenAI-compatible APIs
- Standard endpoint paths for chat completions, embeddings, etc.
- Common request/response mapping
- Stream handling for server-sent events

Use this for providers that follow the OpenAI API format (most modern LLM providers).

## Migration Steps

### Step 1: Determine the Appropriate Base Class

Decide whether the provider uses an OpenAI-compatible API:

- For OpenAI-compatible APIs (OpenAI, Azure OpenAI, Mistral, Groq, etc.): Use `OpenAICompatibleClient`
- For custom APIs (Anthropic, Cohere, etc.): Use `BaseLLMClient`

### Step 2: Create the New Client Class

1. Create a new class that inherits from the appropriate base class
2. Implement the constructor, passing required parameters to the base class
3. Override necessary methods to handle provider-specific logic

### Step 3: Implement Required Methods

For `OpenAICompatibleClient`, override:
- `GetChatCompletionEndpoint()` if it differs from standard
- `GetModelsEndpoint()` if it differs from standard
- Other endpoint methods as needed
- `ConfigureHttpClient()` for custom headers

For `BaseLLMClient`, implement all abstract methods:
- `CreateChatCompletionAsync()`
- `StreamChatCompletionAsync()`
- `GetModelsAsync()`
- `CreateEmbeddingAsync()`
- `CreateImageAsync()`

### Step 4: Add Provider-Specific Features

Add any provider-specific functionality that isn't covered by the base class:
- Custom error handling
- Special parameter mapping
- Additional API features

## OpenAI-Compatible Migration Example

Here's an example of migrating a client that uses an OpenAI-compatible API:

```csharp
public class GroqClientRevised : OpenAICompatibleClient
{
    private const string DefaultGroqApiBase = "https://api.groq.com/v1";

    public GroqClientRevised(
        ProviderCredentials credentials,
        string providerModelId,
        ILogger<GroqClientRevised> logger,
        IHttpClientFactory? httpClientFactory = null)
        : base(
            EnsureGroqCredentials(credentials),
            providerModelId,
            logger,
            httpClientFactory,
            "groq",
            DetermineBaseUrl(credentials))
    {
    }

    // Helper methods
    private static string DetermineBaseUrl(ProviderCredentials credentials)
    {
        return string.IsNullOrWhiteSpace(credentials.ApiBase)
            ? DefaultGroqApiBase
            : credentials.ApiBase.TrimEnd('/');
    }

    private static ProviderCredentials EnsureGroqCredentials(ProviderCredentials credentials)
    {
        // Validation and default setting logic
        // ...
        return groqCredentials;
    }

    // Override necessary methods for provider-specific behavior
    public override async Task<List<ModelInfo>> GetModelsAsync(
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetModelsAsync(apiKey, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fall back to known models list
            // ...
        }
    }

    // Add provider-specific error handling if needed
    public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.CreateChatCompletionAsync(request, apiKey, cancellationToken);
        }
        catch (LLMCommunicationException ex)
        {
            // Provider-specific error handling
            // ...
        }
    }
}
```

## Custom API Migration Example

Here's an example of migrating a client that uses a custom API format:

```csharp
public class AnthropicClientRevised : BaseLLMClient
{
    private const string DefaultApiBase = "https://api.anthropic.com/v1";
    private const string AnthropicVersion = "2023-06-01";

    public AnthropicClientRevised(
        ProviderCredentials credentials, 
        string providerModelId, 
        ILogger<AnthropicClientRevised> logger,
        IHttpClientFactory? httpClientFactory = null)
        : base(
              credentials, 
              providerModelId, 
              logger, 
              httpClientFactory, 
              "anthropic")
    {
    }

    // Override for Anthropic-specific authentication
    protected override void ConfigureHttpClient(HttpClient client, string apiKey)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        
        // Remove default Authorization header
        client.DefaultRequestHeaders.Authorization = null;
        
        string apiBase = string.IsNullOrWhiteSpace(Credentials.ApiBase) ? DefaultApiBase : Credentials.ApiBase;
        client.BaseAddress = new Uri(apiBase.TrimEnd('/'));
    }

    // Implement all required methods
    public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        // Implementation that uses HttpClientHelper and other utilities
        // ...
    }

    public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Implementation that uses StreamHelper
        // ...
    }

    // Other required methods
    // ...
}
```

## Client Selection Guide

Here's a quick reference for which base class to use for common providers:

| Provider | Base Class | Notes |
|----------|------------|-------|
| OpenAI | OpenAICompatibleClient | Standard OpenAI API format |
| Azure OpenAI | OpenAICompatibleClient | Uses deployment-based URL pattern |
| Mistral | OpenAICompatibleClient | Fully OpenAI-compatible |
| Groq | OpenAICompatibleClient | Fully OpenAI-compatible |
| Anthropic | BaseLLMClient | Uses custom API format with Messages API |
| Cohere | BaseLLMClient | Uses custom API format |
| Gemini (Google) | BaseLLMClient | Uses custom API format |
| Ollama | BaseLLMClient | Similar to OpenAI but with key differences |
| Hugging Face | BaseLLMClient | Uses custom API format, different endpoints |
| Replicate | BaseLLMClient | Uses custom prediction API |

## Testing Migrated Clients

After migrating a client:

1. Run unit tests to verify the client works with existing tests
2. Create a simple test application that uses the client for basic operations
3. Test with streaming and non-streaming requests
4. Verify error handling by triggering common error scenarios
5. Test authentication with both valid and invalid credentials

For providers that diverge significantly from their base class, consider adding more comprehensive tests to cover provider-specific functionality.

## Common Issues and Solutions

During the migration process, you may encounter the following issues:

### Ambiguous References in Tests

When both old and revised client versions exist, tests may have ambiguous references to models:

```csharp
// Error: Ambiguous reference between 'ConduitLLM.Tests.TestHelpers.Mocks.GeminiSafetyRating' 
// and 'ConduitLLM.Providers.InternalModels.GeminiSafetyRating'
var safetyRating = new GeminiSafetyRating(); 
```

**Solution**: Use namespace aliases to clarify references:

```csharp
using ProviderModels = ConduitLLM.Providers.InternalModels;
using TestModels = ConduitLLM.Tests.TestHelpers.Mocks;

// Now they're distinct
var providerRating = new ProviderModels.GeminiSafetyRating();
var testRating = new TestModels.GeminiSafetyRating();
```

### Missing Properties in Test Models

Test model classes may be missing properties that exist in the provider's models:

**Solution**: Add the missing properties to test model classes with appropriate default values:

```csharp
// Add missing property to test mock
public class GeminiCandidate
{
    public GeminiContent Content { get; set; } = new();
    public string FinishReason { get; set; } = "STOP";
    public int Index { get; set; }
    // Add the missing property:
    public List<GeminiSafetyRating> SafetyRatings { get; set; } = new();
}
```

### Reflection for Private Method Access in Tests

Some client tests may need to access private methods that were previously public:

**Solution**: Use reflection to access private methods in tests:

```csharp
// Use reflection to access the private method
var method = typeof(ClientClass).GetMethod("PrivateMethod", 
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

if (method != null)
{
    var result = method.Invoke(clientInstance, parameters);
    // Test the result
}
```

## Final Migration Steps

Once all clients have been migrated and tests are passing:

1. Remove the "Revised" suffix from all client class names
2. Update client registration in dependency injection
3. Delete the original client implementations
4. Update any direct references to client classes in application code
5. Run all tests to verify everything still works