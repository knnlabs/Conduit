# Conduit WebAdmin API Patterns & Best Practices

## Overview

This guide documents the **actual implementation patterns** used in the Conduit WebAdmin (Next.js). The WebAdmin uses a minimal API architecture where most business logic happens client-side using SDK clients with ephemeral authentication keys.

**Last Updated**: 2025-11-08
**Status**: ✅ Accurate and Current

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [The Three API Routes](#the-three-api-routes)
3. [Authentication Pattern](#authentication-pattern)
4. [SDK Client Usage](#sdk-client-usage)
5. [Error Handling](#error-handling)
6. [Ephemeral Key Strategy](#ephemeral-key-strategy)
7. [Route Implementation Patterns](#route-implementation-patterns)
8. [Environment Configuration](#environment-configuration)
9. [Best Practices](#best-practices)
10. [Common Patterns](#common-patterns)

---

## Architecture Overview

### Design Philosophy

The WebAdmin follows a **minimal server-side API** approach:

- **Only 3 server-side API routes** - All for authentication/key generation
- **Client-side SDK usage** - Browser communicates directly with Core/Admin APIs
- **Ephemeral key authentication** - Short-lived keys for secure client-side access
- **Simple, direct patterns** - No complex wrappers or middleware layers

### Request Flow

```
┌─────────────┐
│   Browser   │
└──────┬──────┘
       │ 1. Request ephemeral key
       ▼
┌─────────────────┐
│  WebAdmin API      │ ── Clerk Auth ──┐
│  (3 routes)     │                  │
└────────┬────────┘                  │
         │ 2. Returns key + API URL  │
         ▼                            ▼
┌─────────────────┐            ┌──────────┐
│  Browser with   │            │  Clerk   │
│  SDK + Temp Key │            │Middleware│
└────────┬────────┘            └──────────┘
         │
         │ 3. Direct API calls with ephemeral key
         ▼
┌─────────────────┐
│ Core/Admin API  │
└─────────────────┘
```

---

## The Three API Routes

The WebAdmin has exactly **three API routes**:

### 1. `/api/health` - Health Check

**Purpose**: Health status endpoint
**Auth**: None required
**Method**: GET

```typescript
// src/app/api/health/route.ts
export async function GET() {
  try {
    return NextResponse.json({
      status: 'healthy',
      timestamp: new Date().toISOString(),
      uptime: process.uptime(),
      memory: process.memoryUsage().rss
    });
  } catch {
    return NextResponse.json(
      { status: 'unhealthy', timestamp: new Date().toISOString() },
      { status: 500 }
    );
  }
}
```

### 2. `/api/auth/ephemeral-key` - Core API Access

**Purpose**: Generate ephemeral keys for Core API access
**Auth**: Clerk (via middleware)
**Method**: POST

```typescript
// src/app/api/auth/ephemeral-key/route.ts
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as EphemeralKeyRequest;

    // Get WebAdmin's virtual key
    const adminClient = getServerAdminClient();
    const webadminVirtualKey = await adminClient.system.getWebAdminVirtualKey();

    // Extract request metadata
    const sourceIP = request.headers.get('x-forwarded-for') ??
                     request.headers.get('x-real-ip') ??
                     'unknown';
    const userAgent = request.headers.get('user-agent') ?? 'unknown';

    // Generate ephemeral key via Core SDK
    const coreClient = await getServerCoreClient();
    const response = await coreClient.auth.generateEphemeralKey(webadminVirtualKey, {
      metadata: {
        sourceIP,
        userAgent,
        purpose: body.purpose ?? 'web-ui-request'
      }
    });

    return NextResponse.json({
      ...response,
      coreApiUrl: process.env.CONDUIT_API_EXTERNAL_URL ?? 'http://localhost:5000',
    });
  } catch (error) {
    console.error('Error generating ephemeral key:', error);
    return handleSDKError(error);
  }
}
```

### 3. `/api/auth/ephemeral-master-key` - Admin API Access

**Purpose**: Generate ephemeral master keys for Admin API access
**Auth**: Clerk (via middleware)
**Method**: POST

```typescript
// src/app/api/auth/ephemeral-master-key/route.ts
export async function POST(request: NextRequest) {
  try {
    const masterKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;
    if (!masterKey) {
      return NextResponse.json(
        { error: 'Master key not configured' },
        { status: 500 }
      );
    }

    const isDevelopment = process.env.CLERK_AUTH_ENABLED !== 'true';

    if (isDevelopment) {
      // Development: return master key directly
      return NextResponse.json({
        ephemeralMasterKey: masterKey,
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
        expiresInSeconds: 3600,
        adminApiUrl: process.env.CONDUIT_ADMIN_API_EXTERNAL_URL ?? 'http://localhost:5002'
      });
    }

    // Production: call Admin API to generate ephemeral key
    const adminApiUrl = process.env.CONDUIT_ADMIN_API_BASE_URL ?? 'http://admin-api:5002';
    const response = await fetch(`${adminApiUrl}/api/admin/auth/ephemeral-master-key`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Master-Key': masterKey,
      },
      body: JSON.stringify({
        metadata: {
          sourceIP: request.headers.get('x-forwarded-for') ?? 'unknown',
          userAgent: request.headers.get('user-agent') ?? 'unknown',
          purpose: 'web-ui-request'
        }
      })
    });

    if (!response.ok) {
      throw new Error(`Failed to generate ephemeral master key: ${response.status}`);
    }

    const data = await response.json();
    return NextResponse.json({
      ...data,
      adminApiUrl: process.env.CONDUIT_ADMIN_API_EXTERNAL_URL ?? 'http://localhost:5002'
    });
  } catch (error) {
    console.error('Error generating ephemeral master key:', error);
    return handleSDKError(error);
  }
}
```

---

## Authentication Pattern

### Clerk Middleware

Authentication is handled at the **request level** via Clerk middleware, not per-route.

**File**: `src/middleware.ts`

```typescript
import { clerkMiddleware, createRouteMatcher } from '@clerk/nextjs/server';
import { NextResponse } from 'next/server';

// Public routes that don't require authentication
const isPublicRoute = createRouteMatcher([
  '/access-denied',
  '/api/health',  // Add public routes here
]);

export default clerkMiddleware(async (auth, req) => {
  // Skip all auth in development when explicitly disabled
  if (process.env.CLERK_AUTH_ENABLED !== 'true' && process.env.NODE_ENV === 'development') {
    return NextResponse.next();
  }

  if (!isPublicRoute(req)) {
    const { userId, sessionClaims, redirectToSignIn } = await auth();

    // If not authenticated, redirect to sign-in
    if (!userId) {
      return redirectToSignIn();
    }

    // Check if user has admin access
    const metadata = sessionClaims?.metadata as { siteadmin?: boolean } | undefined;
    const isAdmin = metadata?.siteadmin === true;

    // If not admin, redirect to access-denied
    if (!isAdmin) {
      return NextResponse.redirect(new URL('/access-denied', req.url));
    }
  }
});
```

### Key Points

- ✅ **No per-route authentication** - Middleware handles it globally
- ✅ **Public routes** defined in `isPublicRoute` matcher
- ✅ **Development mode** can skip auth when `CLERK_AUTH_ENABLED !== 'true'`
- ✅ **Admin-only access** - Only users with `siteadmin` metadata can access
- ✅ **Route handlers assume authenticated** - If middleware lets request through, user is authenticated

---

## SDK Client Usage

### Server-Side SDK Clients

**File**: `src/lib/server/sdk-config.ts`

#### Admin Client (Synchronous)

```typescript
import { getServerAdminClient } from '@/lib/server/sdk-config';

export async function GET() {
  try {
    const adminClient = getServerAdminClient();
    const providers = await adminClient.providers.list();
    return NextResponse.json(providers);
  } catch (error) {
    return handleSDKError(error);
  }
}
```

#### Core Client (Asynchronous)

```typescript
import { getServerCoreClient } from '@/lib/server/sdk-config';

export async function POST(request: NextRequest) {
  try {
    const coreClient = await getServerCoreClient(); // Note: async!
    const response = await coreClient.chat.create({ /* ... */ });
    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}
```

### Important Notes

- `getServerAdminClient()` is **synchronous** - returns client directly
- `getServerCoreClient()` is **async** - must await
- Both are **singletons** - don't create new clients manually
- **Never expose** master keys to client

---

## Error Handling

### The `handleSDKError()` Function

**File**: `src/lib/errors/sdk-errors.ts`

All SDK errors should use the centralized error handler:

```typescript
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function POST(request: NextRequest) {
  try {
    const client = getServerAdminClient();
    const result = await client.someOperation();
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error); // Automatically maps to correct HTTP status
  }
}
```

### Error Mapping

| SDK Error Type | HTTP Status | Response |
|---|---|---|
| ValidationError | 400 | `{ error: message }` |
| AuthError | 401 | `{ error: message }` |
| NotFoundError | 404 | `{ error: message }` |
| ConflictError | 409 | `{ error: message }` |
| RateLimitError | 429 | `{ error: message }` |
| ServerError | 500 | `{ error: message }` |
| Network errors (ECONNREFUSED) | 503 | Service unavailable |
| Timeout errors (ETIMEDOUT) | 504 | Gateway timeout |
| Unknown | 500 | Internal server error |

### Error Utilities

```typescript
import {
  getErrorMessage,
  getErrorStatusCode,
  isHttpError,
  getCombinedErrorDetails
} from '@/lib/utils/error-utils';

// Extract status code
const status = getErrorStatusCode(error) ?? 500;

// Get error message safely
const message = getErrorMessage(error);

// Check if it's an HTTP error
if (isHttpError(error)) {
  // Handle HTTP-specific error
}

// Get full error details
const details = getCombinedErrorDetails(error);
```

---

## Ephemeral Key Strategy

### Why Ephemeral Keys?

1. **Security** - Short-lived, single-purpose keys minimize exposure
2. **Direct API access** - Browser can call Core/Admin APIs directly
3. **Scalability** - No need to proxy all API traffic through WebAdmin
4. **Simplicity** - Clean separation between auth and business logic

### Ephemeral Key Flow

```
1. Browser needs to call Core API
2. Request ephemeral key from /api/auth/ephemeral-key
3. WebAdmin validates user via Clerk
4. WebAdmin generates ephemeral key using its virtual key
5. Returns ephemeral key + Core API URL to browser
6. Browser stores key in memory (with expiration)
7. Browser makes direct calls to Core API with ephemeral key
8. When key expires or gets 401, browser requests new key
```

### Client-Side Implementation

**File**: `src/lib/client/ephemeralKeyClient.ts`

```typescript
import { ephemeralKeyClient } from '@/lib/client/ephemeralKeyClient';

// Get a valid ephemeral key (from cache or refreshed)
const { key, coreApiUrl } = await ephemeralKeyClient.getKey();

// Make a direct request to Core API
const response = await ephemeralKeyClient.makeDirectRequest('/api/chat/completions', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ /* request */ }),
});
```

### Key Caching Strategy

- Keys are cached in memory with expiration timestamp
- 30-second buffer before actual expiration for safety
- Automatically refresh when approaching expiration
- On 401 errors, clear cache and retry with fresh key
- Race condition protection: concurrent refresh requests share same promise

---

## Route Implementation Patterns

### Basic Pattern

```typescript
import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/sdk-config';

export async function GET(request: NextRequest) {
  try {
    const client = getServerAdminClient();
    const data = await client.someService.list();
    return NextResponse.json(data);
  } catch (error) {
    return handleSDKError(error);
  }
}
```

### With Request Body

```typescript
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as MyRequestType;

    // Validate input
    if (!body.name?.trim()) {
      return NextResponse.json(
        { error: 'Name is required' },
        { status: 400 }
      );
    }

    const client = getServerAdminClient();
    const result = await client.resources.create(body);
    return NextResponse.json(result, { status: 201 });
  } catch (error) {
    return handleSDKError(error);
  }
}
```

### With Query Parameters

```typescript
export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') ?? '1');
    const limit = parseInt(searchParams.get('limit') ?? '20');

    const client = getServerAdminClient();
    const data = await client.resources.list({ page, limit });
    return NextResponse.json(data);
  } catch (error) {
    return handleSDKError(error);
  }
}
```

### Dynamic Route with Parameters

```typescript
// app/api/resources/[id]/route.ts
export async function GET(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    const client = getServerAdminClient();
    const resource = await client.resources.get(params.id);
    return NextResponse.json(resource);
  } catch (error) {
    return handleSDKError(error);
  }
}
```

---

## Environment Configuration

### Required Variables

```bash
# Backend Authentication (Required)
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-master-key-here

# API Endpoints (Internal - Docker service names)
CONDUIT_ADMIN_API_BASE_URL=http://admin-api:5002
CONDUIT_API_BASE_URL=http://core-api:5000

# API Endpoints (External - Browser-accessible URLs)
CONDUIT_ADMIN_API_EXTERNAL_URL=http://localhost:5002
CONDUIT_API_EXTERNAL_URL=http://localhost:5000

# Clerk Authentication (Production)
CLERK_AUTH_ENABLED=true  # Set to false for development without auth
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_...
CLERK_SECRET_KEY=sk_test_...
```

### Development vs Production

```typescript
const isDevelopment = process.env.CLERK_AUTH_ENABLED !== 'true';

if (isDevelopment) {
  // Development mode behavior
  // - Master key used directly
  // - Simplified authentication
  // - More verbose logging
} else {
  // Production mode behavior
  // - Ephemeral keys generated via API
  // - Full Clerk authentication
  // - Minimal logging
}
```

---

## Best Practices

### ✅ DO

1. **Use singleton getters** for SDK clients
   ```typescript
   const adminClient = getServerAdminClient();
   const coreClient = await getServerCoreClient(); // async!
   ```

2. **Wrap all operations in try/catch**
   ```typescript
   try {
     // ... operation
   } catch (error) {
     return handleSDKError(error);
   }
   ```

3. **Validate input before SDK calls**
   ```typescript
   if (!body.name?.trim()) {
     return NextResponse.json({ error: 'Name required' }, { status: 400 });
   }
   ```

4. **Use appropriate HTTP status codes**
   ```typescript
   return NextResponse.json(created, { status: 201 }); // Created
   return new Response(null, { status: 204 }); // No content
   ```

5. **Return NextResponse.json() directly**
   ```typescript
   return NextResponse.json(data); // Simple, direct
   ```

### ❌ DON'T

1. **Don't create SDK clients manually**
   ```typescript
   // ❌ Bad
   const client = new ConduitAdminClient({ ... });

   // ✅ Good
   const client = getServerAdminClient();
   ```

2. **Don't forget to await async SDK client**
   ```typescript
   // ❌ Bad
   const client = getServerCoreClient(); // Missing await!

   // ✅ Good
   const client = await getServerCoreClient();
   ```

3. **Don't add per-route authentication checks**
   ```typescript
   // ❌ Bad - middleware already handles this
   export async function GET(request: NextRequest) {
     const auth = await checkAuth(request);
     // ...
   }

   // ✅ Good - trust middleware
   export async function GET(request: NextRequest) {
     // User is already authenticated
   }
   ```

4. **Don't expose master keys to client**
   ```typescript
   // ❌ NEVER do this
   return NextResponse.json({
     masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY
   });

   // ✅ Good - use ephemeral keys
   const ephemeralKey = await coreClient.auth.generateEphemeralKey(...);
   return NextResponse.json({ ephemeralKey: ephemeralKey.key });
   ```

---

## Common Patterns

### Response Formats

```typescript
// Standard JSON response
return NextResponse.json(data);

// With status code
return NextResponse.json(created, { status: 201 });

// With custom headers
return NextResponse.json(data, {
  headers: {
    'Cache-Control': 'private, max-age=300',
  }
});

// Error response
return NextResponse.json({ error: 'Not found' }, { status: 404 });

// No content
return new Response(null, { status: 204 });
```

### Request Handling

```typescript
// JSON body
const body = await request.json() as MyType;

// Form data
const formData = await request.formData();
const file = formData.get('file') as File;

// Headers
const authHeader = request.headers.get('authorization');
const clientIP = request.headers.get('x-forwarded-for') ?? 'unknown';

// Query params
const { searchParams } = new URL(request.url);
const page = parseInt(searchParams.get('page') ?? '1');
```

---

## Summary

### Key Takeaways

1. **Only 3 API routes** - WebAdmin is not a traditional API backend
2. **Clerk handles authentication** - No custom auth in routes
3. **Use SDK clients** - `getServerAdminClient()` and `getServerCoreClient()`
4. **Simple error handling** - `handleSDKError()` for all SDK errors
5. **Ephemeral keys** - Secure client-side API access
6. **Direct patterns** - No complex wrappers or decorators

### Quick Reference

```typescript
// Standard route template
import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/sdk-config';

export async function GET(request: NextRequest) {
  try {
    const client = getServerAdminClient();
    const data = await client.someService.someMethod();
    return NextResponse.json(data);
  } catch (error) {
    return handleSDKError(error);
  }
}
```

### Related Documentation

- **[Architecture Documentation](../architecture/README.md)** - System architecture overview
- **[SDK Best Practices](../api-guides/sdk/best-practices.md)** - SDK usage patterns
- **[Next.js Integration](../api-guides/sdk/nextjs-integration.md)** - WebAdmin SDK integration
- **[Error Handling](webadmin/error-handling.md)** - WebAdmin error patterns

---

**Document Status**: ✅ Accurate and Current
**Last Validated**: 2025-11-08
**Actual Route Count**: 3
**Helper Functions**: 6 (all documented correctly)
