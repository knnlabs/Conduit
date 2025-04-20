# API Reference

Welcome to the ConduitLLM API! If you’ve used OpenAI’s API before, you’ll feel right at home—ConduitLLM is fully OpenAI-compatible. You can leverage your existing knowledge, client libraries, and integrations with minimal or no changes. This reference will guide you through the endpoints and features, so you can get started quickly and confidently.

## Authentication

All API endpoints require authentication using a **Virtual Key**. Pass your key in the HTTP header:

```
Authorization: Bearer condt_yourvirtualkey
```

## Base URLs

- **Default**: `http://localhost:5000/v1` (API)
- **WebUI**: `http://localhost:5001` (Admin Dashboard)

## Endpoint Overview

- [Chat Completions](#chat-completions)
- [Model Listing](#list-models)
- [Embeddings](#embeddings)
- [Image Generation](#image-generation)

---

## Chat Completions

### Create Chat Completion

```
POST /v1/chat/completions
```

Creates a chat completion for the provided conversation.

**Headers**:
- `Authorization: Bearer condt_yourvirtualkey` (required)

**Request Body**:
```json
{
  "model": "my-gpt4", // Model alias or mapped model name
  "messages": [
    {"role": "system", "content": "You are a helpful assistant."},
    {"role": "user", "content": "Hello!"}
  ],
  "max_tokens": 1024,
  "temperature": 0.7,
  "top_p": 1.0,
  "frequency_penalty": 0.0,
  "presence_penalty": 0.0,
  "stop": ["stop1", "stop2"],
  "stream": false
}
```
- `messages.content` may be a string or multimodal object (see Multimodal Vision Support).

**Response**:
```json
{
  "id": "chatcmpl-abc123",
  "object": "chat.completion",
  "created": 1710000000,
  "model": "openai/gpt-4",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "Hello! How can I help you today?"
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 15,
    "completion_tokens": 8,
    "total_tokens": 23
  }
}
```

### Streaming Chat Completion

Set `"stream": true` in the request body. Response is sent as server-sent events (SSE):
```
data: {"id": ..., "object": "chat.completion.chunk", ...}
data: {"id": ..., "object": "chat.completion.chunk", ...}
data: [DONE]
```

---

## List Models

```
GET /v1/models
```

Lists all models available to your key.

**Headers**:
- `Authorization: Bearer condt_yourvirtualkey` (required)

**Response**:
```json
{
  "object": "list",
  "data": [
    {
      "id": "my-gpt4",
      "object": "model",
      "created": 1677610602,
      "owned_by": "conduitllm"
    }
    // ...
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

**Request Body**:
```json
{
  "model": "embedding-model",
  "input": ["text1", "text2"]
}
```

**Response**:
```json
{
  "object": "list",
  "data": [
    {"object": "embedding", "embedding": [0.1, 0.2, ...], "index": 0},
    {"object": "embedding", "embedding": [0.1, 0.2, ...], "index": 1}
  ],
  "model": "embedding-model",
  "usage": {"prompt_tokens": 10, "total_tokens": 10}
}
```

---

## Image Generation

```
POST /v1/images/generations
```

Generates images from a prompt (if supported by the model/provider).

**Headers**:
- `Authorization: Bearer condt_yourvirtualkey` (required)

**Request Body**:
```json
{
  "model": "image-model",
  "prompt": "A futuristic city skyline",
  "n": 1,
  "size": "1024x1024"
}
```

**Response**:
```json
{
  "created": 1710000000,
  "data": [
    {"url": "https://.../image1.png"}
  ]
}
```

---

## Error Responses

All endpoints may return the following error responses:

- **400 Bad Request**: Invalid request format or parameters
- **401 Unauthorized**: Missing or invalid authentication
- **403 Forbidden**: Insufficient permissions
- **404 Not Found**: Resource not found
- **429 Too Many Requests**: Rate limit exceeded
- **500 Internal Server Error**: Server-side error

Error response format:
```json
{
  "error": {
    "code": "error_code",
    "message": "Error message description"
  }
}
```

---

## Notes

- **Model Aliases**: Use model aliases as configured in your admin panel or `appsettings.json`.
- **Multimodal Vision**: For models supporting vision/multimodal, pass image URLs or objects in `messages.content` (see Multimodal Vision Support doc).
- **Streaming**: Set `"stream": true` for streaming responses.
- **API Compatibility**: ConduitLLM aims for API compatibility with OpenAI clients and SDKs.
