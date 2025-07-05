# Conduit WebUI API Reference

## Overview

This reference documents all API endpoints exposed by the Conduit WebUI, including request/response formats, authentication requirements, and SDK usage examples.

## Table of Contents

1. [Authentication](#authentication)
2. [Core API Endpoints](#core-api-endpoints)
3. [Admin API Endpoints](#admin-api-endpoints)
4. [Utility Functions](#utility-functions)
5. [Types and Interfaces](#types-and-interfaces)
6. [Error Responses](#error-responses)

## Authentication

### Session Authentication

All endpoints require authentication via NextAuth session unless otherwise specified.

```typescript
// Authentication middleware usage
export const GET = withSDKAuth(
  async (request, { auth }) => {
    // auth.session contains user session
    // auth.adminClient available if requireAdmin: true
    // auth.coreClient available if virtual key present
  },
  { requireAdmin: true }
);
```

### Virtual Key Authentication

Core API endpoints accept virtual keys via multiple methods:

```typescript
// 1. Request body
{
  "virtual_key": "vk_abc123...",
  "prompt": "Hello"
}

// 2. Header
headers: {
  "x-virtual-key": "vk_abc123..."
}

// 3. Authorization header
headers: {
  "Authorization": "Bearer vk_abc123..."
}
```

## Core API Endpoints

### Chat Completions

#### `POST /api/core/chat/completions`

Create chat completions with optional streaming.

**Request:**
```typescript
interface ChatCompletionRequest {
  virtual_key?: string; // Required if not in headers/session
  model: string;
  messages: Array<{
    role: 'system' | 'user' | 'assistant';
    content: string;
  }>;
  stream?: boolean;
  temperature?: number;
  max_tokens?: number;
  top_p?: number;
  frequency_penalty?: number;
  presence_penalty?: number;
  stop?: string | string[];
  user?: string;
  functions?: Array<{
    name: string;
    description?: string;
    parameters?: object;
  }>;
  function_call?: 'none' | 'auto' | { name: string };
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

**Response (Streaming):**
```
data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1234567890,"model":"gpt-4","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}
data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1234567890,"model":"gpt-4","choices":[{"index":0,"delta":{"content":" world"},"finish_reason":null}]}
data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1234567890,"model":"gpt-4","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}
data: [DONE]
```

**SDK Example:**
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

### Image Generation

#### `POST /api/core/images/generations`

Generate images from text prompts.

**Request:**
```typescript
interface ImageGenerationRequest {
  virtual_key?: string;
  prompt: string;
  model?: string; // Default: 'dall-e-3'
  n?: number; // Number of images (1-10)
  size?: '256x256' | '512x512' | '1024x1024' | '1792x1024' | '1024x1792';
  quality?: 'standard' | 'hd';
  style?: 'vivid' | 'natural';
  response_format?: 'url' | 'b64_json';
  user?: string;
  // Provider-specific
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

**SDK Example:**
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

### Video Generation

#### `POST /api/core/videos/generations`

Generate videos from text prompts (async operation).

**Request:**
```typescript
interface VideoGenerationRequest {
  virtual_key?: string;
  prompt: string;
  model?: string; // Default: 'video-01'
  duration?: number; // Seconds (max: 6)
  resolution?: '720x480' | '1280x720' | '1920x1080' | '720x1280' | '1080x1920';
  fps?: number; // Frames per second
  aspect_ratio?: string;
  style?: string;
  user?: string;
  // Provider-specific
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

#### `GET /api/core/videos/generations?task_id={taskId}`

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

#### `DELETE /api/core/videos/generations?task_id={taskId}`

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

### Audio Transcription

#### `POST /api/core/audio/transcriptions`

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

**SDK Example:**
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

### Health Check

#### `GET /api/core/health`

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

#### `HEAD /api/core/health`

Lightweight health check.

**Response Headers:**
```
X-Health-Status: healthy | unhealthy
X-Checked-At: 2024-01-01T12:00:00Z
```

## Admin API Endpoints

### Virtual Keys

#### `GET /api/admin/virtual-keys`

List all virtual keys.

**Query Parameters:**
```
page: number (default: 1)
pageSize: number (default: 20, max: 100)
search: string
includeDisabled: boolean
sortBy: string
sortOrder: 'asc' | 'desc'
```

**Response:**
```typescript
interface VirtualKeyListResponse {
  data: Array<{
    id: string;
    keyName: string;
    keyHash: string;
    isEnabled: boolean;
    createdAt: string;
    maxBudget?: number;
    currentSpend: number;
    allowedModels?: string[];
    metadata?: object;
    requestCount: number;
    lastUsedAt?: string;
  }>;
  meta: {
    timestamp: string;
    pagination: {
      page: number;
      pageSize: number;
      total: number;
      totalPages: number;
    };
  };
}
```

#### `POST /api/admin/virtual-keys`

Create a new virtual key.

**Request:**
```typescript
interface CreateVirtualKeyRequest {
  keyName: string;
  maxBudget?: number;
  allowedModels?: string[];
  metadata?: object;
  isEnabled?: boolean; // Default: true
}
```

**Response:**
```typescript
interface CreateVirtualKeyResponse {
  data: {
    id: string;
    key: string; // Full key (only shown once)
    keyName: string;
    keyHash: string;
    createdAt: string;
  };
  meta: {
    timestamp: string;
    created: true;
  };
}
```

#### `GET /api/admin/virtual-keys/{id}`

Get virtual key details.

**Response:**
```typescript
interface VirtualKeyDetailsResponse {
  data: {
    id: string;
    keyName: string;
    keyHash: string;
    isEnabled: boolean;
    createdAt: string;
    updatedAt: string;
    maxBudget?: number;
    currentSpend: number;
    allowedModels?: string[];
    metadata?: object;
    requestCount: number;
    lastUsedAt?: string;
    spendHistory: Array<{
      date: string;
      amount: number;
      requestCount: number;
    }>;
  };
  meta: {
    timestamp: string;
  };
}
```

#### `PUT /api/admin/virtual-keys/{id}`

Update virtual key.

**Request:**
```typescript
interface UpdateVirtualKeyRequest {
  keyName?: string;
  isEnabled?: boolean;
  maxBudget?: number;
  allowedModels?: string[];
  metadata?: object;
}
```

#### `DELETE /api/admin/virtual-keys/{id}`

Delete virtual key.

### Providers

#### `GET /api/admin/providers`

List all providers.

**Response:**
```typescript
interface ProviderListResponse {
  data: Array<{
    id: string;
    name: string;
    type: string;
    isEnabled: boolean;
    priority: number;
    health: {
      status: 'healthy' | 'degraded' | 'unhealthy';
      lastChecked: string;
      latency?: number;
    };
    capabilities: string[];
    modelCount: number;
  }>;
  meta: {
    timestamp: string;
  };
}
```

#### `POST /api/admin/providers`

Create a new provider.

**Request:**
```typescript
interface CreateProviderRequest {
  name: string;
  type: 'openai' | 'anthropic' | 'google' | 'azure' | 'custom';
  credentials: {
    apiKey: string;
    endpoint?: string;
    orgId?: string;
  };
  priority?: number; // 0-100
  isEnabled?: boolean;
  metadata?: object;
}
```

#### `POST /api/admin/providers/{id}/test`

Test provider connection.

**Response:**
```typescript
interface ProviderTestResponse {
  data: {
    success: boolean;
    latency: number;
    models?: string[];
    error?: string;
  };
  meta: {
    timestamp: string;
  };
}
```

### Model Mappings

#### `GET /api/admin/model-mappings`

List all model mappings.

**Response:**
```typescript
interface ModelMappingListResponse {
  data: Array<{
    id: string;
    modelName: string;
    providerId: string;
    providerName: string;
    providerModelName: string;
    isEnabled: boolean;
    priority: number;
    capabilities: string[];
    contextWindow?: number;
    pricing?: {
      inputCost: number;
      outputCost: number;
      unit: string;
    };
  }>;
  meta: {
    timestamp: string;
  };
}
```

#### `POST /api/admin/model-mappings`

Create model mapping.

**Request:**
```typescript
interface CreateModelMappingRequest {
  modelName: string;
  providerId: string;
  providerModelName: string;
  priority?: number;
  isEnabled?: boolean;
  capabilities?: string[];
  contextWindow?: number;
  pricing?: {
    inputCost: number;
    outputCost: number;
    unit: string;
  };
}
```

#### `POST /api/admin/model-mappings/discover`

Discover available models from providers.

**Response:**
```typescript
interface ModelDiscoveryResponse {
  data: {
    providers: Array<{
      providerId: string;
      providerName: string;
      models: Array<{
        name: string;
        displayName: string;
        capabilities: string[];
        contextWindow?: number;
      }>;
    }>;
  };
  meta: {
    timestamp: string;
    totalModels: number;
  };
}
```

### Settings

#### `GET /api/admin/system/settings`

Get system settings.

**Query Parameters:**
```
category: string (optional)
```

**Response:**
```typescript
interface SystemSettingsResponse {
  data: {
    [category: string]: {
      [key: string]: any;
    };
  };
  meta: {
    timestamp: string;
    categories: string[];
  };
}
```

#### `PUT /api/admin/system/settings`

Update system settings.

**Request:**
```typescript
interface UpdateSettingsRequest {
  category: string;
  settings: {
    [key: string]: any;
  };
}
```

### Security

#### `GET /api/admin/security/ip-rules`

List IP filtering rules.

**Response:**
```typescript
interface IPRuleListResponse {
  data: Array<{
    id: string;
    ipAddress: string;
    action: 'allow' | 'deny';
    description?: string;
    isEnabled: boolean;
    expiresAt?: string;
    createdAt: string;
    metadata?: object;
  }>;
  meta: {
    timestamp: string;
    pagination: {
      page: number;
      pageSize: number;
      total: number;
    };
  };
}
```

#### `POST /api/admin/security/ip-rules`

Create IP rule.

**Request:**
```typescript
interface CreateIPRuleRequest {
  ipAddress: string; // Supports CIDR notation
  action: 'allow' | 'deny';
  description?: string;
  expiresAt?: string;
  metadata?: object;
  isTemporary?: boolean;
}
```

## Utility Functions

### Error Handling

```typescript
// lib/errors/sdk-errors.ts
export async function withSDKErrorHandling<T>(
  operation: () => Promise<T>,
  context: string
): Promise<T>

export function mapSDKErrorToResponse(
  error: any,
  options?: ErrorOptions
): Response

export class SDKError extends Error {
  constructor(
    message: string,
    public statusCode: number,
    public code: string,
    public details?: any
  )
}
```

### Response Transformation

```typescript
// lib/utils/sdk-transforms.ts
export function transformSDKResponse<T>(
  data: T,
  options?: TransformOptions
): Response

export function transformPaginatedResponse<T>(
  items: T[],
  pagination: PaginationInfo
): Response

export function createStreamingResponse(
  stream: AsyncIterable<any>,
  options?: StreamOptions
): Response
```

### Authentication Helpers

```typescript
// lib/auth/sdk-auth.ts
export function withSDKAuth<T extends Record<string, any>>(
  handler: (request: NextRequest, context: T & { auth: SDKAuth }) => Promise<Response>,
  options?: AuthOptions
): (request: NextRequest, context: T) => Promise<Response>

export async function validateCoreSession(
  request: NextRequest,
  options?: { requireVirtualKey?: boolean }
): Promise<SessionValidation>

export function extractVirtualKey(
  request: NextRequest
): string | null
```

### Route Helpers

```typescript
// lib/utils/route-helpers.ts
export function createDynamicRouteHandler<TParams extends Record<string, string>>(
  handler: (request: NextRequest, context: { params: TParams; auth: SDKAuth }) => Promise<Response>,
  options?: AuthOptions
): (request: NextRequest, context: { params: Promise<TParams> }) => Promise<Response>

export function parseQueryParams(
  request: NextRequest
): QueryParams

export function validateRequiredFields(
  body: any,
  fields: string[]
): ValidationResult

export function createValidationError(
  message: string,
  details?: any
): Response
```

## Types and Interfaces

### Common Types

```typescript
interface QueryParams {
  page: number;
  pageSize: number;
  search?: string;
  sortBy?: string;
  sortOrder: 'asc' | 'desc';
  startDate?: string;
  endDate?: string;
  get(key: string): string | null;
}

interface PaginationInfo {
  page: number;
  pageSize: number;
  total: number;
  totalPages?: number;
  hasMore?: boolean;
}

interface TransformOptions {
  status?: number;
  headers?: HeadersInit;
  meta?: Record<string, any>;
  cacheControl?: string;
}

interface SDKAuth {
  session: Session | null;
  adminClient?: ConduitAdminClient;
  coreClient?: ConduitCoreClient;
}

interface AuthOptions {
  requireAuth?: boolean;
  requireAdmin?: boolean;
  requireVirtualKey?: boolean;
}
```

### Real-time Event Types

```typescript
interface NavigationStateUpdate {
  type: 'model_mapping' | 'provider' | 'virtual_key';
  action: 'created' | 'updated' | 'deleted';
  data: any;
  timestamp: string;
}

interface VideoGenerationProgress {
  taskId: string;
  status: 'queued' | 'processing' | 'completed' | 'failed';
  progress: number;
  estimatedTimeRemaining?: number;
  resultUrl?: string;
  error?: string;
}

interface SpendUpdate {
  virtualKeyId: string;
  amount: number;
  totalSpend: number;
  model: string;
  timestamp: string;
}

interface SpendLimitAlert {
  virtualKeyId: string;
  currentSpend: number;
  limit: number;
  percentage: number;
  alertLevel: 'warning' | 'critical';
}
```

## Error Responses

All error responses follow a consistent format:

```typescript
interface ErrorResponse {
  error: string; // Human-readable error message
  code?: string; // Machine-readable error code
  details?: any; // Additional error context
  timestamp: string;
}
```

### Common Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `UNAUTHORIZED` | 401 | Missing or invalid authentication |
| `FORBIDDEN` | 403 | Insufficient permissions |
| `NOT_FOUND` | 404 | Resource not found |
| `VALIDATION_ERROR` | 400 | Invalid request data |
| `RATE_LIMITED` | 429 | Too many requests |
| `INTERNAL_ERROR` | 500 | Server error |
| `SERVICE_UNAVAILABLE` | 503 | Service temporarily unavailable |

### Error Examples

```json
// 400 Bad Request
{
  "error": "Validation failed",
  "code": "VALIDATION_ERROR",
  "details": {
    "errors": [
      "keyName is required",
      "maxBudget must be positive"
    ]
  },
  "timestamp": "2024-01-01T12:00:00Z"
}

// 401 Unauthorized
{
  "error": "Invalid virtual key",
  "code": "UNAUTHORIZED",
  "timestamp": "2024-01-01T12:00:00Z"
}

// 429 Rate Limited
{
  "error": "Rate limit exceeded",
  "code": "RATE_LIMITED",
  "details": {
    "limit": 100,
    "window": "1m",
    "retryAfter": 45
  },
  "timestamp": "2024-01-01T12:00:00Z"
}
```

## Rate Limiting

API endpoints implement rate limiting:

- **Anonymous**: 10 requests/minute
- **Authenticated**: 100 requests/minute
- **Virtual Key**: Based on key configuration
- **Admin**: 1000 requests/minute

Rate limit headers:
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1704110460
Retry-After: 45 (when rate limited)
```

## Conclusion

This API reference provides comprehensive documentation for all Conduit WebUI endpoints. Key features:

1. **Consistent patterns** across all endpoints
2. **Multiple authentication methods** for flexibility
3. **Comprehensive error handling** with meaningful messages
4. **Real-time support** via SignalR
5. **Type-safe** with full TypeScript definitions

For additional examples and integration guides, see the [Integration Examples](./INTEGRATION-EXAMPLES.md) documentation.