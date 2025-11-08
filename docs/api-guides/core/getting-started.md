# Core API - Getting Started

This guide will help you get started with the Conduit Core API.

## Authentication

All API requests require authentication using Virtual Keys. Virtual Keys are managed through the Admin API or WebUI.

### Header Format

```http
Authorization: Bearer condt_yourvirtualkey
```

### Example Request

```bash
curl -X POST http://localhost:5002/v1/chat/completions \
  -H "Authorization: Bearer condt_sk_1234567890" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-4",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
```

## Quick Start with SDKs

### Node.js/TypeScript

```bash
npm install @knn_labs/conduit-core-client
```

```typescript
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

const client = new ConduitCoreClient({
  apiKey: 'condt_yourvirtualkey',
  baseURL: 'http://localhost:5002'
});

// Create a chat completion
const response = await client.chat.create({
  model: 'gpt-4',
  messages: [
    { role: 'system', content: 'You are a helpful assistant.' },
    { role: 'user', content: 'Hello!' }
  ],
  temperature: 0.7
});

console.log(response.choices[0].message.content);
```

### Python

```bash
pip install openai
```

```python
import openai

# Configure the client to use Conduit
openai.api_base = "http://localhost:5002/v1"
openai.api_key = "condt_sk_1234567890"

response = openai.ChatCompletion.create(
    model="gpt-4",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": "Hello!"}
    ],
    temperature=0.7
)

print(response.choices[0].message.content)
```

### C#/.NET

```bash
dotnet add package OpenAI
```

```csharp
using OpenAI_API;

var api = new OpenAIAPI("condt_sk_1234567890");
api.ApiUrlFormat = "http://localhost:5002/v1/{0}";

var response = await api.Chat.CreateChatCompletionAsync(
    new ChatRequest
    {
        Model = "gpt-4",
        Messages = new[]
        {
            new ChatMessage("system", "You are a helpful assistant."),
            new ChatMessage("user", "Hello!")
        }
    }
);

Console.WriteLine(response.Choices[0].Message.Content);
```

## Basic Chat Completion

### Request

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

### Response

```json
{
  "id": "chatcmpl-123456",
  "object": "chat.completion",
  "created": 1694365200,
  "model": "gpt-4",
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

## Streaming Responses

Enable streaming by setting `stream: true` in your request:

### Python Example

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

### JavaScript Example

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

## Next Steps

- **[API Reference](./api-reference.md)** - Detailed endpoint documentation
- **[Function Calling](../features/function-calling.md)** - Enable LLMs to use tools
- **[Multimodal Vision](../features/multimodal-vision.md)** - Work with images
- **[Streaming Guide](../features/streaming.md)** - Advanced streaming patterns
- **[SDK Documentation](../sdk/)** - Complete SDK integration guides

## Common Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `model` | string | Model alias or mapped model name |
| `messages` | array | Conversation history |
| `temperature` | number | 0-2, controls randomness (default: 1) |
| `max_tokens` | number | Maximum tokens to generate |
| `top_p` | number | 0-1, nucleus sampling (default: 1) |
| `frequency_penalty` | number | -2 to 2, penalize repetition (default: 0) |
| `presence_penalty` | number | -2 to 2, encourage new topics (default: 0) |
| `stop` | array | Stop sequences |
| `stream` | boolean | Enable streaming responses |

## Error Handling

### Common Error Codes

| HTTP Status | Error Type | Description |
|-------------|------------|-------------|
| 400 | `invalid_request_error` | Invalid request format |
| 401 | `authentication_error` | Invalid or missing API key |
| 403 | `permission_error` | Insufficient permissions |
| 404 | `not_found_error` | Resource not found |
| 429 | `rate_limit_error` | Rate limit exceeded |
| 500 | `server_error` | Internal server error |

### Example Error Response

```json
{
  "error": {
    "message": "Invalid API key provided",
    "type": "authentication_error",
    "code": "invalid_api_key"
  }
}
```

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

## Best Practices

1. **Use Environment Variables**: Store API keys in environment variables
2. **Implement Retry Logic**: Handle transient errors gracefully
3. **Monitor Usage**: Track token usage and costs via the Admin UI
4. **Set Timeouts**: Configure appropriate timeouts for your use case
5. **Use Streaming**: For better UX with long responses
6. **Cache Responses**: When appropriate for your use case
7. **Validate Inputs**: Check message format and parameters before sending

## Getting Help

- **[API Reference](./api-reference.md)** - Complete endpoint documentation
- **[Feature Guides](../features/)** - In-depth guides for specific features
- **[Troubleshooting](../sdk/troubleshooting.md)** - Common issues and solutions
- **[GitHub Issues](https://github.com/knnlabs/Conduit/issues)** - Report bugs or request features
