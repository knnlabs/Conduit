# Conduit WebUI API Reference

## Overview

This reference documents all API endpoints exposed by the Conduit WebUI. The WebUI is a Next.js administrative project that provides API endpoints serving as abstractions for the Node SDK functions, enabling seamless integration between the frontend and the Conduit backend services.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Authentication](#authentication)
3. [Core API Endpoints](#core-api-endpoints)
4. [Admin API Endpoints](#admin-api-endpoints)
5. [Media & Asset Management](#media--asset-management)
6. [Monitoring & Analytics](#monitoring--analytics)
7. [Configuration Management](#configuration-management)
8. [Security & Access Control](#security--access-control)
9. [Real-time Features](#real-time-features)
10. [Error Handling](#error-handling)
11. [Types and Interfaces](#types-and-interfaces)

## Architecture Overview

The WebUI API serves as a thin abstraction layer over the Node SDK, providing:

- **Centralized SDK Management**: Singleton pattern for Admin and Core clients
- **Automatic Authentication**: WebUI virtual key auto-creation and management
- **Enhanced Error Handling**: Standardized error responses via `handleSDKError`
- **Session Integration**: NextAuth session management
- **Real-time Updates**: SignalR integration for live data

```typescript
// SDK Client Architecture
WebUI API → Node SDK → Backend Services
    ↓           ↓            ↓
  Next.js   Admin/Core   Admin/Core APIs
```

## Authentication

### Session Authentication

All endpoints require authentication via NextAuth session unless otherwise specified.

```typescript
// Session-based authentication (default)
export async function GET(request: NextRequest) {
  // Session automatically validated
  const adminClient = getServerAdminClient();
  // ...
}
```

### Virtual Key Authentication

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

### Master Key Authentication

Backend communication uses master key authentication:

```typescript
// Automatic master key handling
const adminClient = getServerAdminClient(); // Uses CONDUIT_API_TO_API_BACKEND_AUTH_KEY
```

## Core API Endpoints

Core API endpoints provide direct access to AI capabilities via the Core SDK.

### Chat Completions

#### `POST /api/chat/completions`

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

#### `POST /api/images/generate`

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

#### `POST /api/videos/generate`

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

Admin API endpoints provide access to system management capabilities via the Admin SDK.

### Virtual Keys Management

#### `GET /api/virtualkeys`

List all virtual keys with pagination support.

**Query Parameters:**
```
page: number (default: 1)
pageSize: number (default: 100, max: 100)
search?: string
includeDisabled?: boolean
sortBy?: string
sortOrder?: 'asc' | 'desc'
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

#### `POST /api/virtualkeys`

Create a new virtual key.

**Request:**
```typescript
interface CreateVirtualKeyRequest {
  keyName: string;
  virtualKeyGroupId: number; // Required: Virtual key group ID
  maxBudget?: number;
  allowedModels?: string; // Comma-separated model names
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

### Virtual Key Groups

#### `GET /api/virtualkeys/groups`

List all virtual key groups with balance tracking.

**Response:**
```typescript
interface VirtualKeyGroupListResponse {
  data: Array<{
    id: number;
    name: string;
    description?: string;
    balance: number;
    isEnabled: boolean;
    createdAt: string;
    updatedAt: string;
    virtualKeyCount: number;
    totalSpend: number;
  }>;
  meta: {
    timestamp: string;
    total: number;
  };
}
```

#### `POST /api/virtualkeys/groups`

Create a new virtual key group.

**Request:**
```typescript
interface CreateVirtualKeyGroupRequest {
  name: string;
  description?: string;
  initialBalance?: number;
  isEnabled?: boolean;
}
```

## Media & Asset Management

### Media Assets

#### `GET /api/media`

Retrieve media assets by virtual key.

**Query Parameters:**
```
virtualKeyId: string (required)
```

**Response:**
```typescript
interface MediaListResponse {
  data: Array<{
    id: string;
    virtualKeyId: string;
    fileName: string;
    fileSize: number;
    mimeType: string;
    url: string;
    createdAt: string;
    metadata?: object;
  }>;
  meta: {
    timestamp: string;
    total: number;
  };
}
```

#### `DELETE /api/media`

Delete a media asset.

**Request:**
```typescript
interface DeleteMediaRequest {
  mediaId: string;
}
```

#### `POST /api/media/cleanup`

Cleanup old or unused media assets.

**Request:**
```typescript
interface MediaCleanupRequest {
  olderThanDays?: number;
  virtualKeyId?: string;
  dryRun?: boolean;
}
```

## Monitoring & Analytics

### Cache Monitoring

#### `GET /api/cache/monitoring/status`

Get cache monitoring status and health metrics.

**Response:**
```typescript
interface CacheMonitoringResponse {
  data: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    caches: Array<{
      name: string;
      status: string;
      hitRate: number;
      memoryUsage: number;
      keyCount: number;
    }>;
    alerts: Array<{
      level: 'warning' | 'critical';
      message: string;
      timestamp: string;
    }>;
  };
  meta: {
    timestamp: string;
  };
}
```

### Error Queue Management

#### `GET /api/error-queues`

List all error queues with statistics.

**Response:**
```typescript
interface ErrorQueueListResponse {
  data: Array<{
    name: string;
    messageCount: number;
    errorRate: number;
    lastError?: string;
    lastErrorTime?: string;
    status: 'healthy' | 'warning' | 'critical';
  }>;
  meta: {
    timestamp: string;
    totalQueues: number;
    totalMessages: number;
  };
}
```

#### `POST /api/error-queues/{queueName}/clear`

Clear all messages from an error queue.

#### `POST /api/error-queues/{queueName}/replay-all`

Replay all messages in an error queue.

## Configuration Management

### Routing Configuration

#### `GET /api/config/routing`

Get current routing configuration.

**Response:**
```typescript
interface RoutingConfigResponse {
  data: {
    rules: Array<{
      id: string;
      name: string;
      conditions: Array<{
        field: string;
        operator: string;
        value: string;
      }>;
      actions: Array<{
        type: string;
        parameters: object;
      }>;
      priority: number;
      isEnabled: boolean;
    }>;
    fallbackProvider?: string;
    loadBalancing: {
      strategy: 'round_robin' | 'weighted' | 'least_connections';
      healthCheckInterval: number;
    };
  };
  meta: {
    timestamp: string;
  };
}
```

#### `PUT /api/config/routing`

Update routing configuration.

### Caching Configuration

#### `GET /api/config/caching`

Get caching configuration for all cache instances.

#### `POST /api/config/caching/{cacheId}/clear`

Clear a specific cache instance.

## Security & Access Control

### IP Filtering

#### `GET /api/admin/security/ip-rules`

List all IP filtering rules.

**Response:**
```typescript
interface IpRuleListResponse {
  data: Array<{
    id: string;
    ipAddress: string;
    cidrRange?: string;
    action: 'allow' | 'deny';
    description?: string;
    isEnabled: boolean;
    createdAt: string;
    lastMatchedAt?: string;
    matchCount: number;
  }>;
  meta: {
    timestamp: string;
    total: number;
  };
}
```

#### `POST /api/admin/security/ip-rules`

Create a new IP filtering rule.

**Request:**
```typescript
interface CreateIpRuleRequest {
  ipAddress?: string;
  cidrRange?: string;
  action: 'allow' | 'deny';
  description?: string;
  isEnabled?: boolean;
}
```

### System Information

#### `GET /api/admin/system/info`

Get system information and health status.

**Response:**
```typescript
interface SystemInfoResponse {
  data: {
    version: string;
    environment: string;
    uptime: number;
    memoryUsage: {
      used: number;
      total: number;
      percentage: number;
    };
    cpuUsage: number;
    diskUsage: {
      used: number;
      total: number;
      percentage: number;
    };
    databaseStatus: 'connected' | 'disconnected';
    redisStatus: 'connected' | 'disconnected';
    dependencies: Array<{
      name: string;
      status: 'healthy' | 'unhealthy';
      version?: string;
      latency?: number;
    }>;
  };
  meta: {
    timestamp: string;
  };
}
```

## Real-time Features

### SignalR Integration

The WebUI supports real-time updates via SignalR for:

- **Video Generation Progress**: Real-time progress updates for video generation tasks
- **Navigation State Updates**: Live updates for model mappings, providers, and virtual keys
- **Spend Updates**: Real-time spending notifications and alerts
- **System Health**: Live system health and performance metrics

**Client-side Usage:**
```typescript
// Enable progress tracking for video generation
const response = await fetch('/api/videos/generate', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    prompt: 'A beautiful sunset',
    useProgressTracking: true // Enable SignalR progress tracking
  })
});
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

## Error Handling

The WebUI API provides standardized error handling via the `handleSDKError` utility.

### Error Response Format

All error responses follow a consistent format:

```typescript
interface ErrorResponse {
  error: string; // Human-readable error message
  code?: string; // Machine-readable error code
  details?: any; // Additional error context
  timestamp: string;
  statusCode?: number; // HTTP status code
}
```

### SDK Error Abstraction

The WebUI automatically handles and transforms SDK errors:

```typescript
// Automatic error handling in API routes
try {
  const adminClient = getServerAdminClient();
  const result = await adminClient.virtualKeys.list(page, pageSize);
  return NextResponse.json(result);
} catch (error) {
  return handleSDKError(error); // Standardized error transformation
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

This API reference provides comprehensive documentation for all Conduit WebUI endpoints. The WebUI serves as a powerful abstraction layer over the Node SDK, offering:

### Key Features

1. **SDK Abstraction**: Thin wrapper over Node SDK functions with centralized client management
2. **Enhanced Streaming**: Real-time metrics and progress tracking for AI operations
3. **Comprehensive Management**: Full CRUD operations for virtual keys, providers, and configurations
4. **Advanced Monitoring**: Cache monitoring, error queue management, and system health tracking
5. **Security & Access Control**: IP filtering, audit logging, and access policies
6. **Real-time Updates**: SignalR integration for live data and progress tracking
7. **Media Management**: Asset lifecycle management with cleanup capabilities
8. **Standardized Error Handling**: Consistent error responses via `handleSDKError`
9. **Type Safety**: Full TypeScript definitions for all endpoints and responses
10. **Flexible Authentication**: Multiple authentication methods including session, virtual key, and master key

### Architecture Benefits

- **Centralized Configuration**: Single point of SDK client management
- **Automatic Authentication**: WebUI virtual key auto-creation and management
- **Error Consistency**: Standardized error handling across all endpoints
- **Performance Optimization**: Singleton pattern for SDK clients
- **Real-time Capabilities**: Built-in SignalR support for live updates

### Integration Patterns

The WebUI API follows consistent patterns:

```typescript
// Standard API route pattern
export async function GET(request: NextRequest) {
  try {
    const adminClient = getServerAdminClient();
    const result = await adminClient.service.method();
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}
```

For additional examples and integration guides, see the [Integration Examples](./INTEGRATION-EXAMPLES.md) documentation.