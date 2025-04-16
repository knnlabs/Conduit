# API Reference

This document provides a comprehensive reference for the ConduitLLM API endpoints.

## Authentication

Most API endpoints require authentication using either:

1. **Virtual Key**: Standard API access using the `X-API-Key` header
2. **Master Key**: Administrative access using the `X-Master-Key` header (required for sensitive operations)

## Base URLs

- **Default**: `http://localhost:5000/api` (may vary based on your configuration)

## Endpoint Categories

- [LLM Endpoints](#llm-endpoints)
- [Virtual Key Endpoints](#virtual-key-endpoints)
- [Provider Endpoints](#provider-endpoints)
- [Router Endpoints](#router-endpoints)
- [Model Mapping Endpoints](#model-mapping-endpoints)

## LLM Endpoints

### Completion Request

```
POST /llm/completion
```

Creates a completion for the provided prompt.

**Headers**:
- `X-API-Key`: Your virtual key (required)

**Request Body**:
```json
{
  "model": "generic-model-name",  // Generic model name, will be mapped to provider-specific model
  "prompt": "Your prompt text",
  "max_tokens": 1024,
  "temperature": 0.7,
  "top_p": 1.0,
  "frequency_penalty": 0.0,
  "presence_penalty": 0.0,
  "stop": ["stop1", "stop2"]
}
```

**Response**:
```json
{
  "id": "completion-id",
  "object": "completion",
  "created": 1638990400,
  "model": "provider-specific-model",
  "choices": [
    {
      "text": "Generated completion text",
      "index": 0,
      "logprobs": null,
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 10,
    "completion_tokens": 20,
    "total_tokens": 30
  }
}
```

### Chat Completion Request

```
POST /llm/chat/completion
```

Creates a chat completion for the provided messages.

**Headers**:
- `X-API-Key`: Your virtual key (required)

**Request Body**:
```json
{
  "model": "generic-model-name",
  "messages": [
    {"role": "system", "content": "You are a helpful assistant."},
    {"role": "user", "content": "Hello!"}
  ],
  "max_tokens": 1024,
  "temperature": 0.7,
  "top_p": 1.0,
  "frequency_penalty": 0.0,
  "presence_penalty": 0.0,
  "stop": ["stop1", "stop2"]
}
```

**Response**:
```json
{
  "id": "chat-completion-id",
  "object": "chat.completion",
  "created": 1638990400,
  "model": "provider-specific-model",
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": "Hello! How can I help you today?"
      },
      "index": 0,
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

```
POST /llm/chat/completion/stream
```

Creates a streaming chat completion. Results are returned as server-sent events.

**Headers**:
- `X-API-Key`: Your virtual key (required)

**Request Body**: Same as regular chat completion

**Response**: Server-sent events in the format:
```
data: {"id":"...", "object":"chat.completion.chunk", "created":1638990400, "model":"...", "choices":[{"delta":{"role":"assistant"}, "index":0, "finish_reason":null}]}

data: {"id":"...", "object":"chat.completion.chunk", "created":1638990400, "model":"...", "choices":[{"delta":{"content":"Hello"}, "index":0, "finish_reason":null}]}

data: {"id":"...", "object":"chat.completion.chunk", "created":1638990400, "model":"...", "choices":[{"delta":{"content":"!"}, "index":0, "finish_reason":null}]}

data: {"id":"...", "object":"chat.completion.chunk", "created":1638990400, "model":"...", "choices":[{"delta":{}, "index":0, "finish_reason":"stop"}]}

data: [DONE]
```

## Virtual Key Endpoints

### List All Virtual Keys

```
GET /virtual-keys
```

Returns a list of all virtual keys.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**:
```json
{
  "keys": [
    {
      "id": "key-id",
      "name": "Key Name",
      "key": "vk-xxxxxxxxxxxxxxxx",
      "created": "2023-01-01T00:00:00Z",
      "expiration": "2024-01-01T00:00:00Z",
      "budget": 100.0,
      "spent": 25.5,
      "isActive": true,
      "lastUsed": "2023-06-01T12:34:56Z"
    },
    // More keys...
  ]
}
```

### Create Virtual Key

```
POST /virtual-keys
```

Creates a new virtual key.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**:
```json
{
  "name": "New Key Name",
  "expiration": "2024-01-01T00:00:00Z",  // Optional
  "budget": 100.0,                       // Optional
  "isActive": true                       // Optional, default: true
}
```

**Response**:
```json
{
  "id": "key-id",
  "name": "New Key Name",
  "key": "vk-xxxxxxxxxxxxxxxx",
  "created": "2023-01-01T00:00:00Z",
  "expiration": "2024-01-01T00:00:00Z",
  "budget": 100.0,
  "spent": 0.0,
  "isActive": true,
  "lastUsed": null
}
```

### Get Virtual Key

```
GET /virtual-keys/{id}
```

Gets a specific virtual key by ID.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**: Same format as in the list endpoint

### Update Virtual Key

```
PUT /virtual-keys/{id}
```

Updates a virtual key.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**:
```json
{
  "name": "Updated Key Name",
  "expiration": "2024-02-01T00:00:00Z",
  "budget": 150.0,
  "isActive": true
}
```

**Response**: Updated key object

### Delete Virtual Key

```
DELETE /virtual-keys/{id}
```

Deletes a virtual key.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**: Status 204 No Content

### Reset Spend

```
POST /virtual-keys/{id}/reset-spend
```

Resets the spent amount for a virtual key to zero.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**: Updated key object

## Provider Endpoints

### List Providers

```
GET /providers
```

Lists all configured providers.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**:
```json
{
  "providers": [
    {
      "id": "provider-id",
      "name": "OpenAI",
      "endpoint": "https://api.openai.com/v1",
      "isActive": true
    },
    // More providers...
  ]
}
```

### Create Provider

```
POST /providers
```

Creates a new provider configuration.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**:
```json
{
  "name": "OpenAI",
  "endpoint": "https://api.openai.com/v1",
  "apiKey": "sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "isActive": true
}
```

**Response**: Created provider object (API key not included)

### Get Provider

```
GET /providers/{id}
```

Gets a specific provider configuration.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**: Provider object (API key not included)

### Update Provider

```
PUT /providers/{id}
```

Updates a provider configuration.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**: Same as create provider

**Response**: Updated provider object (API key not included)

### Delete Provider

```
DELETE /providers/{id}
```

Deletes a provider configuration.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**: Status 204 No Content

## Router Endpoints

### Get Router Configuration

```
GET /router/config
```

Gets the current router configuration.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**:
```json
{
  "strategy": "round-robin",
  "deployments": [
    {
      "id": "deployment-id",
      "model": "generic-model-name",
      "provider": "provider-id",
      "weight": 1.0,
      "isActive": true
    },
    // More deployments...
  ],
  "fallbacks": [
    {
      "primaryModel": "generic-model-1",
      "fallbackModels": ["generic-model-2", "generic-model-3"]
    },
    // More fallbacks...
  ]
}
```

### Update Router Strategy

```
PUT /router/strategy
```

Updates the routing strategy.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**:
```json
{
  "strategy": "round-robin"  // One of: "simple", "random", "round-robin"
}
```

**Response**: Updated router configuration

### Add Model Deployment

```
POST /router/deployments
```

Adds a new model deployment.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**:
```json
{
  "model": "generic-model-name",
  "provider": "provider-id",
  "weight": 1.0,
  "isActive": true
}
```

**Response**: Created deployment object

### Update Model Deployment

```
PUT /router/deployments/{id}
```

Updates a model deployment.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**: Same as add deployment

**Response**: Updated deployment object

### Delete Model Deployment

```
DELETE /router/deployments/{id}
```

Deletes a model deployment.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**: Status 204 No Content

### Configure Fallbacks

```
POST /router/fallbacks
```

Configures fallback models for a primary model.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**:
```json
{
  "primaryModel": "generic-model-1",
  "fallbackModels": ["generic-model-2", "generic-model-3"]
}
```

**Response**: Updated router configuration

## Model Mapping Endpoints

### List Model Mappings

```
GET /model-mappings
```

Lists all model mappings.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**:
```json
{
  "mappings": [
    {
      "id": "mapping-id",
      "genericModel": "generic-model-name",
      "provider": "provider-id",
      "providerModel": "provider-specific-model-name",
      "isActive": true
    },
    // More mappings...
  ]
}
```

### Create Model Mapping

```
POST /model-mappings
```

Creates a new model mapping.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**:
```json
{
  "genericModel": "generic-model-name",
  "provider": "provider-id",
  "providerModel": "provider-specific-model-name",
  "isActive": true
}
```

**Response**: Created mapping object

### Get Model Mapping

```
GET /model-mappings/{id}
```

Gets a specific model mapping.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**: Mapping object

### Update Model Mapping

```
PUT /model-mappings/{id}
```

Updates a model mapping.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Request Body**: Same as create mapping

**Response**: Updated mapping object

### Delete Model Mapping

```
DELETE /model-mappings/{id}
```

Deletes a model mapping.

**Headers**:
- `X-Master-Key`: Your master key (required)

**Response**: Status 204 No Content

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
