# Test Migration Guide for Provider Client Classes

## Overview

The provider client classes have been migrated from their original implementation to a new architecture based on `BaseLLMClient` and `OpenAICompatibleClient` base classes. While the core application code has been successfully migrated, the test files require additional updates to work with the new architecture.

## Changes in Client Architecture

1. **Class Naming**: All "Revised" suffixes have been removed from class names.
2. **Parameter Changes**: Clients now consistently take an `IHttpClientFactory` instead of directly using `HttpClient` instances.
3. **Base Classes**: Clients now inherit from either `BaseLLMClient` or `OpenAICompatibleClient`.
4. **Internal Models**: Many internal model structures have been consolidated or moved to namespaces.

## Test Migration Steps

To update test files to work with the new provider client architecture, follow these steps:

### 1. Use HttpClientFactoryAdapter

For tests directly passing an HttpClient instance, use the new adapter:

```csharp
// Before:
var client = new SomeClient(_credentials, _modelId, _logger.Object, _httpClient);

// After:
var httpClientFactory = HttpClientFactoryAdapter.AdaptHttpClient(_httpClient);
var client = new SomeClient(_credentials, _modelId, _logger.Object, httpClientFactory);
```

### 2. Update Model References

The test models have been moved to `ConduitLLM.Tests.TestHelpers` namespace:

```csharp
// Add this using directive
using ConduitLLM.Tests.TestHelpers;
```

### 3. Update Logger Type Parameters

Make sure all logger mocks use the updated class name:

```csharp
// Before:
private readonly Mock<ILogger<SomeClientRevised>> _loggerMock;

// After:
private readonly Mock<ILogger<SomeClient>> _loggerMock;
```

### 4. Update Custom Helper Methods

If you have helper methods that create response objects for various providers, ensure they're updated to use the test model classes.

### 5. Fix Nullability Warnings

Address any nullability warnings by using appropriate null checking or null-forgiving operators as needed.

## Using Test Models

The repository includes TestModels.cs with common test model classes:

- BedrockClaudeChatResponse
- HuggingFaceTextGenerationResponse
- SageMakerChatResponse

Use these classes in your test setup and assertions instead of trying to reference the provider-specific internal models.

## Running Individual Tests

While the test migration is in progress, you can run individual tests or test classes:

```bash
# Run a specific test class
dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.OpenAIClientTests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.OpenAIClientTests.TestCreateChatCompletion"
```

## Prioritizing Test Migration

Focus on migrating tests in this order:

1. Core provider tests (OpenAI, Azure, Anthropic)
2. Popular provider tests (Mistral, Groq, Cohere)
3. Specialized provider tests (Bedrock, SageMaker, HuggingFace)
4. Integration and controller tests

## Need Assistance?

If you encounter issues with test migration, please:

1. Check the HttpClientFactoryAdapter for handling IHttpClientFactory requirements
2. Refer to TestModels.cs for model structure references
3. Look at successfully migrated tests for examples