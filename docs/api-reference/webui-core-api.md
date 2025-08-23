# WebUI Core API Endpoints

Documentation for Core API endpoints provided by the Conduit WebUI that offer direct access to AI capabilities via the Core SDK.

## Overview

Core API endpoints provide direct access to AI capabilities through the Conduit WebUI, serving as abstractions for the Node SDK functions. These endpoints support multiple authentication methods and provide enhanced features like real-time metrics and streaming.

## Related Documentation

- [WebUI API Reference](./webui-api-reference.md) - Main API reference and overview
- [WebUI Admin API](./webui-admin-api.md) - Administrative endpoints
- [WebUI Authentication](./webui-authentication.md) - Authentication methods and setup
- [Real-Time API Guide](../real-time-api-guide.md) - Real-time features and SignalR

## Authentication

Core API endpoints support multiple virtual key authentication methods:

```typescript
// 1. Request body
{
  "virtual_key": "vk_abc123...",
  "model": "gpt-4",
  "messages": [...]
}

// 2. X-Virtual-Key header
headers: {
  "X-Virtual-Key": "vk_abc123..."
}

// 3. Authorization header
headers: {
  "Authorization": "Bearer vk_abc123..."
}
```

## Chat Completions

### `POST /api/chat/completions`

Create chat completions with enhanced streaming support and real-time metrics.

**Features:**
- Enhanced streaming with real-time metrics events
- Support for tools and function calling
- Multiple authentication methods
- Automatic error handling and retry logic

**Request:**
```typescript
interface ChatCompletionRequest {
  virtual_key?: string; // Required if not in headers/session
  model: string;
  messages: Array<{
    role: 'system' | 'user' | 'assistant';
    content: string | Array<TextContent | ImageContent>;
  }>;
  stream?: boolean;
  temperature?: number;
  max_tokens?: number;
  top_p?: number;
  frequency_penalty?: number;
  presence_penalty?: number;
  stop?: string | string[];
  user?: string;
  tools?: Array<{
    type: 'function';
    function: {
      name: string;
      description?: string;
      parameters?: object;
    };
  }>;
  tool_choice?: 'none' | 'auto' | 'required' | { type: 'function'; function: { name: string } };
  response_format?: { type: 'text' | 'json_object' };
  seed?: number;
  logprobs?: boolean;
  top_logprobs?: number;
}
```

**Response (Non-streaming):**
```typescript
interface ChatCompletionResponse {
  data: {
    id: string;
    object: 'chat.completion';
    created: number;
    model: string;
    choices: Array<{
      index: number;
      message: {
        role: 'assistant';
        content: string;
        function_call?: {
          name: string;
          arguments: string;
        };
        tool_calls?: Array<{
          id: string;
          type: 'function';
          function: {
            name: string;
            arguments: string;
          };
        }>;
      };
      finish_reason: 'stop' | 'length' | 'function_call' | 'tool_calls' | 'content_filter';
      logprobs?: object;
    }>;
    usage: {
      prompt_tokens: number;
      completion_tokens: number;
      total_tokens: number;
    };
  };
  meta: {
    timestamp: string;
    virtualKeyUsed: string;
    streaming: false;
  };
}
```

**Response (Enhanced Streaming):**

The WebUI provides enhanced streaming with multiple event types:

```typescript
// Content events (standard chat completion chunks)
event: content
data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1234567890,"model":"gpt-4","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

// Real-time metrics events
event: metrics
data: {"tokensPerSecond":45.2,"responseTime":1250,"providerLatency":890}

// Final metrics event
event: metrics-final
data: {"totalTokens":150,"promptTokens":20,"completionTokens":130,"totalCost":0.0045,"duration":3200}

// Error events
event: error
data: {"error":"Rate limit exceeded","statusCode":429}

// Stream completion
data: [DONE]
```

**Event Types:**
- `content`: Standard OpenAI-compatible completion chunks
- `metrics`: Real-time performance metrics during generation
- `metrics-final`: Final usage and cost metrics
- `error`: Error events with detailed information

**Example:**
```typescript
// Client-side
const response = await fetch('/api/core/chat/completions', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    virtual_key: 'vk_abc123...',
    model: 'gpt-4',
    messages: [{ role: 'user', content: 'Hello!' }],
    stream: true,
  }),
});

// Process stream
const reader = response.body!.getReader();
const decoder = new TextDecoder();
while (true) {
  const { done, value } = await reader.read();
  if (done) break;
  const chunk = decoder.decode(value);
  // Process chunk...
}
```

## Image Generation

### `POST /api/images/generate`

Generate images with support for both synchronous and asynchronous generation.

**Features:**
- Synchronous and asynchronous generation modes
- Multiple provider support (OpenAI, MiniMax, etc.)
- Webhook support for async operations
- Provider-specific parameters

**Request:**
```typescript
interface ImageGenerationRequest {
  virtual_key?: string; // Required if not in headers/session
  prompt: string;
  model?: string; // Default: 'dall-e-3'
  n?: number; // Number of images (1-10)
  quality?: 'standard' | 'hd';
  response_format?: 'url' | 'b64_json';
  size?: '256x256' | '512x512' | '1024x1024' | '1792x1024' | '1024x1792';
  style?: 'vivid' | 'natural';
  user?: string;
  
  // Async generation options
  async?: boolean; // Enable async generation
  webhook_url?: string; // Webhook for completion notification
  webhook_metadata?: Record<string, unknown>; // Custom webhook data
  timeout_seconds?: number; // Request timeout
  
  // Provider-specific parameters
  aspect_ratio?: string; // For MiniMax: '1:1', '16:9', etc.
  seed?: number;
  steps?: number;
  guidance_scale?: number;
  negative_prompt?: string;
}
```

**Response:**
```typescript
interface ImageGenerationResponse {
  data: {
    created: number;
    data: Array<{
      url?: string;
      b64_json?: string;
      revised_prompt?: string;
    }>;
  };
  meta: {
    timestamp: string;
    virtualKeyUsed: string;
    model: string;
  };
}
```

**Example:**
```typescript
const response = await fetch('/api/core/images/generations', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    virtual_key: 'vk_abc123...',
    prompt: 'A futuristic city at sunset',
    model: 'dall-e-3',
    size: '1024x1024',
    quality: 'hd',
  }),
});

const result = await response.json();
const imageUrl = result.data.data[0].url;
```

## Video Generation

### `POST /api/videos/generate`

Generate videos asynchronously with progress tracking support.

**Features:**
- Asynchronous generation with task tracking
- Progress tracking via SignalR (client-side)
- Webhook support for completion notifications
- Multiple video model support

**Request:**
```typescript
interface AsyncVideoGenerationRequest {
  virtual_key?: string; // Required if not in headers/session
  prompt: string;
  model?: string; // Default model based on provider
  size?: string; // Video resolution
  style?: string;
  user?: string;
  
  // Progress tracking
  useProgressTracking?: boolean; // Enable client-side progress tracking
  
  // Webhook options
  webhook_url?: string;
  webhook_metadata?: Record<string, unknown>;
  
  // Provider-specific parameters
  seed?: number;
  guidance_scale?: number;
  negative_prompt?: string;
  motion_bucket_id?: number;
  conditioning_augmentation?: number;
}
```

**Response:**
```typescript
interface VideoGenerationResponse {
  data: {
    task_id: string;
    status: 'queued';
    created_at: string;
    estimated_time: number; // Seconds
  };
  meta: {
    timestamp: string;
    virtualKeyUsed: string;
    model: string;
    taskId: string;
  };
}
```

### `GET /api/core/videos/generations?task_id={taskId}`

Check video generation status.

**Response:**
```typescript
interface VideoTaskStatusResponse {
  data: {
    task_id: string;
    status: 'queued' | 'processing' | 'completed' | 'failed';
    progress: number; // 0-100
    result_url?: string; // When completed
    error?: string; // When failed
    created_at: string;
    updated_at: string;
  };
  meta: {
    timestamp: string;
  };
}
```

### `DELETE /api/core/videos/generations?task_id={taskId}`

Cancel video generation task.

**Response:**
```typescript
interface VideoTaskCancelResponse {
  data: {
    success: true;
    message: 'Task cancelled successfully';
  };
  meta: {
    timestamp: string;
    cancelled: true;
    taskId: string;
  };
}
```

## Audio Transcription

### `POST /api/core/audio/transcriptions`

Transcribe audio files to text.

**Request (Multipart Form):**
```
file: File (required) - Audio file (mp3, mp4, mpeg, mpga, m4a, wav, webm)
virtual_key: string (optional if in headers)
model: string (optional) - Default: 'whisper-1'
language: string (optional) - ISO language code
prompt: string (optional) - Context prompt
response_format: 'json' | 'text' | 'srt' | 'verbose_json' | 'vtt' (optional)
temperature: number (optional) - 0-1
timestamp_granularities: string (optional) - Comma-separated: 'segment,word'
```

**Response (JSON format):**
```typescript
interface TranscriptionResponse {
  data: {
    text: string;
    language?: string;
    duration?: number;
    segments?: Array<{
      id: number;
      seek: number;
      start: number;
      end: number;
      text: string;
      tokens: number[];
      temperature: number;
      avg_logprob: number;
      compression_ratio: number;
      no_speech_prob: number;
    }>;
    words?: Array<{
      word: string;
      start: number;
      end: number;
    }>;
  };
  meta: {
    timestamp: string;
    virtualKeyUsed: string;
    model: string;
    fileName: string;
    fileSize: number;
  };
}
```

**Example:**
```typescript
const formData = new FormData();
formData.append('file', audioFile);
formData.append('virtual_key', 'vk_abc123...');
formData.append('model', 'whisper-1');
formData.append('language', 'en');
formData.append('response_format', 'json');

const response = await fetch('/api/core/audio/transcriptions', {
  method: 'POST',
  body: formData,
});

const result = await response.json();
console.log('Transcription:', result.data.text);
```

## Health Check

### `GET /api/core/health`

Check Core API health status.

**Response:**
```typescript
interface HealthCheckResponse {
  data: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    checks: Array<{
      name: string;
      status: 'healthy' | 'unhealthy';
      message?: string;
      duration?: number;
    }>;
    timestamp: string;
    version?: string;
    environment?: string;
    uptime?: number;
    dependencies: {
      [key: string]: {
        status: 'healthy' | 'unhealthy';
        latency?: number;
      };
    };
  };
  meta: {
    timestamp: string;
    checkedAt: string;
    responseTime?: number;
  };
}
```

### `HEAD /api/core/health`

Lightweight health check.

**Response Headers:**
```
X-Health-Status: healthy | unhealthy
X-Checked-At: 2024-01-01T12:00:00Z
```

## Error Handling

All Core API endpoints use standardized error handling via the `handleSDKError` utility:

```typescript
interface CoreAPIError {
  error: {
    type: string;
    message: string;
    code?: string;
    details?: any;
  };
  meta: {
    timestamp: string;
    endpoint: string;
    virtualKeyUsed?: string;
  };
}
```

**Common Error Types:**
- `authentication_error`: Invalid or missing virtual key
- `rate_limit_exceeded`: API rate limits exceeded
- `quota_exceeded`: Usage quota exceeded
- `validation_error`: Invalid request parameters
- `provider_error`: External provider failure
- `timeout_error`: Request timeout
- `internal_error`: Unexpected server error

## Best Practices

### Authentication
- Always include virtual key via one of the supported methods
- Use session authentication for WebUI frontend requests
- Include error handling for authentication failures

### Streaming
- Process streaming responses using appropriate event types
- Handle both content and metrics events for full functionality
- Implement proper error handling for stream interruptions

### Async Operations
- Use webhooks for long-running operations when possible
- Implement proper polling strategies for status checks
- Handle task cancellation gracefully

### Error Recovery
- Implement retry logic for transient failures
- Use exponential backoff for rate-limited requests
- Provide meaningful error messages to end users

## Next Steps

- [WebUI Admin API](./webui-admin-api.md) - Administrative endpoint documentation
- [WebUI Authentication](./webui-authentication.md) - Authentication setup and methods
- [Real-Time API Guide](../real-time-api-guide.md) - Real-time features and SignalR integration