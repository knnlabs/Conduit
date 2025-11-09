# Conduit WebUI API Route Standards

This document defines the standard patterns and requirements for all API routes in the Conduit WebUI application. All routes must follow these patterns to ensure consistency, maintainability, and proper SDK usage.

**Last Updated**: January 11, 2025  
**Status**: Active Standard  
**Phase 4 Reference**: Issue #368

## Table of Contents

1. [Standard Route Pattern](#standard-route-pattern)
2. [Authentication Requirements](#authentication-requirements)
3. [SDK Client Usage](#sdk-client-usage)
4. [Error Handling](#error-handling)
5. [Response Formats](#response-formats)
6. [Special Cases](#special-cases)
7. [Common Mistakes](#common-mistakes)
8. [Testing Requirements](#testing-requirements)

## Standard Route Pattern

Every API route MUST follow this standard pattern:

```typescript
import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient, getServerCoreClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// HTTP_METHOD /api/route/path - Brief description
export async function HTTP_METHOD(
  req: NextRequest,
  { params }: { params: Promise<{ paramName: string }> } // Only if route has parameters
) {
  // 1. Authentication check (unless explicitly public)
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    // 2. Parse parameters if needed
    const { paramName } = await params;
    
    // 3. Get SDK client(s)
    const adminClient = getServerAdminClient();
    // const coreClient = getServerCoreClient(); // If needed
    
    // 4. Parse request body if needed
    const body = await req.json(); // Only for POST/PUT/PATCH
    
    // 5. Make SDK call(s)
    const result = await adminClient.service.method(/* args */);
    
    // 6. Return successful response
    return NextResponse.json(result);
  } catch (error) {
    // 7. Use standard error handling
    return handleSDKError(error);
  }
}
```

## Authentication Requirements

### Standard Authentication

All routes MUST use `requireAuth()` unless explicitly documented as public:

```typescript
const auth = requireAuth(req);
if (!auth.isValid) {
  return auth.response!;
}
```

### Admin-Only Routes

For routes that require admin privileges, use `requireAdmin()`:

```typescript
const auth = requireAdmin(req);
if (!auth.isValid) {
  return auth.response!;
}
```

### Public Routes

Only the following routes are allowed to be public:
- `/api/health` - System health check
- `/api/auth/login` - Authentication endpoint
- `/api/auth/refresh` - Token refresh (if implemented)

Public routes must include a comment explaining why authentication is not required:

```typescript
// PUBLIC ROUTE: Health check endpoint for monitoring
export async function GET(req: NextRequest) {
  // No authentication required
  // ...
}
```

## SDK Client Usage

### Getting SDK Clients

Always use the provided helper functions:

```typescript
// For Admin API operations
const adminClient = getServerAdminClient();

// For Core API operations (chat, images, etc.)
const coreClient = getServerCoreClient();
```

### NEVER Access Environment Variables Directly

❌ **Wrong**:
```typescript
const apiKey = process.env.CONDUIT_API_KEY;
const client = new FetchConduitAdminClient({ apiKey });
```

✅ **Correct**:
```typescript
const adminClient = getServerAdminClient();
```

### SDK Method Mapping

Use the correct SDK methods for each operation:

| Operation | SDK Method |
|-----------|-----------|
| List Virtual Keys | `adminClient.virtualKeys.getAll()` |
| Get Virtual Key | `adminClient.virtualKeys.getById(id)` |
| Create Virtual Key | `adminClient.virtualKeys.create(data)` |
| Update Virtual Key | `adminClient.virtualKeys.update(id, data)` |
| Delete Virtual Key | `adminClient.virtualKeys.deleteById(id)` |

## Error Handling

### Standard Error Handler

All routes MUST use `handleSDKError()` for error handling:

```typescript
try {
  // SDK operations
} catch (error) {
  return handleSDKError(error);
}
```

### Error Response Format

The `handleSDKError()` function ensures consistent error responses:

```typescript
{
  "error": "Error message",
  "code": "ERROR_CODE", // Optional
  "details": {} // Optional additional context
}
```

### HTTP Status Code Mapping

| SDK Error Type | HTTP Status | Description |
|----------------|-------------|-------------|
| `AuthenticationError` | 401 | Invalid credentials |
| `AuthorizationError` | 403 | Insufficient permissions |
| `ValidationError` | 400 | Invalid request data |
| `NotFoundError` | 404 | Resource not found |
| `ConflictError` | 409 | Resource conflict |
| `RateLimitError` | 429 | Rate limit exceeded |
| `NetworkError` | 503 | Service unavailable |
| `TimeoutError` | 504 | Gateway timeout |
| `ServerError` | 500 | Internal server error |

## Response Formats

### Successful Responses

#### Single Resource
```typescript
return NextResponse.json(resource);
```

#### Collection
```typescript
return NextResponse.json({
  items: resources,
  total: resources.length,
  page: 1,
  pageSize: 50
});
```

#### No Content
```typescript
return new NextResponse(null, { status: 204 });
```

#### Custom Headers
```typescript
return NextResponse.json(data, {
  headers: {
    'X-Total-Count': total.toString(),
    'X-Page': page.toString()
  }
});
```

### Error Responses

Always use `handleSDKError()` - do not create custom error responses.

## Special Cases

### File Downloads

For routes that return files (images, exports, etc.):

```typescript
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const coreClient = getServerCoreClient();
    const result = await coreClient.images.generate(params);
    
    return new NextResponse(result.image, {
      headers: {
        'Content-Type': 'image/png',
        'Content-Disposition': 'attachment; filename="generated.png"'
      }
    });
  } catch (error) {
    return handleSDKError(error);
  }
}
```

### Streaming Responses

For Server-Sent Events or streaming:

```typescript
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  const encoder = new TextEncoder();
  const stream = new ReadableStream({
    async start(controller) {
      try {
        const coreClient = getServerCoreClient();
        const stream = await coreClient.chat.completions.create({
          ...params,
          stream: true
        });
        
        for await (const chunk of stream) {
          controller.enqueue(encoder.encode(`data: ${JSON.stringify(chunk)}\n\n`));
        }
        
        controller.close();
      } catch (error) {
        controller.error(error);
      }
    }
  });

  return new NextResponse(stream, {
    headers: {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      'Connection': 'keep-alive'
    }
  });
}
```

### Webhooks

Webhook endpoints may skip authentication but MUST validate the request:

```typescript
// PUBLIC ROUTE: Webhook endpoint - validates signature instead of auth
export async function POST(req: NextRequest) {
  // Validate webhook signature
  const signature = req.headers.get('x-webhook-signature');
  if (!validateWebhookSignature(signature, await req.text())) {
    return new NextResponse('Invalid signature', { status: 401 });
  }
  
  try {
    // Process webhook
  } catch (error) {
    return handleSDKError(error);
  }
}
```

## Common Mistakes

### ❌ Creating SDK Clients Incorrectly

```typescript
// WRONG - Creates new client instance
const client = new FetchConduitAdminClient({
  apiKey: process.env.CONDUIT_API_KEY
});

// CORRECT - Uses singleton with proper config
const adminClient = getServerAdminClient();
```

### ❌ Custom Error Handling

```typescript
// WRONG - Custom error response
catch (error) {
  return NextResponse.json(
    { error: 'Something went wrong' },
    { status: 500 }
  );
}

// CORRECT - Standard error handler
catch (error) {
  return handleSDKError(error);
}
```

### ❌ Missing Authentication

```typescript
// WRONG - No auth check
export async function GET(req: NextRequest) {
  const adminClient = getServerAdminClient();
  return NextResponse.json(await adminClient.virtualKeys.getAll());
}

// CORRECT - Includes auth check
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }
  
  const adminClient = getServerAdminClient();
  return NextResponse.json(await adminClient.virtualKeys.getAll());
}
```

### ❌ Accessing Environment Variables

```typescript
// WRONG - Direct env access
const apiUrl = process.env.CONDUIT_API_URL;

// CORRECT - Use SDK clients that handle configuration
const adminClient = getServerAdminClient();
```

### ❌ Inconsistent Response Formats

```typescript
// WRONG - Custom format
return NextResponse.json({
  success: true,
  data: virtualKeys
});

// CORRECT - Direct resource response
return NextResponse.json(virtualKeys);
```

## Testing Requirements

Every API route must be tested for:

### 1. Authentication
```typescript
// Test: Unauthenticated request returns 401
const response = await fetch('/api/virtualkeys');
expect(response.status).toBe(401);
```

### 2. Valid Requests
```typescript
// Test: Valid request returns expected data
const response = await fetch('/api/virtualkeys', {
  headers: { 'Authorization': 'Bearer valid-token' }
});
expect(response.status).toBe(200);
const data = await response.json();
expect(Array.isArray(data)).toBe(true);
```

### 3. Error Handling
```typescript
// Test: SDK errors are properly mapped
// Mock SDK to throw NotFoundError
const response = await fetch('/api/virtualkeys/999');
expect(response.status).toBe(404);
const error = await response.json();
expect(error.error).toBeDefined();
```

### 4. Input Validation
```typescript
// Test: Invalid input returns 400
const response = await fetch('/api/virtualkeys', {
  method: 'POST',
  body: JSON.stringify({ invalid: 'data' })
});
expect(response.status).toBe(400);
```

### 5. Method Not Allowed
```typescript
// Test: Unsupported method returns 405
const response = await fetch('/api/virtualkeys/123', {
  method: 'PATCH' // If only PUT is supported
});
expect(response.status).toBe(405);
```

## Route Categories

### Virtual Keys (`/api/virtualkeys/*`)
- Full CRUD operations
- Requires authentication
- Uses Admin SDK

### Providers (`/api/providers/*`)
- Full CRUD operations
- Requires authentication
- Uses Admin SDK
- Special health check endpoints

### Analytics (`/api/analytics/*`)
- Read-only operations
- Requires authentication
- May aggregate data from multiple sources

### Settings (`/api/settings/*`)
- Key-value operations
- Requires admin authentication
- Uses Admin SDK

### Model Mappings (`/api/model-mappings/*`)
- Full CRUD operations
- Requires authentication
- Uses Admin SDK
- Special discover/test endpoints

### Core Operations (`/api/chat/*`, `/api/images/*`, etc.)
- AI operations
- Requires authentication
- Uses Core SDK
- May support streaming

## Implementation Checklist

When implementing or updating a route:

- [ ] Uses `requireAuth()` or documents why public
- [ ] Gets SDK clients via helper functions
- [ ] Uses `handleSDKError()` for all errors
- [ ] No direct `process.env` access
- [ ] Follows naming conventions
- [ ] Has proper TypeScript types
- [ ] Returns appropriate status codes
- [ ] Handles all error cases
- [ ] Includes route documentation comment
- [ ] Has corresponding tests

## Migration Guide

When migrating existing routes to the standard:

1. **Audit Current Implementation**
   - Check authentication method
   - Identify SDK usage
   - Note custom error handling

2. **Update Authentication**
   ```typescript
   // Replace custom auth with:
   const auth = requireAuth(req);
   if (!auth.isValid) {
     return auth.response!;
   }
   ```

3. **Update SDK Usage**
   ```typescript
   // Replace custom client creation with:
   const adminClient = getServerAdminClient();
   ```

4. **Update Error Handling**
   ```typescript
   // Replace custom error responses with:
   catch (error) {
     return handleSDKError(error);
   }
   ```

5. **Test Thoroughly**
   - Verify authentication works
   - Test error scenarios
   - Ensure responses match expected format

## Conclusion

Following these standards ensures:
- Consistent behavior across all routes
- Proper security through standardized authentication
- Efficient SDK usage with singleton clients
- Predictable error handling and responses
- Easier maintenance and debugging
- Clear patterns for new developers

All new routes MUST follow this standard. Existing routes should be migrated as part of ongoing maintenance or when modified for other reasons.