# Conduit SDK Quick Reference

A quick reference for common SDK patterns and usage in Next.js applications.

## Installation & Setup

```bash
# Admin functionality (server-side only)
npm install @conduit/admin-client

# Core functionality (client/server)
npm install @conduit/core-client

# React hooks
npm install @conduit/admin-client @conduit/core-client
```

```typescript
// lib/conduit.ts
import { createAdminClient } from '@conduit/admin-client';
import { createCoreClient } from '@conduit/core-client';

export const adminClient = createAdminClient({
  baseUrl: process.env.CONDUIT_API_URL!,
  apiKey: process.env.CONDUIT_WEBUI_AUTH_KEY!
});

export const coreClient = createCoreClient({
  baseUrl: process.env.NEXT_PUBLIC_CONDUIT_API_URL!
});
```

## Common Patterns

### List View with Loading & Error States

```typescript
'use client';

import { useVirtualKeys } from '@conduit/admin-client/react';

export function VirtualKeysList() {
  const { data, isLoading, error } = useVirtualKeys();
  
  if (isLoading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;
  if (!data?.length) return <div>No virtual keys found</div>;
  
  return (
    <ul>
      {data.map(key => (
        <li key={key.id}>{key.name}</li>
      ))}
    </ul>
  );
}
```

### Create with Form

```typescript
'use client';

import { useCreateVirtualKey } from '@conduit/admin-client/react';

export function CreateKeyForm() {
  const createKey = useCreateVirtualKey();
  
  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    
    try {
      await createKey.mutateAsync({
        name: formData.get('name') as string,
        providers: formData.getAll('providers') as string[]
      });
      // Success - redirect or show message
    } catch (error) {
      // Error handled by mutation
    }
  };
  
  return (
    <form onSubmit={handleSubmit}>
      <input name="name" required />
      <select name="providers" multiple>
        <option value="openai">OpenAI</option>
        <option value="anthropic">Anthropic</option>
      </select>
      <button type="submit" disabled={createKey.isPending}>
        {createKey.isPending ? 'Creating...' : 'Create'}
      </button>
      {createKey.error && <div>{createKey.error.message}</div>}
    </form>
  );
}
```

### Update with Optimistic UI

```typescript
'use client';

import { useUpdateVirtualKey, useQueryClient } from '@conduit/admin-client/react';

export function UpdateKeyButton({ keyId, newData }: Props) {
  const queryClient = useQueryClient();
  const updateKey = useUpdateVirtualKey();
  
  const handleUpdate = () => {
    updateKey.mutate(
      { id: keyId, data: newData },
      {
        onMutate: async (variables) => {
          // Cancel in-flight queries
          await queryClient.cancelQueries({ queryKey: ['virtualKeys'] });
          
          // Update cache optimistically
          const previous = queryClient.getQueryData(['virtualKeys']);
          queryClient.setQueryData(['virtualKeys'], (old: any) =>
            old?.map((key: any) => 
              key.id === variables.id ? { ...key, ...variables.data } : key
            )
          );
          
          return { previous };
        },
        onError: (err, variables, context) => {
          // Rollback on error
          queryClient.setQueryData(['virtualKeys'], context?.previous);
        },
        onSettled: () => {
          // Refetch to ensure consistency
          queryClient.invalidateQueries({ queryKey: ['virtualKeys'] });
        }
      }
    );
  };
  
  return <button onClick={handleUpdate}>Update</button>;
}
```

### Delete with Confirmation

```typescript
'use client';

import { useDeleteVirtualKey } from '@conduit/admin-client/react';

export function DeleteKeyButton({ keyId, keyName }: Props) {
  const deleteKey = useDeleteVirtualKey();
  
  const handleDelete = async () => {
    if (!confirm(`Delete virtual key "${keyName}"?`)) return;
    
    try {
      await deleteKey.mutateAsync(keyId);
      // Success - redirect or update UI
    } catch (error) {
      // Error handled by mutation
    }
  };
  
  return (
    <button 
      onClick={handleDelete}
      disabled={deleteKey.isPending}
    >
      {deleteKey.isPending ? 'Deleting...' : 'Delete'}
    </button>
  );
}
```

### Infinite Scroll

```typescript
'use client';

import { useInfiniteVirtualKeys } from '@conduit/admin-client/react';
import { useInView } from 'react-intersection-observer';

export function InfiniteKeysList() {
  const {
    data,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage
  } = useInfiniteVirtualKeys({ limit: 20 });
  
  const { ref } = useInView({
    onChange: (inView) => {
      if (inView && hasNextPage && !isFetchingNextPage) {
        fetchNextPage();
      }
    }
  });
  
  const keys = data?.pages.flatMap(page => page.items) ?? [];
  
  return (
    <div>
      {keys.map(key => (
        <div key={key.id}>{key.name}</div>
      ))}
      <div ref={ref}>
        {isFetchingNextPage && 'Loading more...'}
      </div>
    </div>
  );
}
```

### File Upload

```typescript
'use client';

import { useUploadMedia } from '@conduit/admin-client/react';

export function ImageUpload() {
  const uploadMedia = useUploadMedia();
  
  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    
    try {
      const result = await uploadMedia.mutateAsync({ file });
      console.log('Uploaded:', result.url);
    } catch (error) {
      // Error handled by mutation
    }
  };
  
  return (
    <div>
      <input
        type="file"
        accept="image/*"
        onChange={handleFileSelect}
        disabled={uploadMedia.isPending}
      />
      {uploadMedia.isPending && (
        <progress value={uploadMedia.progress} max={100} />
      )}
    </div>
  );
}
```

### Real-time Updates

```typescript
'use client';

import { useRealtimeConnection } from '@conduit/core-client/react';
import { useEffect, useState } from 'react';

export function RealtimeChat({ virtualKey }: Props) {
  const [messages, setMessages] = useState<Message[]>([]);
  const { connection, isConnected } = useRealtimeConnection({
    hub: 'chat',
    virtualKey
  });
  
  useEffect(() => {
    if (!connection) return;
    
    connection.on('NewMessage', (message: Message) => {
      setMessages(prev => [...prev, message]);
    });
    
    return () => {
      connection.off('NewMessage');
    };
  }, [connection]);
  
  return (
    <div>
      {!isConnected && <div>Connecting...</div>}
      {messages.map(msg => (
        <div key={msg.id}>{msg.content}</div>
      ))}
    </div>
  );
}
```

### Protected Route

```typescript
// app/admin/layout.tsx
import { requireAuth } from '@conduit/admin-client/nextjs';

export default requireAuth(function AdminLayout({
  children
}: {
  children: React.ReactNode
}) {
  return <div>{children}</div>;
});
```

### API Route Handler

```typescript
// app/api/admin/providers/route.ts
import { adminClient } from '@/lib/conduit';
import { NextResponse } from 'next/server';

export async function GET() {
  try {
    const providers = await adminClient.providers.list();
    return NextResponse.json(providers);
  } catch (error) {
    return NextResponse.json(
      { error: 'Failed to fetch providers' },
      { status: 500 }
    );
  }
}

export async function POST(request: Request) {
  try {
    const data = await request.json();
    const provider = await adminClient.providers.create(data);
    return NextResponse.json(provider, { status: 201 });
  } catch (error) {
    return NextResponse.json(
      { error: 'Failed to create provider' },
      { status: 500 }
    );
  }
}
```

### Error Boundary

```typescript
// app/providers.tsx
'use client';

import { QueryErrorResetBoundary } from '@tanstack/react-query';
import { ErrorBoundary } from 'react-error-boundary';

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <QueryErrorResetBoundary>
      {({ reset }) => (
        <ErrorBoundary
          onReset={reset}
          fallbackRender={({ error, resetErrorBoundary }) => (
            <div>
              <h2>Something went wrong:</h2>
              <pre>{error.message}</pre>
              <button onClick={resetErrorBoundary}>Try again</button>
            </div>
          )}
        >
          {children}
        </ErrorBoundary>
      )}
    </QueryErrorResetBoundary>
  );
}
```

## Hook Reference

### Admin Hooks (Server-side data via API routes)

```typescript
// Providers
useProviders()
useProvider(id)
useCreateProvider()
useUpdateProvider()
useDeleteProvider()

// Virtual Keys
useVirtualKeys()
useVirtualKey(id)
useCreateVirtualKey()
useUpdateVirtualKey()
useDeleteVirtualKey()

// Model Mappings
useModelMappings(providerId)
useCreateModelMapping()
useUpdateModelMapping()
useDeleteModelMapping()

// Media
useUploadMedia()
useDeleteMedia()

// Stats
useStats()
useProviderStats(providerId)
```

### Core Hooks (Client-side with virtual key)

```typescript
// Chat
useChat({ apiKey })
useChatStream({ apiKey })

// Completions
useCompletion({ apiKey })
useCompletionStream({ apiKey })

// Images
useGenerateImage({ apiKey })
useEditImage({ apiKey })

// Embeddings
useCreateEmbedding({ apiKey })

// Real-time
useRealtimeConnection({ hub, virtualKey })
```

## TypeScript Types

```typescript
import type {
  Provider,
  VirtualKey,
  ModelMapping,
  ChatMessage,
  ChatResponse,
  ImageGenerationRequest,
  ImageGenerationResponse
} from '@conduit/admin-client/types';
```

## Common Errors & Solutions

| Error | Solution |
|-------|----------|
| `NEXT_PUBLIC_CONDUIT_API_URL is not defined` | Add to `.env.local` |
| `Unauthorized` | Check admin key in environment |
| `Failed to fetch` | Check CORS and API URL |
| `Hydration mismatch` | Use `'use client'` directive |
| `Cannot read properties of undefined` | Add loading/error checks |

## Performance Tips

1. **Use Suspense** for better perceived performance
2. **Prefetch on hover** for instant navigation
3. **Use optimistic updates** for mutations
4. **Enable query deduplication** (default in React Query)
5. **Set appropriate `staleTime`** to reduce refetches
6. **Use `select` to transform/filter data**
7. **Implement virtual scrolling** for long lists
8. **Use `keepPreviousData`** for pagination

## Security Checklist

- [ ] Admin SDK only in server components/API routes
- [ ] Virtual keys from user input, not hardcoded
- [ ] API routes protected with authentication
- [ ] CORS configured properly
- [ ] Error messages don't leak sensitive info
- [ ] File uploads validated and size-limited
- [ ] Rate limiting implemented
- [ ] SQL injection prevented (if using custom queries)

---

Last updated: 2025-01-08