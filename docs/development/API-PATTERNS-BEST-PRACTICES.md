# Conduit SDK API Patterns & Best Practices

## Overview

This guide provides comprehensive patterns and best practices for building APIs with the Conduit SDK in Next.js applications. These patterns have been battle-tested in production and optimize for type safety, performance, and maintainability.

## Table of Contents

1. [Core Principles](#core-principles)
2. [Route Structure](#route-structure)
3. [Authentication Patterns](#authentication-patterns)
4. [Error Handling](#error-handling)
5. [Data Validation](#data-validation)
6. [Response Formatting](#response-formatting)
7. [Pagination Patterns](#pagination-patterns)
8. [Streaming Responses](#streaming-responses)
9. [File Uploads](#file-uploads)
10. [Caching Strategies](#caching-strategies)
11. [Testing Patterns](#testing-patterns)
12. [Security Best Practices](#security-best-practices)

## Core Principles

### 1. Type Safety First
Always leverage TypeScript's type system:
```typescript
// ✅ Good - Fully typed
export const GET = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, auth }) => {
    const virtualKey = await auth.adminClient!.virtualKeys.get(params.id);
    return transformSDKResponse(virtualKey);
  }
);

// ❌ Bad - Untyped params
export async function GET(request: NextRequest, { params }: any) {
  const id = params.id; // No type safety
}
```

### 2. Consistent Error Handling
Use the centralized error handling system:
```typescript
// ✅ Good - Wrapped with error handling
const result = await withSDKErrorHandling(
  async () => client.someOperation(),
  'descriptive context for debugging'
);

// ❌ Bad - Raw try/catch
try {
  const result = await client.someOperation();
} catch (error) {
  console.error(error);
  return new Response('Error', { status: 500 });
}
```

### 3. Separation of Concerns
Keep route handlers thin:
```typescript
// ✅ Good - Thin route handler
export const POST = withSDKAuth(
  async (request, { auth }) => {
    const body = await request.json();
    const validated = validateCreateRequest(body);
    const result = await createResource(auth.adminClient!, validated);
    return transformSDKResponse(result, { status: 201 });
  }
);

// ❌ Bad - Business logic in route
export async function POST(request: NextRequest) {
  // 100+ lines of business logic...
}
```

## Route Structure

### Standard CRUD Pattern
```typescript
// app/api/admin/resources/route.ts
export const GET = withSDKAuth(listResources, { requireAdmin: true });
export const POST = withSDKAuth(createResource, { requireAdmin: true });

// app/api/admin/resources/[id]/route.ts
export const GET = createDynamicRouteHandler(getResource, { requireAdmin: true });
export const PUT = createDynamicRouteHandler(updateResource, { requireAdmin: true });
export const DELETE = createDynamicRouteHandler(deleteResource, { requireAdmin: true });
```

### Nested Resources
```typescript
// app/api/admin/providers/[providerId]/models/route.ts
export const GET = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, auth }) => {
    const models = await auth.adminClient!.providers.listModels(params.providerId);
    return transformSDKResponse(models);
  }
);
```

### Action Endpoints
```typescript
// app/api/admin/providers/[id]/test-connection/route.ts
export const POST = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, auth }) => {
    const result = await auth.adminClient!.providers.testConnection(params.id);
    return transformSDKResponse(result);
  }
);
```

## Authentication Patterns

### Basic Authentication
```typescript
export const GET = withSDKAuth(
  async (request, { auth }) => {
    // auth.session is guaranteed to exist
    // auth.adminClient is available if requireAdmin: true
  },
  { requireAdmin: true }
);
```

### Virtual Key Extraction
```typescript
export async function POST(request: NextRequest) {
  const validation = await validateCoreSession(request, { requireVirtualKey: false });
  
  // Extract from multiple sources
  const virtualKey = body.virtual_key || 
                    extractVirtualKey(request) || 
                    validation.session?.virtualKey;
  
  if (!virtualKey) {
    return createValidationError('Virtual key required');
  }
}
```

### Multi-tenant Authentication
```typescript
export const GET = withSDKAuth(
  async (request, { auth }) => {
    const { organizationId } = parseQueryParams(request);
    
    // Verify user has access to organization
    if (!auth.session!.organizations.includes(organizationId)) {
      return createForbiddenResponse('Access denied to organization');
    }
    
    // Proceed with organization-scoped query
  }
);
```

## Error Handling

### Standard Error Response Format
```typescript
interface ErrorResponse {
  error: string;
  code: string;
  details?: any;
  timestamp: string;
}
```

### Error Handling Patterns
```typescript
// 1. Validation Errors
if (!body.requiredField) {
  return createValidationError('Required field missing', {
    field: 'requiredField',
    provided: Object.keys(body),
  });
}

// 2. Not Found Errors
const resource = await getResource(id);
if (!resource) {
  return createNotFoundResponse(`Resource ${id} not found`);
}

// 3. Permission Errors
if (!hasPermission(user, resource)) {
  return createForbiddenResponse('Insufficient permissions');
}

// 4. Business Logic Errors
if (account.balance < amount) {
  return createBusinessError('Insufficient funds', {
    required: amount,
    available: account.balance,
  });
}
```

### Custom Error Classes
```typescript
export class SDKError extends Error {
  constructor(
    message: string,
    public statusCode: number,
    public code: string,
    public details?: any
  ) {
    super(message);
    this.name = 'SDKError';
  }
}

export class ValidationError extends SDKError {
  constructor(message: string, details?: any) {
    super(message, 400, 'VALIDATION_ERROR', details);
  }
}
```

## Data Validation

### Input Validation
```typescript
export function validateCreateVirtualKeyRequest(body: any): CreateVirtualKeyDto {
  const errors: string[] = [];
  
  if (!body.keyName?.trim()) {
    errors.push('keyName is required');
  }
  
  if (body.maxBudget !== undefined && body.maxBudget < 0) {
    errors.push('maxBudget must be positive');
  }
  
  if (body.allowedModels && !Array.isArray(body.allowedModels)) {
    errors.push('allowedModels must be an array');
  }
  
  if (errors.length > 0) {
    throw new ValidationError('Validation failed', { errors });
  }
  
  return {
    keyName: body.keyName.trim(),
    maxBudget: body.maxBudget,
    allowedModels: body.allowedModels || [],
  };
}
```

### Schema Validation with Zod
```typescript
import { z } from 'zod';

const CreateProviderSchema = z.object({
  name: z.string().min(1).max(100),
  type: z.enum(['openai', 'anthropic', 'google']),
  credentials: z.object({
    apiKey: z.string().min(1),
    endpoint: z.string().url().optional(),
  }),
  priority: z.number().int().min(0).max(100).default(50),
});

export async function createProvider(request: NextRequest) {
  const body = await request.json();
  
  try {
    const validated = CreateProviderSchema.parse(body);
    // Proceed with validated data
  } catch (error) {
    if (error instanceof z.ZodError) {
      return createValidationError('Invalid request', {
        errors: error.errors,
      });
    }
    throw error;
  }
}
```

## Response Formatting

### Standard Response Wrapper
```typescript
interface ApiResponse<T> {
  data: T;
  meta: {
    timestamp: string;
    requestId?: string;
    [key: string]: any;
  };
}
```

### Response Helpers
```typescript
// Success response
return transformSDKResponse(data, {
  status: 200,
  meta: {
    cached: false,
    source: 'database',
  },
});

// Created response
return transformSDKResponse(created, {
  status: 201,
  headers: {
    'Location': `/api/resources/${created.id}`,
  },
});

// No content response
return new Response(null, { status: 204 });

// Paginated response
return transformPaginatedResponse(items, {
  page: 1,
  pageSize: 20,
  total: 100,
  hasMore: true,
});
```

## Pagination Patterns

### Query Parameter Parsing
```typescript
export function parsePaginationParams(request: NextRequest): PaginationParams {
  const { searchParams } = new URL(request.url);
  
  return {
    page: Math.max(1, parseInt(searchParams.get('page') || '1')),
    pageSize: Math.min(100, Math.max(1, parseInt(searchParams.get('pageSize') || '20'))),
    sortBy: searchParams.get('sortBy') || 'createdAt',
    sortOrder: (searchParams.get('sortOrder') || 'desc') as 'asc' | 'desc',
  };
}
```

### Cursor-based Pagination
```typescript
export const GET = withSDKAuth(
  async (request, { auth }) => {
    const { cursor, limit } = parseCursorParams(request);
    
    const result = await auth.adminClient!.resources.list({
      cursor,
      limit,
    });
    
    return transformSDKResponse({
      items: result.items,
      nextCursor: result.nextCursor,
      hasMore: result.hasMore,
    });
  }
);
```

## Streaming Responses

### Server-Sent Events (SSE)
```typescript
export async function POST(request: NextRequest) {
  const body = await request.json();
  
  if (body.stream) {
    const stream = await coreClient.chat.createStream(body);
    
    return createStreamingResponse(stream, {
      transformer: (chunk) => {
        if (chunk.object === 'chat.completion.chunk') {
          return `data: ${JSON.stringify(chunk)}\n\n`;
        }
        if (chunk === '[DONE]') {
          return 'data: [DONE]\n\n';
        }
        return '';
      },
    });
  }
}
```

### Progress Streaming
```typescript
export function createProgressStream(taskId: string) {
  const encoder = new TextEncoder();
  
  const stream = new ReadableStream({
    async start(controller) {
      const sendProgress = (progress: number) => {
        const data = JSON.stringify({ taskId, progress });
        controller.enqueue(encoder.encode(`data: ${data}\n\n`));
      };
      
      // Simulate progress
      for (let i = 0; i <= 100; i += 10) {
        sendProgress(i);
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
      
      controller.enqueue(encoder.encode('data: [DONE]\n\n'));
      controller.close();
    },
  });
  
  return new Response(stream, {
    headers: {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      'Connection': 'keep-alive',
    },
  });
}
```

## File Uploads

### Handling Multipart Forms
```typescript
export async function POST(request: NextRequest) {
  const formData = await request.formData();
  
  // Extract file
  const file = formData.get('file') as File;
  if (!file) {
    return createValidationError('File is required');
  }
  
  // Validate file
  if (file.size > 25 * 1024 * 1024) { // 25MB
    return createValidationError('File too large', {
      maxSize: '25MB',
      provided: `${(file.size / 1024 / 1024).toFixed(2)}MB`,
    });
  }
  
  // Convert to buffer for SDK
  const arrayBuffer = await file.arrayBuffer();
  const buffer = Buffer.from(arrayBuffer);
  
  // Process with SDK
  const result = await coreClient.audio.transcribe({
    file: {
      buffer,
      name: file.name,
      type: file.type,
    },
  });
  
  return transformSDKResponse(result);
}
```

### File Validation
```typescript
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
const MAX_IMAGE_SIZE = 10 * 1024 * 1024; // 10MB

export function validateImageFile(file: File): void {
  if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
    throw new ValidationError('Invalid file type', {
      allowed: ALLOWED_IMAGE_TYPES,
      provided: file.type,
    });
  }
  
  if (file.size > MAX_IMAGE_SIZE) {
    throw new ValidationError('File too large', {
      maxSize: '10MB',
      provided: `${(file.size / 1024 / 1024).toFixed(2)}MB`,
    });
  }
}
```

## Caching Strategies

### Response Caching
```typescript
export const GET = withSDKAuth(
  async (request, { auth }) => {
    const cacheKey = `providers:${auth.session!.organizationId}`;
    const cached = await cache.get(cacheKey);
    
    if (cached) {
      return transformSDKResponse(cached, {
        meta: { cached: true },
        headers: {
          'X-Cache': 'HIT',
          'Cache-Control': 'private, max-age=300',
        },
      });
    }
    
    const result = await auth.adminClient!.providers.list();
    await cache.set(cacheKey, result, 300); // 5 minutes
    
    return transformSDKResponse(result, {
      headers: {
        'X-Cache': 'MISS',
        'Cache-Control': 'private, max-age=300',
      },
    });
  }
);
```

### Conditional Requests
```typescript
export const GET = withSDKAuth(
  async (request, { auth }) => {
    const etag = request.headers.get('if-none-match');
    const resource = await getResource(id);
    const currentEtag = generateEtag(resource);
    
    if (etag === currentEtag) {
      return new Response(null, { status: 304 });
    }
    
    return transformSDKResponse(resource, {
      headers: {
        'ETag': currentEtag,
        'Cache-Control': 'private, must-revalidate',
      },
    });
  }
);
```

## Testing Patterns

### Route Testing
```typescript
import { createMockRequest } from '@/test/utils';

describe('Virtual Keys API', () => {
  it('should list virtual keys', async () => {
    const request = createMockRequest({
      method: 'GET',
      headers: {
        'Authorization': 'Bearer test-master-key',
      },
    });
    
    const response = await GET(request);
    const data = await response.json();
    
    expect(response.status).toBe(200);
    expect(data.data).toBeInstanceOf(Array);
  });
});
```

### Mock SDK Clients
```typescript
export function createMockAdminClient(): ConduitAdminClient {
  return {
    virtualKeys: {
      list: jest.fn().mockResolvedValue({
        data: [],
        page: 1,
        pageSize: 20,
        total: 0,
      }),
      create: jest.fn().mockResolvedValue({
        id: 'test-id',
        keyName: 'Test Key',
      }),
    },
  } as any;
}
```

## Security Best Practices

### 1. Input Sanitization
```typescript
export function sanitizeInput(input: string): string {
  return input
    .trim()
    .replace(/[<>]/g, '') // Remove potential XSS
    .substring(0, 1000); // Limit length
}
```

### 2. Rate Limiting
```typescript
const rateLimiter = new Map<string, number[]>();

export function checkRateLimit(clientId: string, limit = 100): boolean {
  const now = Date.now();
  const windowStart = now - 60000; // 1 minute window
  
  const requests = rateLimiter.get(clientId) || [];
  const recentRequests = requests.filter(time => time > windowStart);
  
  if (recentRequests.length >= limit) {
    return false;
  }
  
  recentRequests.push(now);
  rateLimiter.set(clientId, recentRequests);
  return true;
}
```

### 3. CORS Configuration
```typescript
export function setCORSHeaders(response: Response, origin?: string): Response {
  const headers = new Headers(response.headers);
  
  if (origin && ALLOWED_ORIGINS.includes(origin)) {
    headers.set('Access-Control-Allow-Origin', origin);
  } else {
    headers.set('Access-Control-Allow-Origin', 'null');
  }
  
  headers.set('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  headers.set('Access-Control-Allow-Headers', 'Content-Type, Authorization');
  headers.set('Access-Control-Max-Age', '86400');
  
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
}
```

### 4. Content Security
```typescript
export function setSecurityHeaders(response: Response): Response {
  const headers = new Headers(response.headers);
  
  headers.set('X-Content-Type-Options', 'nosniff');
  headers.set('X-Frame-Options', 'DENY');
  headers.set('X-XSS-Protection', '1; mode=block');
  headers.set('Referrer-Policy', 'strict-origin-when-cross-origin');
  headers.set('Permissions-Policy', 'camera=(), microphone=(), geolocation=()');
  
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
}
```

## Performance Best Practices

### 1. Minimize Payload Size
```typescript
// Only return necessary fields
export const GET = withSDKAuth(
  async (request, { auth }) => {
    const { fields } = parseQueryParams(request);
    const result = await auth.adminClient!.providers.list({
      select: fields?.split(',') || ['id', 'name', 'status'],
    });
    
    return transformSDKResponse(result);
  }
);
```

### 2. Parallel Operations
```typescript
export const GET = withSDKAuth(
  async (request, { auth }) => {
    // Run operations in parallel
    const [providers, models, health] = await Promise.all([
      auth.adminClient!.providers.list(),
      auth.adminClient!.models.list(),
      auth.adminClient!.health.check(),
    ]);
    
    return transformSDKResponse({
      providers,
      models,
      health,
    });
  }
);
```

### 3. Early Returns
```typescript
export const POST = withSDKAuth(
  async (request, { auth }) => {
    const body = await request.json();
    
    // Quick validation checks first
    if (!body.required) {
      return createValidationError('Missing required field');
    }
    
    // Check cache before expensive operation
    const cached = await checkCache(body);
    if (cached) {
      return transformSDKResponse(cached, { meta: { cached: true } });
    }
    
    // Expensive operation last
    const result = await performExpensiveOperation(body);
    return transformSDKResponse(result);
  }
);
```

## Conclusion

These patterns and best practices provide a solid foundation for building robust, maintainable, and performant APIs with the Conduit SDK. Remember to:

1. **Prioritize type safety** - Use TypeScript features fully
2. **Handle errors gracefully** - Provide meaningful error messages
3. **Validate thoroughly** - Never trust client input
4. **Format consistently** - Use standard response formats
5. **Optimize performance** - Cache when appropriate
6. **Secure by default** - Apply security headers and validations
7. **Test comprehensively** - Cover edge cases and error scenarios

Following these patterns will ensure your API is production-ready and provides an excellent developer experience.