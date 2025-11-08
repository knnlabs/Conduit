# SDK Connection Health Check Feature

## Overview

This document describes the lightweight health check functionality added to both Core and Admin SDKs to enable connection monitoring without authentication.

## Background

Previously, the WebUI had one remaining direct API call for connection monitoring in `useConnectionStore.ts`. To achieve 100% SDK usage, we've added lightweight ping methods to both SDKs.

## Implementation

### Core SDK (v0.2.2+)

Added to `ConnectionService`:

```typescript
// Lightweight health check without authentication
async ping(): Promise<boolean>

// Health check with custom timeout
async pingWithTimeout(timeoutMs: number): Promise<boolean>
```

Usage:
```typescript
const coreClient = new ConduitCoreClient({
  baseURL: 'https://api.example.com',
  apiKey: 'dummy-key', // Not used for ping
  timeout: 5000,
});

const isConnected = await coreClient.connection.ping();
// or with custom timeout
const isConnectedWithTimeout = await coreClient.connection.pingWithTimeout(3000);
```

### Admin SDK (v1.0.2+)

Added to `ConnectionService`:

```typescript
// Lightweight health check without authentication
async ping(): Promise<boolean>

// Health check with custom timeout
async pingWithTimeout(timeoutMs: number): Promise<boolean>
```

Usage:
```typescript
const adminClient = new ConduitAdminClient({
  adminApiUrl: 'https://admin.example.com',
  masterKey: 'dummy-key', // Not used for ping
  options: { timeout: 5000 }
});

const isConnected = await adminClient.connection.ping();
// or with custom timeout
const isConnectedWithTimeout = await adminClient.connection.pingWithTimeout(3000);
```

## API Details

### Endpoint
Both methods call the `/health/ready` endpoint without authentication headers.

### Returns
- `true` if the API responds with HTTP 200
- `false` for any error or non-200 status

### Timeout Behavior
- `ping()`: Uses default 5-second timeout
- `pingWithTimeout(ms)`: Uses custom timeout in milliseconds
- Throws error if timeout <= 0

## WebUI Integration

The `useConnectionStore` has been updated to use these SDK methods:

```typescript
// Before: Direct fetch call
const response = await fetch(`${url}/health/ready`, {
  signal: controller.signal,
  mode: 'cors',
  headers: { 'Accept': 'application/json' },
});

// After: SDK method
const isConnected = await client.connection.pingWithTimeout(5000);
```

### Backward Compatibility

The WebUI implementation includes a fallback for older SDK versions:

```typescript
if ('connection' in coreClient && coreClient.connection && 'pingWithTimeout' in coreClient.connection) {
  // Use new SDK method
  isConnected = await coreClient.connection.pingWithTimeout(5000);
} else {
  // Fallback to direct fetch for older SDKs
  // This code can be removed once SDK updates are published
}
```

## Benefits

1. **100% SDK Usage**: Eliminates the last direct API call in WebUI
2. **Consistent Error Handling**: SDK manages retries and errors
3. **Type Safety**: Full TypeScript support
4. **Easier Testing**: Can mock SDK methods
5. **Single Source of Truth**: All API interactions through SDK

## Migration Guide

1. Update SDK dependencies:
   ```json
   {
     "@knn_labs/conduit-core-client": "^0.2.2",
     "@knn_labs/conduit-admin-client": "^1.0.2"
   }
   ```

2. Remove fallback code from `useConnectionStore.ts` once SDKs are updated

3. Remove TODO comments about the direct API call

## Related Issues

- #249 - Add lightweight health check method to SDK for connection monitoring
- #242 - Epic: WebUI SDK Feature Integration
- #243 - Epic: Conduit SDK Feature Parity (Completed)