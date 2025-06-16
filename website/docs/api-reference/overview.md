---
sidebar_position: 1
title: API Overview
description: An overview of Conduit's API
---

# API Overview

Conduit provides an OpenAI-compatible REST API that allows you to interact with various LLM providers through a consistent interface.

## Base URL

The base URL for all API requests is:

```
http://your-conduit-host:5000/v1
```

Replace `your-conduit-host` with your Conduit server address (e.g., `localhost` for local development).

## Authentication

All API requests require authentication using a virtual key in the Authorization header:

```
Authorization: Bearer condt_your_virtual_key
```

Virtual keys can be created and managed through the Conduit Web UI.

## Core Endpoints

Conduit implements the following core endpoints:

### Text Generation
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1/chat/completions` | POST | Create chat completions |
| `/v1/completions` | POST | Create text completions |
| `/v1/embeddings` | POST | Generate text embeddings |
| `/v1/models` | GET | List available models |
| `/v1/images/generations` | POST | Generate images (if configured) |

### Audio Services
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1/audio/transcriptions` | POST | Transcribe audio to text (Speech-to-Text) |
| `/v1/audio/translations` | POST | Transcribe and translate audio to English |
| `/v1/audio/speech` | POST | Generate speech from text (Text-to-Speech) |
| `/v1/audio/speech/stream` | POST | Stream generated speech |
| `/v1/realtime/sessions` | POST | Create real-time audio session |
| `/v1/realtime/sessions/{id}/ws` | WebSocket | Connect to real-time audio stream |

## Response Format

API responses follow the OpenAI format:

```json
{
  "id": "cmpl-abcdef123456",
  "object": "chat.completion",
  "created": 1677858242,
  "model": "my-gpt4",
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": "Hello! How can I help you today?"
      },
      "finish_reason": "stop",
      "index": 0
    }
  ],
  "usage": {
    "prompt_tokens": 10,
    "completion_tokens": 8,
    "total_tokens": 18
  }
}
```

## Error Handling

Conduit returns standard HTTP status codes and error responses:

```json
{
  "error": {
    "message": "Invalid request",
    "type": "invalid_request_error",
    "param": "model",
    "code": "model_not_found"
  }
}
```

Common status codes:

- **200**: Success
- **400**: Bad request (client error)
- **401**: Unauthorized (invalid or missing API key)
- **403**: Forbidden (permissions issue)
- **404**: Not found
- **429**: Too many requests (rate limit exceeded)
- **500**: Server error

## Common Parameters

These parameters are supported across multiple endpoints:

| Parameter | Type | Description |
|-----------|------|-------------|
| `model` | string | The model to use (virtual model name) |
| `user` | string | User identifier for tracking and rate limiting |
| `temperature` | number | Controls randomness (0-2) |
| `top_p` | number | Controls diversity via nucleus sampling |
| `n` | integer | Number of completions to generate |
| `stream` | boolean | Stream responses as they're generated |

## Versioning

The API uses the `/v1` prefix, aligned with the OpenAI API version. Future breaking changes may introduce new version prefixes.

## Rate Limits

Rate limits are applied based on the virtual key settings. When exceeded, the API returns a 429 status code with a `Retry-After` header indicating when to retry.

## Streaming Responses

For streaming endpoints, responses follow the Server-Sent Events (SSE) format, with each event containing a chunk of the response.

## Next Steps

- [Chat Completions API](chat-completions): Learn about the chat interface
- [Embeddings API](embeddings): Generate vector embeddings
- [Models API](models): List and filter available models
- [Audio API](audio): Speech-to-Text, Text-to-Speech, and real-time audio