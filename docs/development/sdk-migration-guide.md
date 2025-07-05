# SDK Migration Guide for Conduit WebUI

This guide explains how to migrate from direct API calls to using the Conduit Node.js SDK clients.

## Phase 1 Implementation Summary

We've implemented the core infrastructure needed to use the SDK throughout the WebUI:

### 1. SDK Client Management (`src/lib/clients/server.ts`)
- **Environment validation** - Ensures all required variables are set
- **Configurable SDK options** - Timeout, retries, logging
- **Connection pooling** - Efficient client reuse
- **Health checks** - Monitor SDK connectivity
- **Browser client support** - SignalR-enabled clients for frontend

### 2. Error Handling (`src/lib/errors/sdk-errors.ts`)
- **Comprehensive error types** - Network, auth, validation, etc.
- **User-friendly messages** - No technical details exposed
- **Retry logic** - Automatic retry for transient failures
- **Consistent error responses** - Standardized error format

### 3. Response Transformations (`src/lib/utils/sdk-transforms.ts`)
- **Unified response format** - Consistent API responses
- **Pagination support** - Standard pagination metadata
- **Streaming responses** - For chat completions
- **Batch operations** - Handle bulk results
- **Security** - Sanitize sensitive data

### 4. Authentication (`src/lib/auth/sdk-auth.ts`)
- **Session validation** - Works with SDK requirements
- **Admin/Core client separation** - Proper client selection
- **Virtual key extraction** - Multiple sources supported
- **Middleware helper** - Simple route protection

## Migration Examples

### Basic Route Migration

#### Before (Direct Fetch):
```typescript
export async function GET(request: NextRequest) {
  const validation = await validateSession(request);
  if (!validation.isValid) {
    return createUnauthorizedResponse(validation.error);
  }

  const response = await fetch(`${adminApiUrl}/v1/providers`, {
    headers: { 'Authorization': `Bearer ${masterKey}` },
  });
  
  const providers = await response.json();
  return NextResponse.json(providers);
}
```

#### After (Using SDK):
```typescript
export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      const providers = await auth.adminClient!.providers.list();
      return transformSDKResponse(providers);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);
```

### Core API Migration (Chat Example)

```typescript
export const POST = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      
      // Create streaming response
      const stream = await auth.coreClient!.chat.createStream({
        model: body.model,
        messages: body.messages,
        temperature: body.temperature,
        maxTokens: body.maxTokens,
      });

      // Return SSE stream
      return createStreamingResponse(stream, {
        transformer: (chunk) => `data: ${JSON.stringify(chunk)}\n\n`
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireCore: true, requireVirtualKey: true }
);
```

### Pagination Example

```typescript
export const GET = withSDKAuth(
  async (request, { auth }) => {
    const url = new URL(request.url);
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');

    const result = await auth.adminClient!.requestLogs.list({
      page,
      pageSize,
      startDate: url.searchParams.get('startDate'),
      endDate: url.searchParams.get('endDate'),
    });

    const pagination = extractPagination(result);
    return transformPaginatedResponse(result.data, pagination!);
  },
  { requireAdmin: true }
);
```

### Error Handling with Context

```typescript
export const POST = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      
      // Wrap SDK calls with error context
      const provider = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.create(body),
        'create provider'
      );

      // Test connection if requested
      if (body.testConnection) {
        await withSDKErrorHandling(
          async () => auth.adminClient!.providers.testConnection(provider.id),
          'test provider connection'
        );
      }

      return transformSDKResponse(provider, { status: 201 });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);
```

## Configuration

### Environment Variables

```bash
# Required
CONDUIT_MASTER_KEY=your-master-key
NEXT_PUBLIC_CONDUIT_ADMIN_API_URL=http://localhost:5002
NEXT_PUBLIC_CONDUIT_CORE_API_URL=http://localhost:5000

# Optional SDK Configuration
CONDUIT_SDK_TIMEOUT=30000              # Request timeout in ms
CONDUIT_SDK_RETRIES=3                  # Number of retries
CONDUIT_SDK_RETRY_DELAY=1000          # Initial retry delay
CONDUIT_SDK_LOGGING=true              # Enable SDK debug logging
CONDUIT_SDK_LOG_LEVEL=warn            # Log level: debug, info, warn, error
CONDUIT_VALIDATE_SDK_AUTH=true        # Validate clients on creation
CONDUIT_TEST_VIRTUAL_KEY=vk_test_...  # Virtual key for health checks
```

### Client-Side Usage

For React components that need real-time features:

```typescript
import { createBrowserClient } from '@/lib/clients/browser';
import { useEffect, useState } from 'react';

export function ChatComponent({ virtualKey }: { virtualKey: string }) {
  const [client, setClient] = useState<ConduitCoreClient | null>(null);

  useEffect(() => {
    const coreClient = createBrowserClient({
      virtualKey,
      onConnectionChange: (connected) => {
        console.log('SignalR connection:', connected);
      },
    });

    // Subscribe to real-time events
    coreClient.signalR.tasks.onProgress((progress) => {
      console.log('Task progress:', progress);
    });

    setClient(coreClient);

    return () => {
      coreClient.signalR.disconnect();
    };
  }, [virtualKey]);

  // Use client for API calls
  const sendMessage = async (message: string) => {
    const response = await client!.chat.create({
      model: 'gpt-4',
      messages: [{ role: 'user', content: message }],
    });
    return response;
  };
}
```

## Best Practices

1. **Always use error handling utilities**
   ```typescript
   const result = await withSDKErrorHandling(
     async () => client.someOperation(),
     'operation context'
   );
   ```

2. **Transform responses consistently**
   ```typescript
   return transformSDKResponse(data, { 
     status: 201,
     meta: { created: true }
   });
   ```

3. **Use appropriate auth validation**
   ```typescript
   // Admin operations
   { requireAdmin: true }
   
   // Core operations with virtual key
   { requireCore: true, requireVirtualKey: true }
   
   // Basic authenticated routes
   { requireAdmin: false, requireCore: false }
   ```

4. **Handle streaming responses**
   ```typescript
   const stream = await client.chat.createStream(params);
   return createStreamingResponse(stream);
   ```

5. **Sanitize sensitive data**
   ```typescript
   const sanitized = sanitizeResponse(data, ['apiKey', 'secret']);
   return transformSDKResponse(sanitized);
   ```

## Next Steps

1. **Phase 2**: Migrate all Admin API routes
2. **Phase 3**: Migrate all Core API routes  
3. **Phase 4**: Integrate SignalR from SDK
4. **Phase 5**: Add comprehensive testing

## Troubleshooting

### Common Issues

1. **Client initialization fails**
   - Check environment variables
   - Verify API connectivity
   - Review SDK debug logs

2. **Authentication errors**
   - Ensure session is valid
   - Check master key configuration
   - Verify virtual key permissions

3. **Timeout errors**
   - Increase `CONDUIT_SDK_TIMEOUT`
   - Check network latency
   - Consider retry configuration

4. **SignalR connection issues**
   - Verify WebSocket support
   - Check CORS configuration
   - Review firewall rules

### Debug Mode

Enable detailed logging:
```bash
CONDUIT_SDK_LOGGING=true
CONDUIT_SDK_LOG_LEVEL=debug
```

Monitor SDK operations in the console for troubleshooting.