---
sidebar_position: 1
title: Core APIs Overview
description: Comprehensive guide to Conduit's Core APIs for developers integrating LLM capabilities
---

# Core APIs Overview

The Conduit Core APIs provide a unified, OpenAI-compatible interface for accessing multiple LLM providers. This guide covers all available endpoints, authentication, request/response formats, and integration patterns for developers.

## API Architecture

### OpenAI Compatibility

Conduit implements OpenAI's API specification, allowing you to use existing OpenAI client libraries and code with minimal changes:

```javascript
// Standard OpenAI client works with Conduit
const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'https://api.conduit.yourdomain.com/v1'
});
```

### Unified Provider Access

Behind the scenes, Conduit routes requests to appropriate providers:

```
Client Request → Virtual Key Auth → Model Routing → Provider Selection → LLM Provider
                                                                      ↓
Response ←  Response Processing ←  Provider Response ←  Provider API
```

## Base URLs and Endpoints

### Environment URLs

**Development:**
```
Core API: http://localhost:5000
Base URL: http://localhost:5000/v1
```

**Production:**
```
Core API: https://api.conduit.yourdomain.com
Base URL: https://api.conduit.yourdomain.com/v1
```

### Available Endpoints

| Category | Endpoint | Description |
|----------|----------|-------------|
| **Chat** | `/v1/chat/completions` | Text generation and conversation |
| **Completions** | `/v1/completions` | Legacy text completion (returns 501 not implemented) |
| **Embeddings** | `/v1/embeddings` | Vector embeddings generation |
| **Models** | `/v1/models` | Available models listing |
| **Audio** | `/v1/audio/transcriptions` | Speech-to-text transcription |
| **Audio** | `/v1/audio/speech` | Text-to-speech synthesis |
| **Images** | `/v1/images/generations` | Basic image generation |
| **Real-Time** | `/v1/realtime/connect` | WebSocket audio proxy |

## Authentication

### Virtual Key Authentication

All Core API requests require virtual key authentication:

```http
Authorization: Bearer condt_your_virtual_key_here
```

### Request Headers

```http
Content-Type: application/json
Authorization: Bearer condt_your_virtual_key
User-Agent: YourApp/1.0
X-Request-ID: unique-request-id (optional)
```

## Core Endpoints

### Chat Completions

**Primary endpoint for conversational AI:**

```bash
curl https://api.conduit.yourdomain.com/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "gpt-4",
    "messages": [
      {
        "role": "system",
        "content": "You are a helpful assistant."
      },
      {
        "role": "user", 
        "content": "Explain quantum computing in simple terms."
      }
    ],
    "max_tokens": 1000,
    "temperature": 0.7,
    "stream": false
  }'
```

**Supported Parameters:**
- `model` (required): Model identifier
- `messages` (required): Conversation history
- `max_tokens`: Maximum response length
- `temperature`: Response randomness (0.0-2.0)
- `top_p`: Nucleus sampling parameter
- `frequency_penalty`: Reduce repetition
- `presence_penalty`: Encourage topic diversity
- `stream`: Enable streaming responses
- `functions`: Function calling definitions
- `function_call`: Function calling mode
- `tools`: Tool definitions (newer format)
- `tool_choice`: Tool selection strategy

### Embeddings

**Generate vector embeddings for text:**

```bash
curl https://api.conduit.yourdomain.com/v1/embeddings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "text-embedding-3-large",
    "input": [
      "The quick brown fox jumps over the lazy dog.",
      "Machine learning is a subset of artificial intelligence."
    ],
    "encoding_format": "float",
    "dimensions": 1536
  }'
```

**Supported Parameters:**
- `model` (required): Embedding model
- `input` (required): Text or array of texts
- `encoding_format`: "float" or "base64"
- `dimensions`: Output dimensions (model-dependent)
- `user`: User identifier for tracking

### Models

**List available models:**

```bash
curl https://api.conduit.yourdomain.com/v1/models \
  -H "Authorization: Bearer condt_your_virtual_key"
```

**Response:**
```json
{
  "object": "list",
  "data": [
    {
      "id": "gpt-4",
      "object": "model",
      "created": 1677610602,
      "owned_by": "openai",
      "provider": "openai",
      "capabilities": ["chat", "function_calling"],
      "context_length": 8192,
      "max_output_tokens": 4096,
      "pricing": {
        "input_tokens_per_1k": 0.03,
        "output_tokens_per_1k": 0.06
      }
    }
  ]
}
```

## Advanced Features

### Streaming Responses

**Real-time response streaming:**

```javascript
const stream = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [{role: 'user', content: 'Tell me a story'}],
  stream: true
});

for await (const chunk of stream) {
  process.stdout.write(chunk.choices[0]?.delta?.content || '');
}
```

**Streaming Response Format:**
```
data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1677652288,"model":"gpt-4","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1677652288,"model":"gpt-4","choices":[{"index":0,"delta":{"content":" there"},"finish_reason":null}]}

data: [DONE]
```

### Function Calling

**Enable AI to call functions:**

```javascript
const completion = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [{
    role: 'user',
    content: 'What is the weather like in Boston?'
  }],
  functions: [
    {
      name: 'get_current_weather',
      description: 'Get the current weather in a given location',
      parameters: {
        type: 'object',
        properties: {
          location: {
            type: 'string',
            description: 'The city and state, e.g. San Francisco, CA'
          },
          unit: {
            type: 'string',
            enum: ['celsius', 'fahrenheit']
          }
        },
        required: ['location']
      }
    }
  ],
  function_call: 'auto'
});
```

### Multimodal Support

**Send images with text:**

```javascript
const completion = await openai.chat.completions.create({
  model: 'gpt-4-vision-preview',
  messages: [
    {
      role: 'user',
      content: [
        {
          type: 'text',
          text: 'What is in this image?'
        },
        {
          type: 'image_url',
          image_url: {
            url: 'https://example.com/image.jpg'
          }
        }
      ]
    }
  ],
  max_tokens: 1000
});
```

## Provider Routing

### Automatic Model Routing

Conduit automatically routes requests to appropriate providers:

```javascript
// Request for GPT-4 automatically routed to OpenAI
const gptResponse = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [{role: 'user', content: 'Hello'}]
});

// Request for Claude automatically routed to Anthropic
const claudeResponse = await openai.chat.completions.create({
  model: 'claude-3-sonnet',
  messages: [{role: 'user', content: 'Hello'}]
});
```

### Provider Selection

Provider selection is handled automatically based on the model name and available provider configurations.

### Fallback Handling

Conduit automatically handles provider failures:

1. **Primary Provider**: Attempt request with preferred provider
2. **Health Check**: Verify provider availability
3. **Fallback**: Route to backup provider if primary fails
4. **Response**: Return successful response or error

## Error Handling

### Standard Error Responses

```json
{
  "error": {
    "message": "The model 'invalid-model' does not exist",
    "type": "invalid_request_error",
    "param": "model",
    "code": "model_not_found"
  }
}
```

### Error Types

| Error Type | HTTP Status | Description |
|------------|-------------|-------------|
| `invalid_request_error` | 400 | Malformed request |
| `authentication_error` | 401 | Invalid API key |
| `permission_error` | 403 | Insufficient permissions |
| `not_found_error` | 404 | Resource not found |
| `rate_limit_error` | 429 | Rate limit exceeded |
| `api_error` | 500 | Internal server error |
| `overloaded_error` | 503 | Service temporarily unavailable |

### Error Handling Examples

```javascript
try {
  const completion = await openai.chat.completions.create({
    model: 'gpt-4',
    messages: [{role: 'user', content: 'Hello'}]
  });
} catch (error) {
  if (error.status === 429) {
    // Handle rate limiting
    console.log('Rate limited, retrying in', error.headers['retry-after'], 'seconds');
  } else if (error.status === 401) {
    // Handle authentication error
    console.log('Invalid API key');
  } else {
    // Handle other errors
    console.log('API error:', error.message);
  }
}
```

## Rate Limiting

### Rate Limit Headers

Responses include rate limiting information:

```http
X-RateLimit-Limit-Requests: 1000
X-RateLimit-Remaining-Requests: 999
X-RateLimit-Reset-Requests: 1642267800
X-RateLimit-Limit-Tokens: 100000
X-RateLimit-Remaining-Tokens: 95000
X-RateLimit-Reset-Tokens: 1642267800
```

### Handling Rate Limits

```javascript
class ConduitClient {
  async makeRequest(requestFn) {
    try {
      return await requestFn();
    } catch (error) {
      if (error.status === 429) {
        const retryAfter = parseInt(error.headers['retry-after'] || '60');
        console.log(`Rate limited, waiting ${retryAfter} seconds`);
        await new Promise(resolve => setTimeout(resolve, retryAfter * 1000));
        return await requestFn(); // Retry once
      }
      throw error;
    }
  }
}
```

## Request/Response Examples

### Basic Chat Completion

**Request:**
```json
{
  "model": "gpt-3.5-turbo",
  "messages": [
    {"role": "user", "content": "Hello, how are you?"}
  ]
}
```

**Response:**
```json
{
  "id": "chatcmpl-abc123",
  "object": "chat.completion",
  "created": 1677652288,
  "model": "gpt-3.5-turbo",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "Hello! I'm doing well, thank you for asking. How can I help you today?"
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 12,
    "completion_tokens": 19,
    "total_tokens": 31
  },
  "conduit_metadata": {
    "provider": "openai",
    "model_version": "gpt-3.5-turbo-0125",
    "virtual_key_id": "550e8400-e29b-41d4-a716-446655440000",
    "request_id": "req-abc123",
    "processing_time_ms": 1234,
    "cost": 0.0000465
  }
}
```

### Function Calling Response

**Response with Function Call:**
```json
{
  "id": "chatcmpl-def456",
  "object": "chat.completion",
  "created": 1677652288,
  "model": "gpt-4",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": null,
        "function_call": {
          "name": "get_current_weather",
          "arguments": "{\"location\": \"Boston, MA\"}"
        }
      },
      "finish_reason": "function_call"
    }
  ],
  "usage": {
    "prompt_tokens": 82,
    "completion_tokens": 18,
    "total_tokens": 100
  }
}
```

## Client Libraries

### .NET Client

The repository includes a .NET client in `/Clients/DotNet/`. You can reference it in your projects:

```csharp
using ConduitLLM.Client;

var client = new ConduitClient("condt_your_virtual_key", "https://api.conduit.yourdomain.com");

var response = await client.ChatCompletions.CreateAsync(new ChatCompletionRequest
{
    Model = "gpt-4",
    Messages = new[] { new Message { Role = "user", Content = "Hello" } }
});
```

### Using OpenAI Libraries

**Python with OpenAI library:**
```python
from openai import OpenAI

client = OpenAI(
    api_key="condt_your_virtual_key",
    base_url="https://api.conduit.yourdomain.com/v1"
)

response = client.chat.completions.create(
    model="gpt-4",
    messages=[{"role": "user", "content": "Hello"}]
)
```

**JavaScript with OpenAI library:**
```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'https://api.conduit.yourdomain.com/v1'
});

const completion = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [{role: 'user', content: 'Hello'}]
});
```

## Performance Optimization

### Connection Pooling

```javascript
// Reuse client instances for connection pooling
const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'https://api.conduit.yourdomain.com/v1',
  httpAgent: new https.Agent({
    keepAlive: true,
    maxSockets: 20
  })
});
```

### Caching Responses

```javascript
class CachedConduitClient {
  constructor(client) {
    this.client = client;
    this.cache = new Map();
  }

  async getCachedCompletion(params) {
    const cacheKey = JSON.stringify(params);
    
    if (this.cache.has(cacheKey)) {
      return this.cache.get(cacheKey);
    }

    const response = await this.client.chat.completions.create(params);
    this.cache.set(cacheKey, response);
    
    return response;
  }
}
```

### Batch Processing

```javascript
async function processBatch(prompts) {
  const promises = prompts.map(prompt => 
    openai.chat.completions.create({
      model: 'gpt-3.5-turbo',
      messages: [{role: 'user', content: prompt}]
    })
  );

  try {
    const results = await Promise.allSettled(promises);
    return results.map((result, index) => ({
      prompt: prompts[index],
      success: result.status === 'fulfilled',
      response: result.status === 'fulfilled' ? result.value : null,
      error: result.status === 'rejected' ? result.reason : null
    }));
  } catch (error) {
    console.error('Batch processing failed:', error);
    throw error;
  }
}
```

## Security Best Practices

### API Key Security

1. **Environment Variables**: Store keys in environment variables
2. **Key Rotation**: Rotate virtual keys regularly
3. **Scope Limitation**: Use keys with minimal required permissions
4. **Monitoring**: Monitor key usage for anomalies

```javascript
// Good: Use environment variables
const client = new OpenAI({
  apiKey: process.env.CONDUIT_API_KEY,
  baseURL: process.env.CONDUIT_BASE_URL
});

// Bad: Hardcoded keys
const client = new OpenAI({
  apiKey: 'condt_hardcoded_key_here' // Never do this!
});
```

### Request Validation

```javascript
function validateChatRequest(params) {
  if (!params.model) {
    throw new Error('Model is required');
  }
  
  if (!params.messages || !Array.isArray(params.messages)) {
    throw new Error('Messages must be an array');
  }
  
  if (params.max_tokens && params.max_tokens > 4096) {
    throw new Error('max_tokens cannot exceed 4096');
  }
  
  return true;
}
```

## Monitoring and Debugging

### Request IDs

Track requests using correlation IDs:

```javascript
const response = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [{role: 'user', content: 'Hello'}]
}, {
  headers: {
    'X-Request-ID': 'req-' + Date.now()
  }
});

console.log('Request ID:', response.conduit_metadata?.request_id);
```

### Usage Tracking

```javascript
function logUsage(response) {
  const metadata = response.conduit_metadata;
  console.log({
    requestId: metadata?.request_id,
    model: response.model,
    provider: metadata?.provider,
    tokensUsed: response.usage?.total_tokens,
    cost: metadata?.cost,
    processingTime: metadata?.processing_time_ms
  });
}
```

## Migration from OpenAI

### Minimal Changes Required

**Before (OpenAI direct):**
```javascript
const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY
});
```

**After (Conduit):**
```javascript
const openai = new OpenAI({
  apiKey: process.env.CONDUIT_VIRTUAL_KEY,
  baseURL: 'https://api.conduit.yourdomain.com/v1'
});
```

### Provider-Specific Features

Some provider-specific features may require adjustments:

```javascript
// Anthropic-specific features through Conduit
const response = await openai.chat.completions.create({
  model: 'claude-3-sonnet',
  messages: [{role: 'user', content: 'Hello'}],
  // Anthropic-specific parameters
  anthropic_version: '2023-06-01',
  stop_sequences: ['\n\nHuman:']
});
```

## Next Steps

- **Text-to-Speech**: Explore [text-to-speech capabilities](../audio/text-to-speech)
- **Real-Time Audio**: Learn about [real-time audio streaming](../audio/real-time-audio)
- **Media Generation**: Discover [image and video generation](../media/image-generation)
- **Administration**: Manage your setup via [Admin API](../admin/admin-api-overview)