# Provider Integration

## Overview

ConduitLLM supports integration with multiple LLM providers through a unified interface. This document explains how to configure and use different providers, set up model mappings, and leverage the abstraction layer to build provider-agnostic applications.

## Supported Providers

ConduitLLM currently supports the following LLM providers:

| Provider   | Description                         | Vision Support | Documentation | API Key URL |
|------------|-------------------------------------|---------------|---------------|-------------|
| OpenAI     | Provider of GPT models              | Yes           | [OpenAI API Docs](https://platform.openai.com/docs/api-reference) | [Get API Key](https://platform.openai.com/api-keys) |
| Anthropic  | Provider of Claude models           | Planned       | [Anthropic API Docs](https://docs.anthropic.com/claude/reference) | [Get API Key](https://console.anthropic.com/keys) |
| Cohere     | Provider of Command models          | No            | [Cohere API Docs](https://docs.cohere.com/reference/about) | [Get API Key](https://dashboard.cohere.com/api-keys) |
| Gemini     | Google's generative AI              | Partial       | [Gemini API Docs](https://ai.google.dev/docs) | [Get API Key](https://makersuite.google.com/app/apikey) |
| Fireworks  | Various fine-tuned models           | No            | [Fireworks API Docs](https://docs.fireworks.ai/api) | [Get API Key](https://app.fireworks.ai/users/settings/api-keys) |
| OpenRouter | Meta-provider (routes to many)      | Dependent     | [OpenRouter API Docs](https://openrouter.ai/docs) | [Get API Key](https://openrouter.ai/keys) |

- Only providers with "Yes" or "Partial" vision support will process image content. Others will ignore images and process only text parts of a message.
- If a vision request is routed to a provider without vision support, ConduitLLM will fallback to text-only handling or return an error, depending on your router configuration.
- For OpenRouter, vision/model support depends on the selected backend provider.

> **Router and Budget Awareness:**
> - Routing and budget management are vision-aware and cost-aware. Vision model usage and pricing are tracked and can influence routing and fallback decisions.

For provider-specific options, message formats, and endpoint details, see the [API Reference](./API-Reference.md).

## Provider Configuration

### Adding a Provider

Providers can be added through the WebUI or API:

#### Via WebUI

1. Navigate to the **Configuration** page
2. Select the **Providers** tab
3. Click **Add Provider**
4. Select the provider type
5. Enter the required information:
   - Name (for identification)
   - API Key
   - Endpoint URL (optional, uses default if blank)
6. Click **Save**

#### Via API

```
POST /api/providers
```

```json
{
  "name": "OpenAI",
  "apiKey": "sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "endpoint": "https://api.openai.com/v1"
}
```

### Provider-Specific Configuration

Each provider may have unique configuration requirements:

#### OpenAI

- API Key format: `sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://api.openai.com/v1`
- Optional organization ID for enterprise accounts

#### Anthropic

- API Key format: `sk-ant-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://api.anthropic.com`
- Version header is automatically handled

#### Cohere

- API Key format: `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://api.cohere.ai`

#### Gemini

- API Key format: `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://generativelanguage.googleapis.com`

#### Fireworks

- API Key format: `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://api.fireworks.ai/inference`

#### OpenRouter

- API Key format: `sk-or-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- Default endpoint: `https://openrouter.ai/api`
- Optional referer and site information

## Model Mappings

### Concept

Model mappings create an abstraction layer between generic model names and provider-specific implementations. This allows:

1. **Provider Agnosticism**: Applications can request capabilities rather than specific models
2. **Seamless Switching**: Change providers without modifying client applications
3. **Fallback Support**: Configure alternative models when primary choices are unavailable

### Creating Model Mappings

#### Via WebUI

1. Navigate to the **Configuration** page
2. Select the **Model Mappings** tab
3. Click **Add Mapping**
4. Configure the mapping:
   - Generic Model Name (e.g., "gpt-4-equivalent")
   - Provider (select from configured providers)
   - Provider-Specific Model (e.g., "gpt-4" for OpenAI)
5. Click **Save**

#### Via API

```
POST /api/model-mappings
```

```json
{
  "genericModel": "gpt-4-equivalent",
  "provider": "openai-provider-id",
  "providerModel": "gpt-4",
  "isActive": true
}
```

### Example Mappings

The following table shows example mappings across providers:

| Generic Model | OpenAI | Anthropic | Cohere | Gemini |
|---------------|--------|-----------|--------|--------|
| chat-large | gpt-4 | claude-2 | command-r-plus | gemini-pro |
| chat-medium | gpt-3.5-turbo | claude-instant | command | gemini-flash |
| embedding | text-embedding-3-large | N/A | embed-english-v3.0 | embedding-001 |

### Using Mapped Models

In API requests, use the generic model name instead of provider-specific names:

```json
{
  "model": "chat-large",
  "messages": [
    {"role": "system", "content": "You are a helpful assistant."},
    {"role": "user", "content": "Hello!"}
  ],
  "max_tokens": 1024
}
```

The system will automatically route this request to the appropriate provider-specific model.

## Model Capabilities

Different providers support different features. ConduitLLM normalizes these where possible:

### Common Capabilities

All supported providers offer:

- **Text Completion**: Generate text from a prompt
- **Chat Completion**: Generate responses in a conversational format
- **Temperature Control**: Adjust randomness of outputs

### Provider-Specific Capabilities

Some capabilities are only available with certain providers:

| Capability | OpenAI | Anthropic | Cohere | Gemini | Fireworks | OpenRouter |
|------------|--------|-----------|--------|--------|-----------|------------|
| Chat | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Embeddings | ✓ | ✗ | ✓ | ✓ | ✓ | ✓ |
| Function Calling | ✓ | ✓ | ✓ | ✓ | Varies | Varies |
| JSON Mode | ✓ | ✓ | ✗ | ✓ | Varies | Varies |
| Vision | ✓ | ✓ | ✗ | ✓ | Varies | Varies |

## Integration With Router

Provider integrations work seamlessly with the router:

1. Configure multiple providers
2. Create model mappings for each provider
3. Set up model deployments in the router
4. Configure fallback paths

Example router configuration using multiple providers:

```json
{
  "strategy": "round-robin",
  "deployments": [
    {
      "model": "chat-large",
      "provider": "openai-provider-id",
      "weight": 1.0,
      "isActive": true
    },
    {
      "model": "chat-large",
      "provider": "anthropic-provider-id",
      "weight": 1.0,
      "isActive": true
    }
  ]
}
```

## Request Transformation

ConduitLLM handles the transformation of requests between the unified API format and provider-specific formats:

### Input Transformation

1. Client sends request using generic model and unified format
2. System identifies the target provider
3. Request is transformed to match provider-specific API
4. Transformed request is sent to the provider

### Output Transformation

1. Provider returns response in its native format
2. System transforms the response to unified format
3. Transformed response is returned to the client

## Provider-Specific Links

The WebUI provides helpful links for each provider:

- API documentation
- API key generation
- Pricing information
- Model capabilities

## Implementation Examples

### Basic LLM Request

```csharp
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

// Inject ILlmService
public class MyService
{
    private readonly ILlmService _llmService;

    public MyService(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<string> GetCompletionAsync(string prompt)
    {
        var request = new ChatCompletionRequest
        {
            Model = "chat-medium", // Generic model name
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = prompt }
            },
            MaxTokens = 1000,
            Temperature = 0.7
        };

        var response = await _llmService.CreateChatCompletionAsync(request);
        return response.Choices[0].Message.Content;
    }
}
```

### Provider-Specific Request

If you need to use provider-specific features:

```csharp
var request = new ChatCompletionRequest
{
    Model = "openai:gpt-4", // Direct provider and model specification
    Messages = new List<ChatMessage>
    {
        new ChatMessage { Role = "user", Content = prompt }
    },
    ProviderOptions = new Dictionary<string, object>
    {
        ["functions"] = new[]
        {
            new
            {
                name = "get_weather",
                description = "Get the current weather",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        location = new
                        {
                            type = "string",
                            description = "The city and state, e.g. San Francisco, CA"
                        }
                    },
                    required = new[] { "location" }
                }
            }
        }
    }
};
```

## Troubleshooting

### Common Issues

#### Rate Limiting

Each provider has different rate limits. If you encounter rate limiting:

1. Implement retry logic with exponential backoff
2. Distribute requests across multiple providers
3. Contact the provider to request limit increases

#### Authentication Errors

If you experience authentication errors:

1. Verify the API key is correct and active
2. Check that the endpoint URL is correct
3. Ensure the provider account is in good standing
4. Check for required headers or authentication methods

#### Model Availability

If a model is unavailable:

1. Verify the model exists with the provider
2. Check that your account has access to the model
3. Configure appropriate fallbacks in the router

## Best Practices

1. **Use Generic Models**: Work with generic model names to maintain flexibility
2. **Configure Multiple Providers**: Set up alternatives for critical capabilities
3. **Monitor Usage**: Watch for costs and rate limits across providers
4. **Review Mappings**: Periodically review model mappings for new options
5. **Test Regularly**: Verify all providers are functioning correctly
