# ConduitLLM.CoreClient

Official .NET client library for the Conduit Core API (OpenAI-compatible).

## Features

- Complete OpenAI-compatible API coverage
- Chat completions with streaming support
- Image generation, editing, and variations
- Model management and discovery
- Type-safe C# models with comprehensive validation
- Built-in retry policies with exponential backoff
- Async/await throughout
- Comprehensive error handling

## Services Included

- **ChatService** - Chat completions with streaming and function calling
- **ImagesService** - Image generation, editing, and variations
- **ModelsService** - Model listing and information retrieval

## Installation

```bash
dotnet add package ConduitLLM.CoreClient
```

## Usage

```csharp
using ConduitLLM.CoreClient;

// Create client from API key
var client = ConduitCoreClient.FromApiKey("your-api-key");

// Or create from environment variables (CONDUIT_API_KEY, CONDUIT_BASE_URL)
var client = ConduitCoreClient.FromEnvironment();

// Chat completion
var response = await client.Chat.CreateCompletionAsync(new ChatCompletionRequest
{
    Model = "gpt-4",
    Messages = new[]
    {
        new ChatCompletionMessage { Role = "user", Content = "Hello!" }
    }
});

// Streaming chat completion
await foreach (var chunk in client.Chat.CreateCompletionStreamAsync(new ChatCompletionRequest
{
    Model = "gpt-4",
    Messages = new[] { new ChatCompletionMessage { Role = "user", Content = "Hello!" } },
    Stream = true
}))
{
    Console.Write(chunk.Choices.FirstOrDefault()?.Delta.Content);
}

// Image generation
var imageResponse = await client.Images.GenerateAsync(new ImageGenerationRequest
{
    Prompt = "A beautiful sunset over mountains",
    Model = "dall-e-3",
    Size = ImageSize.Size1024x1024,
    Quality = ImageQuality.HD
});

// List available models
var models = await client.Models.ListAsync();
```

## Extension Methods

The library includes convenient extension methods for common operations:

```csharp
// Simple chat
string response = await client.ChatAsync("gpt-4", "Hello, world!");

// Simple image generation
string? imageUrl = await client.GenerateImageAsync("A sunset over mountains");

// Check model availability
bool isAvailable = await client.IsModelAvailableAsync("gpt-4");
```

## Configuration

The client can be configured with custom settings:

```csharp
var config = new ConduitCoreClientConfiguration
{
    ApiKey = "your-api-key",
    BaseUrl = "https://your-instance.com",
    TimeoutSeconds = 120,
    MaxRetries = 3,
    RetryDelayMs = 1000
};

var client = new ConduitCoreClient(config);
```

## Error Handling

The library provides comprehensive error handling with custom exception types:

- `ConduitCoreException` - Base exception for all API errors
- `ValidationException` - Invalid request parameters
- `UnauthorizedException` - Authentication failures
- `NotFoundException` - Resource not found
- `RateLimitException` - Rate limit exceeded
- `StreamException` - Streaming operation failures

## License

MIT License