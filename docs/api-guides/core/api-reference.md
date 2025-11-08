# Core API Reference

Complete endpoint documentation for the Conduit Core API.

## Table of Contents

1. [Chat Completions](#chat-completions)
2. [Streaming Responses](#streaming-responses)
3. [Multimodal Support](#multimodal-support)
4. [Function Calling](#function-calling)
5. [Models Endpoint](#models-endpoint)
6. [Embeddings](#embeddings)
7. [Image Generation](#image-generation)
8. [Rate Limiting](#rate-limiting)
9. [Performance Considerations](#performance-considerations)

---

## Chat Completions

### Create Chat Completion

```
POST /v1/chat/completions
```

Creates a chat completion for the provided conversation.

**Headers**:
- `Authorization: Bearer condt_yourvirtualkey` (required)

#### Request Parameters

```json
{
  "model": "gpt-4",
  "messages": [
    {"role": "system", "content": "You are a helpful assistant."},
    {"role": "user", "content": "What is the capital of France?"}
  ],
  "temperature": 0.7,
  "max_tokens": 150,
  "top_p": 0.9,
  "frequency_penalty": 0.5,
  "presence_penalty": 0.5,
  "stop": ["END", "STOP"],
  "n": 1,
  "user": "user123",
  "seed": 42,
  "stream": false
}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `model` | string | Yes | Model alias or mapped model name |
| `messages` | array | Yes | Array of message objects with `role` and `content` |
| `temperature` | number | No | 0-2, controls randomness (default: 1) |
| `max_tokens` | number | No | Maximum tokens to generate |
| `top_p` | number | No | 0-1, nucleus sampling (default: 1) |
| `frequency_penalty` | number | No | -2 to 2, penalize repetition (default: 0) |
| `presence_penalty` | number | No | -2 to 2, encourage new topics (default: 0) |
| `stop` | array | No | Up to 4 sequences where API will stop |
| `n` | number | No | Number of completions to generate (default: 1) |
| `user` | string | No | Unique identifier for end-user |
| `seed` | number | No | For deterministic sampling |
| `stream` | boolean | No | Enable streaming responses (default: false) |

#### Response Format

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

**Finish Reasons:**
- `stop`: Natural stopping point or stop sequence reached
- `length`: Maximum token limit reached
- `function_call`: Model requested function call (deprecated)
- `tool_calls`: Model requested tool execution
- `content_filter`: Content filtered by provider

#### SDK Usage

**TypeScript:**
```typescript
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

const client = new ConduitCoreClient({
  apiKey: 'condt_yourvirtualkey',
  baseURL: 'http://localhost:5002'
});

const response = await client.chat.create({
  model: 'gpt-4',
  messages: [
    { role: 'system', content: 'You are a helpful assistant.' },
    { role: 'user', content: 'What is the capital of France?' }
  ],
  temperature: 0.7,
  max_tokens: 150
});

console.log(response.choices[0].message.content);
```

**Python:**
```python
import openai

openai.api_base = "http://localhost:5002/v1"
openai.api_key = "condt_sk_1234567890"

response = openai.ChatCompletion.create(
    model="gpt-4",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": "What is the capital of France?"}
    ],
    temperature=0.7,
    max_tokens=150
)

print(response.choices[0].message.content)
```

---

## Streaming Responses

Enable streaming by setting `stream: true` in your request. Responses are sent as Server-Sent Events (SSE).

### Streaming Request

```json
{
  "model": "gpt-4",
  "messages": [{"role": "user", "content": "Count to 5"}],
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

---

## Multimodal Support

Conduit supports multimodal inputs for vision-capable models. See [Multimodal Vision Guide](../features/multimodal-vision.md) for detailed information.

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
          "image_url": {"url": "https://example.com/image1.jpg"}
        },
        {
          "type": "image_url",
          "image_url": {"url": "https://example.com/image2.jpg"}
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
        {"type": "text", "text": "Describe this image"},
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

---

## Function Calling

Conduit supports OpenAI's function calling feature. See [Function Calling Guide](../features/function-calling.md) for complete documentation.

### Defining Functions

```json
{
  "model": "gpt-4",
  "messages": [
    {"role": "user", "content": "What's the weather in San Francisco?"}
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
    return f"Weather in {location}: 72°{unit[0].upper()}"

# Initial request
response = openai.ChatCompletion.create(
    model="gpt-4",
    messages=[{"role": "user", "content": "What's the weather in San Francisco?"}],
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

# Execute function if requested
if response.choices[0].finish_reason == "tool_calls":
    tool_call = response.choices[0].message.tool_calls[0]
    args = json.loads(tool_call.function.arguments)
    result = get_current_weather(**args)

    # Send result back
    follow_up = openai.ChatCompletion.create(
        model="gpt-4",
        messages=[
            {"role": "user", "content": "What's the weather in San Francisco?"},
            response.choices[0].message,
            {"role": "tool", "tool_call_id": tool_call.id, "content": result}
        ]
    )

    print(follow_up.choices[0].message.content)
```

---

## Models Endpoint

### List Available Models

```
GET /v1/models
```

Returns a list of models available to your virtual key.

**Headers**:
- `Authorization: Bearer condt_yourvirtualkey` (required)

**Response:**
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

---

## Embeddings

```
POST /v1/embeddings
```

Generates embeddings for input text.

**Headers**:
- `Authorization: Bearer condt_yourvirtualkey` (required)

**Request:**
```json
{
  "model": "text-embedding-ada-002",
  "input": ["text1", "text2"]
}
```

**Response:**
```json
{
  "object": "list",
  "data": [
    {"object": "embedding", "embedding": [0.1, 0.2, ...], "index": 0},
    {"object": "embedding", "embedding": [0.1, 0.2, ...], "index": 1}
  ],
  "model": "text-embedding-ada-002",
  "usage": {"prompt_tokens": 10, "total_tokens": 10}
}
```

---

## Image Generation

```
POST /v1/images/generations
```

Generates images from a text prompt.

**Headers**:
- `Authorization: Bearer condt_yourvirtualkey` (required)

**Request:**
```json
{
  "model": "dall-e-3",
  "prompt": "A futuristic city skyline",
  "n": 1,
  "size": "1024x1024",
  "quality": "standard",
  "response_format": "url"
}
```

**Response:**
```json
{
  "created": 1710000000,
  "data": [
    {"url": "https://cdn.example.com/generated-image.png"}
  ]
}
```

---

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

            retry_after = int(e.response.headers.get('Retry-After', 60))
            print(f"Rate limited. Waiting {retry_after} seconds...")
            time.sleep(retry_after)
```

---

## Performance Considerations

### Connection Pooling

Reuse HTTP connections for better performance:

```python
import httpx
import openai

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

Use streaming for better perceived performance:

```javascript
const stream = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: messages,
  stream: true,
  max_tokens: 1000
});

for await (const chunk of stream) {
  updateUI(chunk.choices[0]?.delta?.content || '');
}
```

---

## Related Documentation

- **[Getting Started](./getting-started.md)** - Quick start guide
- **[Function Calling](../features/function-calling.md)** - Detailed function calling guide
- **[Multimodal Vision](../features/multimodal-vision.md)** - Complete vision API documentation
- **[LLM Routing](../features/llm-routing.md)** - Router configuration and strategies
- **[Webhooks](../features/webhooks.md)** - Webhook notifications for async tasks
- **[SDK Documentation](../sdk/)** - Complete SDK integration guides
