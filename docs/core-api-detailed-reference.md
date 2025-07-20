# Core API Detailed Reference

This document provides comprehensive documentation for the Conduit Core API, including detailed examples, best practices, and implementation notes.

## Table of Contents

1. [Overview](#overview)
2. [Authentication](#authentication)
3. [Chat Completions](#chat-completions)
4. [Streaming Responses](#streaming-responses)
5. [Multimodal Support](#multimodal-support)
6. [Function Calling](#function-calling)
7. [Models Endpoint](#models-endpoint)
8. [Error Handling](#error-handling)
9. [Rate Limiting](#rate-limiting)
10. [Performance Considerations](#performance-considerations)
11. [Client Libraries](#client-libraries)
12. [Migration Guide](#migration-guide)

## Overview

The Conduit Core API provides an OpenAI-compatible interface for interacting with multiple LLM providers through a unified gateway. The API is designed to be drop-in compatible with existing OpenAI client libraries and code.

### Base URLs

- **Development**: `http://localhost:5002/v1`
- **Production**: Configure based on your deployment

### OpenAPI Specification

OpenAPI specifications are available in both YAML and JSON formats:
- `/ConduitLLM.Http/openapi.yaml`
- `/ConduitLLM.Http/openapi.json`

## Authentication

All API requests require authentication using Virtual Keys. Virtual Keys are managed through the Admin API or WebUI.

### Header Format

```http
Authorization: Bearer condt_yourvirtualkey
```

### Example

```bash
curl -X POST http://localhost:5002/v1/chat/completions \
  -H "Authorization: Bearer condt_sk_1234567890" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-4",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
```

## Chat Completions

The `/v1/chat/completions` endpoint is the primary interface for generating text responses.

### Basic Request

```json
POST /v1/chat/completions
{
  "model": "gpt-4",
  "messages": [
    {
      "role": "system",
      "content": "You are a helpful assistant."
    },
    {
      "role": "user",
      "content": "What is the capital of France?"
    }
  ]
}
```

### Advanced Parameters

```json
{
  "model": "gpt-4",
  "messages": [...],
  "temperature": 0.7,
  "max_tokens": 150,
  "top_p": 0.9,
  "frequency_penalty": 0.5,
  "presence_penalty": 0.5,
  "stop": ["END", "STOP"],
  "n": 1,
  "user": "user123",
  "seed": 42
}
```

### Response Format

```json
{
  "id": "chatcmpl-123456",
  "object": "chat.completion",
  "created": 1694365200,
  "model": "gpt-4",
  "system_fingerprint": "fp_12345",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "The capital of France is Paris."
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 20,
    "completion_tokens": 10,
    "total_tokens": 30
  }
}
```

### Python Example

```python
import openai

# Configure the client to use Conduit
openai.api_base = "http://localhost:5002/v1"
openai.api_key = "condt_sk_1234567890"

response = openai.ChatCompletion.create(
    model="gpt-4",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": "What is the capital of France?"}
    ],
    temperature=0.7
)

print(response.choices[0].message.content)
```

### Node.js Example

```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'condt_sk_1234567890',
  baseURL: 'http://localhost:5002/v1'
});

const completion = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [
    { role: 'system', content: 'You are a helpful assistant.' },
    { role: 'user', content: 'What is the capital of France?' }
  ],
  temperature: 0.7
});

console.log(completion.choices[0].message.content);
```

## Streaming Responses

Enable streaming by setting `stream: true` in your request. Responses are sent as Server-Sent Events (SSE).

### Streaming Request

```json
{
  "model": "gpt-4",
  "messages": [...],
  "stream": true
}
```

### Streaming Response Format

```
data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1694365200,"model":"gpt-4","choices":[{"index":0,"delta":{"role":"assistant"},"finish_reason":null}]}

data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1694365200,"model":"gpt-4","choices":[{"index":0,"delta":{"content":"The"},"finish_reason":null}]}

data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1694365200,"model":"gpt-4","choices":[{"index":0,"delta":{"content":" capital"},"finish_reason":null}]}

data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1694365200,"model":"gpt-4","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

data: [DONE]
```

### Python Streaming Example

```python
import openai

openai.api_base = "http://localhost:5002/v1"
openai.api_key = "condt_sk_1234567890"

stream = openai.ChatCompletion.create(
    model="gpt-4",
    messages=[{"role": "user", "content": "Count to 5"}],
    stream=True
)

for chunk in stream:
    if chunk.choices[0].delta.content is not None:
        print(chunk.choices[0].delta.content, end='')
```

### JavaScript Streaming Example

```javascript
const stream = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'Count to 5' }],
  stream: true
});

for await (const chunk of stream) {
  process.stdout.write(chunk.choices[0]?.delta?.content || '');
}
```

## Multimodal Support

Conduit supports multimodal inputs for vision-capable models.

### Image Input Format

```json
{
  "model": "gpt-4-vision",
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "text",
          "text": "What's in this image?"
        },
        {
          "type": "image_url",
          "image_url": {
            "url": "https://example.com/image.jpg",
            "detail": "high"
          }
        }
      ]
    }
  ]
}
```

### Multiple Images

```json
{
  "model": "gpt-4-vision",
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "text",
          "text": "What are the differences between these images?"
        },
        {
          "type": "image_url",
          "image_url": {
            "url": "https://example.com/image1.jpg"
          }
        },
        {
          "type": "image_url",
          "image_url": {
            "url": "https://example.com/image2.jpg"
          }
        }
      ]
    }
  ]
}
```

### Base64 Images

```json
{
  "model": "gpt-4-vision",
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "text",
          "text": "Describe this image"
        },
        {
          "type": "image_url",
          "image_url": {
            "url": "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQ..."
          }
        }
      ]
    }
  ]
}
```

## Function Calling

Conduit supports OpenAI's function calling feature for structured interactions.

### Defining Functions

```json
{
  "model": "gpt-4",
  "messages": [
    {
      "role": "user",
      "content": "What's the weather in San Francisco?"
    }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_current_weather",
        "description": "Get the current weather in a given location",
        "parameters": {
          "type": "object",
          "properties": {
            "location": {
              "type": "string",
              "description": "The city and state, e.g. San Francisco, CA"
            },
            "unit": {
              "type": "string",
              "enum": ["celsius", "fahrenheit"]
            }
          },
          "required": ["location"]
        }
      }
    }
  ],
  "tool_choice": "auto"
}
```

### Function Call Response

```json
{
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": null,
        "tool_calls": [
          {
            "id": "call_123",
            "type": "function",
            "function": {
              "name": "get_current_weather",
              "arguments": "{\"location\":\"San Francisco, CA\",\"unit\":\"fahrenheit\"}"
            }
          }
        ]
      },
      "finish_reason": "tool_calls"
    }
  ]
}
```

### Complete Function Calling Flow

```python
import json
import openai

def get_current_weather(location, unit="fahrenheit"):
    # Mock weather function
    return f"Weather in {location}: 72°{unit[0].upper()}"

# Initial request with function definition
response = openai.ChatCompletion.create(
    model="gpt-4",
    messages=[
        {"role": "user", "content": "What's the weather in San Francisco?"}
    ],
    tools=[{
        "type": "function",
        "function": {
            "name": "get_current_weather",
            "description": "Get the current weather",
            "parameters": {
                "type": "object",
                "properties": {
                    "location": {"type": "string"},
                    "unit": {"type": "string", "enum": ["celsius", "fahrenheit"]}
                },
                "required": ["location"]
            }
        }
    }]
)

# Check if the model wants to call a function
if response.choices[0].finish_reason == "tool_calls":
    tool_call = response.choices[0].message.tool_calls[0]
    
    # Parse function arguments
    args = json.loads(tool_call.function.arguments)
    
    # Call the function
    result = get_current_weather(**args)
    
    # Send the result back to the model
    follow_up = openai.ChatCompletion.create(
        model="gpt-4",
        messages=[
            {"role": "user", "content": "What's the weather in San Francisco?"},
            response.choices[0].message,
            {
                "role": "tool",
                "tool_call_id": tool_call.id,
                "content": result
            }
        ]
    )
    
    print(follow_up.choices[0].message.content)
```

## Models Endpoint

List available models configured in your Conduit instance.

### Request

```http
GET /v1/models
Authorization: Bearer condt_sk_1234567890
```

### Response

```json
{
  "object": "list",
  "data": [
    {
      "id": "gpt-4",
      "object": "model",
      "created": 1677610602,
      "owned_by": "conduitllm"
    },
    {
      "id": "claude-3-opus",
      "object": "model",
      "created": 1677610602,
      "owned_by": "conduitllm"
    },
    {
      "id": "gemini-pro",
      "object": "model",
      "created": 1677610602,
      "owned_by": "conduitllm"
    }
  ]
}
```

## Error Handling

Conduit returns errors in OpenAI's standard format.

### Error Response Format

```json
{
  "error": {
    "message": "Invalid API key provided",
    "type": "authentication_error",
    "code": "invalid_api_key"
  }
}
```

### Common Error Types

| HTTP Status | Error Type | Description |
|-------------|------------|-------------|
| 400 | `invalid_request_error` | Invalid request format |
| 401 | `authentication_error` | Invalid or missing API key |
| 403 | `permission_error` | Insufficient permissions |
| 404 | `not_found_error` | Resource not found |
| 429 | `rate_limit_error` | Rate limit exceeded |
| 500 | `server_error` | Internal server error |
| 503 | `service_unavailable` | Service temporarily unavailable |

### Error Handling Example

```python
try:
    response = openai.ChatCompletion.create(
        model="gpt-4",
        messages=[{"role": "user", "content": "Hello"}]
    )
except openai.error.RateLimitError as e:
    print(f"Rate limit exceeded: {e}")
    # Implement exponential backoff
except openai.error.AuthenticationError as e:
    print(f"Authentication failed: {e}")
    # Check API key
except openai.error.APIError as e:
    print(f"API error: {e}")
    # Handle general API errors
```

## Rate Limiting

Conduit implements rate limiting per Virtual Key.

### Rate Limit Headers

Response headers include rate limit information:

```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 1694368800
```

### Handling Rate Limits

```python
import time
import openai

def make_request_with_retry(messages, max_retries=3):
    for attempt in range(max_retries):
        try:
            return openai.ChatCompletion.create(
                model="gpt-4",
                messages=messages
            )
        except openai.error.RateLimitError as e:
            if attempt == max_retries - 1:
                raise
            
            # Extract retry-after from error response
            retry_after = int(e.response.headers.get('Retry-After', 60))
            print(f"Rate limited. Waiting {retry_after} seconds...")
            time.sleep(retry_after)
```

## Performance Considerations

### Connection Pooling

Reuse HTTP connections for better performance:

```python
import httpx
import openai

# Configure connection pooling
openai.requestssession = httpx.Client(
    limits=httpx.Limits(max_connections=100)
)
```

### Timeout Configuration

Set appropriate timeouts for your use case:

```python
response = openai.ChatCompletion.create(
    model="gpt-4",
    messages=[{"role": "user", "content": "Hello"}],
    timeout=30  # 30 seconds timeout
)
```

### Streaming for Long Responses

Use streaming for better perceived performance with long responses:

```javascript
// Stream tokens as they arrive
const stream = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: messages,
  stream: true,
  max_tokens: 1000
});

for await (const chunk of stream) {
  // Process each chunk immediately
  updateUI(chunk.choices[0]?.delta?.content || '');
}
```

## Client Libraries

Conduit is compatible with official OpenAI client libraries:

### Python

```bash
pip install openai
```

```python
import openai
openai.api_base = "http://localhost:5002/v1"
openai.api_key = "condt_sk_1234567890"
```

### Node.js/TypeScript

```bash
npm install openai
```

```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'condt_sk_1234567890',
  baseURL: 'http://localhost:5002/v1'
});
```

### C#/.NET

```bash
dotnet add package OpenAI
```

```csharp
using OpenAI_API;

var api = new OpenAIAPI("condt_sk_1234567890");
api.ApiUrlFormat = "http://localhost:5002/v1/{0}";
```

### Go

```bash
go get github.com/sashabaranov/go-openai
```

```go
import "github.com/sashabaranov/go-openai"

config := openai.DefaultConfig("condt_sk_1234567890")
config.BaseURL = "http://localhost:5002/v1"
client := openai.NewClientWithConfig(config)
```

### cURL

```bash
curl -X POST http://localhost:5002/v1/chat/completions \
  -H "Authorization: Bearer condt_sk_1234567890" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-4",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
```

## Migration Guide

### From OpenAI to Conduit

1. **Update Base URL**: Change the API base URL to your Conduit instance
2. **Update API Key**: Replace OpenAI API key with Conduit Virtual Key
3. **Model Names**: Update model names if using custom aliases

```python
# Before (OpenAI)
import openai
openai.api_key = "sk-..."
response = openai.ChatCompletion.create(model="gpt-4", ...)

# After (Conduit)
import openai
openai.api_base = "http://localhost:5002/v1"
openai.api_key = "condt_sk_..."
response = openai.ChatCompletion.create(model="gpt-4", ...)
```

### Model Mapping

Conduit allows custom model aliases. Map your model names in the Admin UI:

| Alias | Provider Model |
|-------|----------------|
| `gpt-4` | `openai/gpt-4` |
| `claude` | `anthropic/claude-3-opus-20240229` |
| `fast-model` | `groq/llama3-70b-8192` |

### Feature Compatibility

| Feature | OpenAI | Conduit |
|---------|--------|---------|
| Chat Completions | ✅ | ✅ |
| Streaming | ✅ | ✅ |
| Function Calling | ✅ | ✅ |
| Vision/Multimodal | ✅ | ✅ |
| Embeddings | ✅ | 🚧 (Coming Soon) |
| Fine-tuning | ✅ | ❌ |
| Assistants API | ✅ | ❌ |

## Best Practices

1. **Use Environment Variables**: Store API keys in environment variables
2. **Implement Retry Logic**: Handle transient errors gracefully
3. **Monitor Usage**: Track token usage and costs via the Admin UI
4. **Set Timeouts**: Configure appropriate timeouts for your use case
5. **Use Streaming**: For better UX with long responses
6. **Cache Responses**: When appropriate for your use case
7. **Validate Inputs**: Check message format and parameters before sending

## Support

- **Documentation**: `/docs/API-Reference.md`
- **OpenAPI Spec**: `/ConduitLLM.Http/openapi.yaml`
- **Admin UI**: `http://localhost:5000`
- **Health Check**: `GET /health/db`

For issues or questions, please refer to the project repository.