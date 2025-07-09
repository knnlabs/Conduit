# WebUI SDK Migration Guide

## Overview

This guide helps developers understand the migration from API routes to direct SDK hook usage in the Conduit WebUI.

## Architecture Changes

### Before: Proxy-Based Architecture
```
Browser → Custom Hooks → API Routes → SDK Client → Conduit API
```

### After: Direct SDK Architecture
```
Browser → SDK React Query Hooks → Conduit API
```

## Migration Examples

### 1. Provider Management

#### Before (API Route Approach)
```typescript
// hooks/api/useAdminApi.ts
export function useProviders() {
  return useQuery({
    queryKey: ['providers'],
    queryFn: async () => {
      const response = await fetch('/api/admin/providers');
      return response.json();
    }
  });
}

// components/ProvidersPage.tsx
import { useProviders } from '@/hooks/api/useAdminApi';

function ProvidersPage() {
  const { data: providers, isLoading } = useProviders();
  // ...
}
```

#### After (Direct SDK Approach)
```typescript
// components/ProvidersPage.tsx
import { useProviders } from '@knn_labs/conduit-admin-client/react-query';

function ProvidersPage() {
  const { data: providers, isLoading } = useProviders();
  // ...
}
```

### 2. Chat Completions

#### Before
```typescript
// api/core/chat/completions/route.ts
export async function POST(request: Request) {
  const { messages } = await request.json();
  const coreClient = getCoreClient();
  const response = await coreClient.chat.complete({ messages });
  return NextResponse.json(response);
}

// hooks/api/useCoreApi.ts
export function useChatCompletion() {
  return useMutation({
    mutationFn: async (messages) => {
      const response = await fetch('/api/core/chat/completions', {
        method: 'POST',
        body: JSON.stringify({ messages })
      });
      return response.json();
    }
  });
}
```

#### After
```typescript
// components/ChatComponent.tsx
import { useChatCompletion } from '@knn_labs/conduit-core-client/react-query';

function ChatComponent() {
  const { mutate: sendMessage } = useChatCompletion();
  
  const handleSend = (messages) => {
    sendMessage({ messages });
  };
}
```

### 3. Virtual Key Operations

#### Before
```typescript
// Custom hook with error handling
export function useCreateVirtualKey() {
  return useMutation({
    mutationFn: async (data) => {
      const response = await fetch('/api/admin/virtual-keys', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
      });
      if (!response.ok) throw new Error('Failed to create key');
      return response.json();
    },
    onError: (error) => {
      toast.error(error.message);
    }
  });
}
```

#### After
```typescript
// Direct SDK usage with built-in error handling
import { useCreateVirtualKey } from '@knn_labs/conduit-admin-client/react-query';

function CreateKeyModal() {
  const { mutate: createKey } = useCreateVirtualKey({
    onError: (error) => {
      toast.error(error.message);
    }
  });
  
  const handleCreate = (data) => {
    createKey(data);
  };
}
```

## Provider Setup

### 1. Install SDK Packages
```bash
npm install @knn_labs/conduit-core-client @knn_labs/conduit-admin-client
```

### 2. Configure Providers
```typescript
// lib/providers/ConduitProviders.tsx
import { ConduitProvider } from '@knn_labs/conduit-core-client/react-query';
import { ConduitAdminProvider } from '@knn_labs/conduit-admin-client/react-query';
import { useAuthStore } from '@/stores/useAuthStore';

export function ConduitProviders({ children }: { children: React.ReactNode }) {
  const virtualKey = useAuthStore((state) => state.virtualKey);
  const queryClient = useQueryClient();
  
  const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL!;
  const adminApiUrl = process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL!;
  const masterKey = process.env.CONDUIT_MASTER_KEY!;

  return (
    <ConduitProvider
      virtualKey={virtualKey}
      baseUrl={coreApiUrl}
      queryClient={queryClient}
    >
      <ConduitAdminProvider
        authKey={masterKey}
        baseUrl={adminApiUrl}
        queryClient={queryClient}
      >
        {children}
      </ConduitAdminProvider>
    </ConduitProvider>
  );
}
```

### 3. Wrap Your App
```typescript
// app/layout.tsx
import { ConduitProviders } from '@/lib/providers/ConduitProviders';

export default function RootLayout({ children }) {
  return (
    <html>
      <body>
        <QueryProvider>
          <AuthProvider>
            <ConduitProviders>
              {children}
            </ConduitProviders>
          </AuthProvider>
        </QueryProvider>
      </body>
    </html>
  );
}
```

## Common Patterns

### 1. Loading States
```typescript
const { data, isLoading, error } = useProviders();

if (isLoading) return <Skeleton />;
if (error) return <ErrorMessage error={error} />;
return <ProviderList providers={data} />;
```

### 2. Optimistic Updates
```typescript
const queryClient = useQueryClient();
const { mutate: updateProvider } = useUpdateProvider({
  onMutate: async (newData) => {
    // Cancel in-flight queries
    await queryClient.cancelQueries({ queryKey: ['providers'] });
    
    // Snapshot previous value
    const previousProviders = queryClient.getQueryData(['providers']);
    
    // Optimistically update
    queryClient.setQueryData(['providers'], (old) => {
      return old.map(p => p.id === newData.id ? newData : p);
    });
    
    return { previousProviders };
  },
  onError: (err, newData, context) => {
    // Rollback on error
    queryClient.setQueryData(['providers'], context.previousProviders);
  },
  onSettled: () => {
    // Always refetch after error or success
    queryClient.invalidateQueries({ queryKey: ['providers'] });
  }
});
```

### 3. Type Safety
```typescript
// SDK provides full TypeScript types
import type { Provider, CreateProviderRequest } from '@knn_labs/conduit-admin-client';

const { data } = useProviders(); // data is Provider[]
const { mutate } = useCreateProvider(); // mutate expects CreateProviderRequest
```

## Removing Old Code

### 1. Delete API Routes
```bash
# Remove all proxy API routes
rm -rf src/app/api/core/
rm -rf src/app/api/admin/
# Keep only auth routes
```

### 2. Remove Custom Hooks
```bash
# Remove old hook files
rm src/hooks/api/useCoreApi.ts
rm src/hooks/api/useAdminApi.ts
```

### 3. Update Imports
```typescript
// Find and replace all imports
// Old: import { useProviders } from '@/hooks/api/useAdminApi';
// New: import { useProviders } from '@knn_labs/conduit-admin-client/react-query';
```

## Testing Migration

### 1. Verify SDK Hooks
```typescript
// Test component
function TestSDKHooks() {
  const { data: providers } = useProviders();
  const { data: models } = useModels();
  
  console.log('Providers:', providers);
  console.log('Models:', models);
  
  return <pre>{JSON.stringify({ providers, models }, null, 2)}</pre>;
}
```

### 2. Check Network Tab
- Open browser DevTools
- Go to Network tab
- Verify API calls go directly to Conduit API
- No calls should go to `/api/` routes (except auth)

### 3. Test Error Handling
```typescript
// Force an error to test handling
const { mutate } = useCreateProvider({
  onError: (error) => {
    console.error('SDK Error:', error);
    // Should see proper error from SDK
  }
});

// Try invalid data
mutate({ invalid: 'data' });
```

## Rollback Plan

If you need to rollback:

1. Keep old API routes during migration
2. Use feature flags to toggle between implementations
3. Gradually migrate one feature at a time

```typescript
// Feature flag approach
const USE_SDK_HOOKS = process.env.NEXT_PUBLIC_USE_SDK_HOOKS === 'true';

export function useProviders() {
  if (USE_SDK_HOOKS) {
    return useSDKProviders(); // From SDK
  } else {
    return useAPIProviders(); // Old implementation
  }
}
```

## Performance Improvements

### Before
- API Route adds 50-100ms latency
- Additional server processing
- Extra network hop

### After
- Direct API calls
- Better caching with React Query
- Reduced server load

## Next Steps

1. Start with read operations (queries)
2. Move to write operations (mutations)
3. Remove old code once stable
4. Update documentation
5. Train team on new patterns