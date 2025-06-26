# API Reference

ConduitLLM provides two distinct APIs:

1. **LLM API**: OpenAI-compatible API for LLM interactions
2. **Admin API**: Management API for configuration and monitoring

## LLM API

The LLM API is fully OpenAI-compatible. If you've used OpenAI's API before, you'll feel right at home. You can leverage your existing knowledge, client libraries, and integrations with minimal or no changes.

### Authentication

LLM API endpoints require authentication using a **Virtual Key**. Pass your key in the HTTP header:

```
Authorization: Bearer condt_yourvirtualkey
```

### Base URLs

- **LLM API**: `http://localhost:5002/v1` (OpenAI-compatible API)
- **Admin API**: `http://localhost:5001/api` (Management API)
- **WebUI**: `http://localhost:5000` (Admin Dashboard)

### LLM API Endpoints

- [Chat Completions](#chat-completions)
- [Model Listing](#list-models)
- [Embeddings](#embeddings)
- [Image Generation](#image-generation)
- [Real-Time Features](./Real-Time-Features-Index.md) - Webhooks, SignalR, and async updates

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

**Example of multimodal message:**
```json
{
  "role": "user",
  "content": [
    { "type": "text", "text": "What is in this image?" },
    { "type": "image_url", "image_url": { "url": "https://example.com/cat.png" } }
  ]
}
```

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

## Admin API

The Admin API provides endpoints for administrative operations like managing virtual keys, configuring providers, and monitoring usage. This API is used primarily by the WebUI but can also be used directly for automation or integration purposes.

### Authentication

Admin API endpoints require authentication using the master key:

```
Authorization: Bearer your_master_key_here
```

### Admin API Endpoints

Below are the main categories of Admin API endpoints:

#### Virtual Keys Management

```
GET    /api/virtualkeys            - List all virtual keys
POST   /api/virtualkeys            - Create a new virtual key
GET    /api/virtualkeys/{id}       - Get a specific virtual key
PUT    /api/virtualkeys/{id}       - Update a virtual key
DELETE /api/virtualkeys/{id}       - Delete a virtual key
```

#### Provider Configuration

```
GET    /api/providers               - List all providers
POST   /api/providers               - Add a new provider
GET    /api/providers/{id}          - Get a specific provider
PUT    /api/providers/{id}          - Update a provider
DELETE /api/providers/{id}          - Delete a provider
POST   /api/providers/test          - Test provider connectivity
```

#### Model Mappings

```
GET    /api/mappings               - List all model mappings
POST   /api/mappings               - Create a new model mapping
PUT    /api/mappings/{id}          - Update a model mapping
DELETE /api/mappings/{id}          - Delete a model mapping
```

#### Usage and Monitoring

```
GET    /api/logs                   - Get request logs
GET    /api/cost-dashboard         - Get cost dashboard data
GET    /api/provider-health        - Get provider health status
```

#### System Configuration

```
GET    /api/settings               - Get global settings
PUT    /api/settings               - Update global settings
GET    /api/system-info            - Get system information
POST   /api/database/backup        - Create database backup
```

---

## Environment Variables for Database Location

### SQLite Database Path

To specify a custom location for the SQLite database file (e.g., when using Docker with a mounted volume), set the `CONDUIT_SQLITE_PATH` environment variable:

```bash
export CONDUIT_SQLITE_PATH=/data/conduit.db
```

If `CONDUIT_SQLITE_PATH` is set, it will override the default location and any value in `DB_CONNECTION_STRING` for SQLite. This ensures your database is stored on persistent storage, such as a Docker volume.

- If not set, the application falls back to `DB_CONNECTION_STRING` or the default path from `appsettings.json`.

#### Example (Docker Compose)
```yaml
environment:
  - DB_PROVIDER=sqlite
  - CONDUIT_SQLITE_PATH=/data/conduit.db
volumes:
  - ./my-data:/data
```

### Other Providers
- For PostgreSQL, use `DB_CONNECTION_STRING` as before.

See the [Environment Variables](Environment-Variables.md) documentation for more details on environment variable usage.

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

- **Model Aliases**: Use model aliases as configured in your admin panel or through the Admin API.
- **Multimodal Vision**: For models supporting vision/multimodal, pass image URLs or multimodal objects (see example above) in `messages.content`. The property type is `object?` and supports both plain strings and objects.
- **Streaming**: Set `"stream": true` for streaming responses.
- **API Compatibility**: ConduitLLM aims for API compatibility with OpenAI clients and SDKs.
- **Service Architecture**: The LLM API communicates with the Admin API to retrieve configurations and record usage statistics.