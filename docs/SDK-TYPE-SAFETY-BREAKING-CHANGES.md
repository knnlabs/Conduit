# SDK Type Safety Breaking Changes Documentation

This document provides a comprehensive overview of all breaking changes introduced by the type safety improvements to the Conduit Node.js SDKs (Admin and Core), as part of the [SDK Type Safety Epic #349](https://github.com/knnlabs/Conduit/issues/349).

## Overview

The type safety improvements represent a **major version change** from Axios-based SDKs with `any`/`unknown` types to fetch-based SDKs with full TypeScript type safety generated from OpenAPI specifications.

**Version Information:**
- **Admin SDK**: `1.0.1` → `2.0.0` (Major version bump required)
- **Core SDK**: `0.2.0` → `1.0.0` (Major version bump required)

## Summary of Changes

### Key Improvements
- ✅ **Complete Type Safety**: All `any`/`unknown` types replaced with OpenAPI-generated types
- ✅ **Native Fetch**: Removed Axios dependency, using native fetch API
- ✅ **Reduced Bundle Size**: Admin ~47KB reduction, Core ~36KB reduction
- ✅ **Better Error Handling**: Typed error classes with specific error types
- ✅ **Enhanced Configuration**: More flexible client configuration options

### Major Removals
- ❌ **React Query Support**: All React hooks and query utilities removed
- ❌ **Axios HTTP Client**: Complete migration to native fetch
- ❌ **Runtime Validation**: Zod validation layers simplified
- ❌ **Some Services**: DatabaseBackupService, partial DiscoveryService removal

---

## Breaking Changes by Category

### 1. Removed Dependencies and Functionality

#### React Query Removal
All React Query functionality has been removed to focus on server-side usage:

```typescript
// ❌ REMOVED - No longer available
import { 
  useVirtualKeys,
  useChatCompletion,
  ConduitAdminProvider 
} from '@knn_labs/conduit-admin-client';

// ✅ MIGRATION - Use direct service calls
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
const client = new ConduitAdminClient(config);
const keys = await client.virtualKeys.list();
```

#### Axios HTTP Client Removal
The Axios-based HTTP client has been completely removed:

```typescript
// ❌ REMOVED - Axios-specific configurations
const client = new ConduitCoreClient({
  apiKey: 'key',
  axios: {
    timeout: 5000,
    interceptors: { /* ... */ }
  }
});

// ✅ MIGRATION - Native fetch configuration
const client = new ConduitCoreClient({
  apiKey: 'key',
  timeout: 5000,
  onRequest: (config) => { /* custom logic */ },
  onResponse: (response) => { /* custom logic */ }
});
```

#### Removed Services

**DatabaseBackupService (Admin SDK)**
```typescript
// ❌ REMOVED
import { DatabaseBackupService } from '@knn_labs/conduit-admin-client';

// ✅ MIGRATION - Use Admin API directly
const response = await fetch('/api/admin/database/backup', {
  method: 'POST',
  headers: { 'Authorization': `Bearer ${masterKey}` }
});
```

**ConnectionService and HealthService (Core SDK)**
```typescript
// ❌ REMOVED
import { ConnectionService, HealthService } from '@knn_labs/conduit-core-client';

// ✅ MIGRATION - Functionality moved to main client
const client = new ConduitCoreClient(config);
const health = await client.getHealth(); // Built into main client
```

### 2. Changed Class Names and Exports

#### Main Client Classes
Both SDKs have new fetch-based implementations:

```typescript
// ❌ OLD (presumed previous structure)
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

// ✅ NEW (backwards compatible aliases exist)
import { 
  ConduitAdminClient,     // Alias to FetchConduitAdminClient
  FetchConduitAdminClient // Direct fetch implementation
} from '@knn_labs/conduit-admin-client';

import { 
  ConduitCoreClient,      // Alias to FetchConduitCoreClient
  FetchConduitCoreClient  // Direct fetch implementation
} from '@knn_labs/conduit-core-client';
```

### 3. Changed Method Signatures

#### Client Configuration

**Admin SDK Configuration Changes:**
```typescript
// ❌ OLD (presumed structure)
interface OldAdminConfig {
  masterKey: string;
  adminApiUrl: string;
  // axios options
}

// ✅ NEW
interface ConduitAdminConfig {
  masterKey: string;
  adminApiUrl: string;
  conduitApiUrl?: string;  // NEW - for core API access
  options?: {
    timeout?: number;
    retries?: number | RetryConfig;  // Enhanced retry config
    logger?: Logger;
    cache?: CacheProvider;
    headers?: Record<string, string>;
    validateStatus?: (status: number) => boolean;
    signalR?: SignalRConfig;         // Enhanced SignalR config
    retryDelay?: number[];
    onError?: (error: Error) => void;      // NEW - Error callback
    onRequest?: (config: RequestConfigInfo) => void | Promise<void>;  // NEW
    onResponse?: (response: ResponseInfo) => void | Promise<void>;    // NEW
  };
}
```

**Core SDK Configuration Changes:**
```typescript
// ✅ NEW
interface ClientConfig {
  apiKey: string;
  baseURL?: string;
  timeout?: number;
  maxRetries?: number;
  headers?: Record<string, string>;
  debug?: boolean;
  signalR?: SignalRConfig;
  retryDelay?: number[];
  onError?: (error: Error) => void;
  onRequest?: (config: RequestConfig) => void | Promise<void>;
  onResponse?: (response: ResponseInfo) => void | Promise<void>;
}
```

#### Enhanced Retry Configuration
```typescript
// ❌ OLD - Simple number
retries: 3

// ✅ NEW - Enhanced configuration
retries: {
  maxRetries: 3,
  retryDelay: 1000,
  retryCondition: (error) => error.statusCode >= 500
}
```

### 4. Error Handling Changes

#### New Error Class Structure
```typescript
// ✅ NEW - Enhanced error classes
class ConduitError extends Error {
  public statusCode: number;
  public code: string;
  public context?: Record<string, unknown>;
  
  // Core SDK specific
  public type?: string;
  public param?: string;
  
  // Admin SDK specific  
  public details?: unknown;
  public endpoint?: string;
  public method?: string;
}
```

#### New Error Types
```typescript
// ✅ NEW - Specific error types available
import {
  ValidationError,
  NotFoundError,
  TimeoutError,
  ServerError,
  AuthorizationError,
  ConflictError
} from '@knn_labs/conduit-core-client';
```

#### Type Guards for Error Handling
```typescript
// ✅ NEW - Type-safe error handling
try {
  await client.chat.create(request);
} catch (error) {
  if (client.isRateLimitError(error)) {
    console.log(`Rate limited. Retry after ${error.retryAfter}`);
  } else if (client.isAuthError(error)) {
    console.log(`Authentication failed: ${error.code}`);
  } else if (client.isValidationError(error)) {
    console.log(`Validation error: ${error.details}`);
  }
}
```

### 5. Type Changes That Affect Usage

#### Strict Type Safety
All API methods now use generated OpenAPI types:

```typescript
// ❌ OLD - Loose typing
const response = await client.virtualKeys.create({
  keyName: "test",
  maxBudget: "100"  // String accepted
} as any);

// ✅ NEW - Strict typing
const response = await client.virtualKeys.create({
  keyName: "test",
  maxBudget: 100,   // Must be number
  budgetDuration: "Daily"  // Enum value required
});
```

#### Generated Type Exports
```typescript
// ✅ NEW - Generated types available for direct use
import type { 
  components as AdminComponents,
  operations as AdminOperations 
} from '@knn_labs/conduit-admin-client/generated';

import type { 
  components as CoreComponents,
  operations as CoreOperations 
} from '@knn_labs/conduit-core-client/generated';

// Use specific types
type VirtualKey = AdminComponents['schemas']['VirtualKeyDto'];
type ChatRequest = CoreComponents['schemas']['ChatCompletionRequest'];
```

### 6. Request Options Changes

#### New RequestOptions Interface
```typescript
// ✅ NEW - Enhanced request options
interface RequestOptions {
  signal?: AbortSignal;           // Cancellation support
  headers?: Record<string, string>;
  timeout?: number;
  correlationId?: string;         // Request tracking
  responseType?: 'json' | 'text' | 'arraybuffer' | 'blob';
}

// Usage
const response = await client.chat.create(request, {
  timeout: 30000,
  correlationId: 'my-request-123',
  signal: abortController.signal
});
```

### 7. SignalR Configuration Changes

#### Enhanced SignalR Configuration
```typescript
// ✅ NEW - More configuration options
interface SignalRConfig {
  enabled?: boolean;
  autoConnect?: boolean;
  reconnectDelay?: number[];      // Progressive backoff
  logLevel?: SignalRLogLevel;
  transport?: HttpTransportType;
  headers?: Record<string, string>;
  connectionTimeout?: number;
}
```

### 8. Package Dependency Changes

#### Admin SDK Dependencies
```json
{
  "dependencies": {
    "@microsoft/signalr": "^8.0.7",
    "zod": "^3.22.0"
  },
  "peerDependencies": {
    "next": ">=13.0.0"
  }
}
```

#### Core SDK Dependencies  
```json
{
  "dependencies": {
    "@microsoft/signalr": "^8.0.7", 
    "zod": "^4.0.5"
  }
}
```

**Removed Dependencies:**
- `axios` - Replaced with native fetch
- `@tanstack/react-query` - React hooks removed
- Any React-related dependencies

---

## Migration Impact Assessment

### High Impact Changes (Require Code Changes)
1. **React Query Usage** - All hook usage must be replaced
2. **Axios Configuration** - Custom axios setups must be migrated
3. **Error Handling** - Error catching patterns need updates
4. **Type Annotations** - Stricter typing may reveal existing issues

### Medium Impact Changes (May Require Changes)
1. **Client Configuration** - Enhanced options available
2. **Service Method Calls** - Return types are more specific
3. **Import Statements** - Some services removed/renamed

### Low Impact Changes (Backwards Compatible)
1. **Main Client Classes** - Aliases maintain compatibility
2. **Core Service Methods** - Most method signatures unchanged
3. **Basic Usage Patterns** - Simple use cases still work

---

## Next Steps

1. **Review Migration Guide** - See [SDK-TYPE-SAFETY-MIGRATION-GUIDE.md](./SDK-TYPE-SAFETY-MIGRATION-GUIDE.md)
2. **Update Dependencies** - Upgrade to latest SDK versions
3. **Run Type Checking** - Identify areas needing updates
4. **Test Thoroughly** - Verify all functionality works as expected
5. **Update Error Handling** - Take advantage of improved error types

## Support

For migration assistance:
- Review the [Migration Guide](./SDK-TYPE-SAFETY-MIGRATION-GUIDE.md)
- Check [examples directory](../Clients/Node/*/examples/) for updated patterns
- Create issues at [GitHub Issues](https://github.com/knnlabs/Conduit/issues)