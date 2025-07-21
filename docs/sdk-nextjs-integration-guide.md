# Conduit SDK - Next.js Integration Guide

This guide demonstrates how to use the Conduit SDK in Next.js applications with real examples from the WebUI.

## Quick Start

### Installation

```bash
# For admin functionality (server-side only)
npm install @conduit/admin-client

# For public API functionality (client/server)
npm install @conduit/core-client
```

### Basic Setup

1. **Configure environment variables:**

```env
# For admin routes (server-side only)
CONDUIT_WEBUI_AUTH_KEY=your-admin-key

# For public API (can be exposed to client)
NEXT_PUBLIC_CONDUIT_API_URL=http://localhost:5074
```

2. **Create SDK instances:**

```typescript
// app/lib/conduit.ts
import { createAdminClient } from '@conduit/admin-client';
import { createCoreClient } from '@conduit/core-client';

// Admin client - server-side only
export const adminClient = createAdminClient({
  baseUrl: process.env.CONDUIT_API_URL || 'http://localhost:5074',
  apiKey: process.env.CONDUIT_WEBUI_AUTH_KEY!
});

// Core client - can be used client-side
export const coreClient = createCoreClient({
  baseUrl: process.env.NEXT_PUBLIC_CONDUIT_API_URL || 'http://localhost:5074'
});
```

## Route Handlers (Admin SDK)

### Creating Routes

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
```

### Handling Parameters

```typescript
// app/api/admin/virtual-keys/[id]/route.ts
import { adminClient } from '@/lib/conduit';
import { NextResponse } from 'next/server';

export async function GET(
  request: Request,
  { params }: { params: { id: string } }
) {
  try {
    const key = await adminClient.virtualKeys.get(params.id);
    return NextResponse.json(key);
  } catch (error) {
    return NextResponse.json(
      { error: 'Virtual key not found' },
      { status: 404 }
    );
  }
}

export async function DELETE(
  request: Request,
  { params }: { params: { id: string } }
) {
  try {
    await adminClient.virtualKeys.delete(params.id);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return NextResponse.json(
      { error: 'Failed to delete virtual key' },
      { status: 500 }
    );
  }
}
```

### Error Responses

```typescript
// app/api/admin/virtual-keys/route.ts
import { adminClient } from '@/lib/conduit';
import { NextResponse } from 'next/server';
import { z } from 'zod';

const createKeySchema = z.object({
  name: z.string().min(1),
  providers: z.array(z.string()),
  maxRequestsPerMinute: z.number().optional()
});

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const data = createKeySchema.parse(body);
    
    const key = await adminClient.virtualKeys.create(data);
    return NextResponse.json(key, { status: 201 });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { error: 'Invalid request', details: error.errors },
        { status: 400 }
      );
    }
    
    return NextResponse.json(
      { error: 'Failed to create virtual key' },
      { status: 500 }
    );
  }
}
```

### File Uploads

```typescript
// app/api/admin/images/upload/route.ts
import { adminClient } from '@/lib/conduit';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  try {
    const formData = await request.formData();
    const file = formData.get('file') as File;
    
    if (!file) {
      return NextResponse.json(
        { error: 'No file provided' },
        { status: 400 }
      );
    }
    
    // Convert File to Buffer
    const buffer = Buffer.from(await file.arrayBuffer());
    
    const result = await adminClient.media.upload({
      file: buffer,
      filename: file.name,
      contentType: file.type
    });
    
    return NextResponse.json(result);
  } catch (error) {
    return NextResponse.json(
      { error: 'Failed to upload file' },
      { status: 500 }
    );
  }
}
```

## React Components (Core SDK)

### Using Hooks

```typescript
// app/components/providers/provider-list.tsx
'use client';

import { useProviders } from '@conduit/admin-client/react';

export function ProviderList() {
  const { data: providers, isLoading, error } = useProviders();
  
  if (isLoading) return <div>Loading providers...</div>;
  if (error) return <div>Error: {error.message}</div>;
  
  return (
    <ul>
      {providers?.map(provider => (
        <li key={provider.id}>{provider.name}</li>
      ))}
    </ul>
  );
}
```

### Error Handling

```typescript
// app/components/virtual-keys/key-form.tsx
'use client';

import { useCreateVirtualKey } from '@conduit/admin-client/react';
import { useState } from 'react';

export function KeyForm() {
  const createKey = useCreateVirtualKey();
  const [error, setError] = useState<string | null>(null);
  
  const handleSubmit = async (data: any) => {
    try {
      setError(null);
      await createKey.mutateAsync(data);
      // Success - redirect or show message
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create key');
    }
  };
  
  return (
    <form onSubmit={handleSubmit}>
      {error && <div className="error">{error}</div>}
      {/* Form fields */}
    </form>
  );
}
```

### Loading States

```typescript
// app/components/model-mappings/mapping-list.tsx
'use client';

import { useModelMappings } from '@conduit/admin-client/react';
import { Skeleton } from '@/components/ui/skeleton';

export function MappingList({ providerId }: { providerId: string }) {
  const { data, isLoading } = useModelMappings(providerId);
  
  if (isLoading) {
    return (
      <div className="space-y-2">
        {[...Array(3)].map((_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    );
  }
  
  return (
    <div>
      {data?.map(mapping => (
        <div key={mapping.id}>{mapping.requestModel} → {mapping.actualModel}</div>
      ))}
    </div>
  );
}
```

### Optimistic Updates

```typescript
// app/components/virtual-keys/key-actions.tsx
'use client';

import { useDeleteVirtualKey, useVirtualKeys } from '@conduit/admin-client/react';
import { useQueryClient } from '@tanstack/react-query';

export function KeyActions({ keyId }: { keyId: string }) {
  const queryClient = useQueryClient();
  const deleteKey = useDeleteVirtualKey();
  
  const handleDelete = () => {
    deleteKey.mutate(keyId, {
      onMutate: async () => {
        // Cancel in-flight queries
        await queryClient.cancelQueries({ queryKey: ['virtualKeys'] });
        
        // Optimistically update cache
        const previous = queryClient.getQueryData(['virtualKeys']);
        queryClient.setQueryData(['virtualKeys'], (old: any) =>
          old?.filter((key: any) => key.id !== keyId)
        );
        
        return { previous };
      },
      onError: (err, variables, context) => {
        // Rollback on error
        if (context?.previous) {
          queryClient.setQueryData(['virtualKeys'], context.previous);
        }
      },
      onSettled: () => {
        // Refetch after mutation
        queryClient.invalidateQueries({ queryKey: ['virtualKeys'] });
      }
    });
  };
  
  return <button onClick={handleDelete}>Delete</button>;
}
```

## Real-time Features

### SignalR Setup

```typescript
// app/lib/signalr.ts
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export function createConnection(hub: string) {
  return new HubConnectionBuilder()
    .withUrl(`${process.env.NEXT_PUBLIC_CONDUIT_API_URL}/hubs/${hub}`)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
}
```

### Connection Management

```typescript
// app/hooks/use-signalr.ts
import { useEffect, useState } from 'react';
import { HubConnection } from '@microsoft/signalr';
import { createConnection } from '@/lib/signalr';

export function useSignalR(hub: string) {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  
  useEffect(() => {
    const conn = createConnection(hub);
    
    conn.onclose(() => setIsConnected(false));
    conn.onreconnected(() => setIsConnected(true));
    
    conn.start()
      .then(() => {
        setConnection(conn);
        setIsConnected(true);
      })
      .catch(console.error);
    
    return () => {
      conn.stop();
    };
  }, [hub]);
  
  return { connection, isConnected };
}
```

### Event Handling

```typescript
// app/components/chat/real-time-chat.tsx
'use client';

import { useSignalR } from '@/hooks/use-signalr';
import { useEffect, useState } from 'react';

interface Message {
  id: string;
  content: string;
  timestamp: Date;
}

export function RealTimeChat({ virtualKey }: { virtualKey: string }) {
  const { connection, isConnected } = useSignalR('navigation-state');
  const [messages, setMessages] = useState<Message[]>([]);
  
  useEffect(() => {
    if (!connection) return;
    
    // Join virtual key group
    connection.invoke('JoinGroup', virtualKey);
    
    // Listen for messages
    connection.on('ReceiveMessage', (message: Message) => {
      setMessages(prev => [...prev, message]);
    });
    
    return () => {
      connection.off('ReceiveMessage');
      connection.invoke('LeaveGroup', virtualKey);
    };
  }, [connection, virtualKey]);
  
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

## Security

### Admin vs Core SDK

```typescript
// ❌ NEVER expose admin SDK in client components
'use client';
import { adminClient } from '@/lib/conduit'; // ❌ This exposes your admin key!

// ✅ Use route handlers for admin operations
'use client';
export function DeleteButton({ id }: { id: string }) {
  const handleDelete = async () => {
    await fetch(`/api/admin/virtual-keys/${id}`, { method: 'DELETE' });
  };
  return <button onClick={handleDelete}>Delete</button>;
}
```

### Key Management

```typescript
// ✅ Correct: Keys in environment variables
const adminClient = createAdminClient({
  apiKey: process.env.CONDUIT_WEBUI_AUTH_KEY!
});

// ❌ Wrong: Hardcoded keys
const adminClient = createAdminClient({
  apiKey: 'sk-abc123' // NEVER DO THIS
});

// ✅ Correct: Virtual key from user input
const coreClient = createCoreClient({
  apiKey: userProvidedVirtualKey // From form input or cookie
});
```

### CORS Configuration

```typescript
// app/api/public/[...path]/route.ts
import { NextResponse } from 'next/server';

// Handle CORS for public API routes
export async function OPTIONS() {
  return new NextResponse(null, {
    status: 200,
    headers: {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, PUT, DELETE, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type, Authorization'
    }
  });
}
```

## Migration Guide

### From Raw Fetch

#### List Views

```typescript
// ❌ Old Way (50+ lines)
export function useProviders() {
  const [providers, setProviders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  
  useEffect(() => {
    async function fetchProviders() {
      try {
        const response = await fetch('/api/admin/providers', {
          headers: {
            'Authorization': `Bearer ${process.env.NEXT_PUBLIC_ADMIN_KEY}`
          }
        });
        
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const data = await response.json();
        setProviders(data.items || data);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    }
    
    fetchProviders();
  }, []);
  
  return { providers, loading, error };
}

// ✅ New Way (1 line)
import { useProviders } from '@conduit/admin-client/react';
```

#### Create Operations

```typescript
// ❌ Old Way (30+ lines)
async function createVirtualKey(data) {
  try {
    const response = await fetch('/api/admin/virtual-keys', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${process.env.NEXT_PUBLIC_ADMIN_KEY}`
      },
      body: JSON.stringify(data)
    });
    
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to create key');
    }
    
    return await response.json();
  } catch (err) {
    console.error('Error creating virtual key:', err);
    throw err;
  }
}

// ✅ New Way (3 lines)
import { useCreateVirtualKey } from '@conduit/admin-client/react';
const createKey = useCreateVirtualKey();
await createKey.mutateAsync(data);
```

#### Update Operations

```typescript
// ❌ Old Way (35+ lines)
async function updateModelMapping(providerId, mappingId, data) {
  try {
    const response = await fetch(
      `/api/admin/providers/${providerId}/model-mappings/${mappingId}`,
      {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${process.env.NEXT_PUBLIC_ADMIN_KEY}`
        },
        body: JSON.stringify(data)
      }
    );
    
    if (!response.ok) {
      throw new Error('Failed to update mapping');
    }
    
    return await response.json();
  } catch (err) {
    console.error('Error updating mapping:', err);
    throw err;
  }
}

// ✅ New Way (3 lines)
import { useUpdateModelMapping } from '@conduit/admin-client/react';
const updateMapping = useUpdateModelMapping();
await updateMapping.mutateAsync({ providerId, mappingId, data });
```

### From Old SDK Version

```typescript
// Old SDK (v1.x)
import { ConduitClient } from '@conduit/client';
const client = new ConduitClient({ apiKey: 'sk-xxx' });
const providers = await client.getProviders();

// New SDK (v2.x)
import { createAdminClient } from '@conduit/admin-client';
const client = createAdminClient({ apiKey: 'sk-xxx' });
const providers = await client.providers.list();
```

### Common Patterns

#### Pagination

```typescript
// ❌ Old Way - Manual pagination handling
const [page, setPage] = useState(1);
const [hasMore, setHasMore] = useState(true);

useEffect(() => {
  fetch(`/api/items?page=${page}&limit=20`)
    .then(res => res.json())
    .then(data => {
      setItems(data.items);
      setHasMore(data.hasMore);
    });
}, [page]);

// ✅ New Way - Built-in pagination
const { data, fetchNextPage, hasNextPage } = useVirtualKeys({
  limit: 20
});
```

#### Error Boundaries

```typescript
// app/components/error-boundary.tsx
'use client';

import { useQueryErrorResetBoundary } from '@tanstack/react-query';
import { ErrorBoundary } from 'react-error-boundary';

export function QueryErrorBoundary({ children }: { children: React.ReactNode }) {
  const { reset } = useQueryErrorResetBoundary();
  
  return (
    <ErrorBoundary
      onReset={reset}
      fallbackRender={({ error, resetErrorBoundary }) => (
        <div>
          <p>Something went wrong: {error.message}</p>
          <button onClick={resetErrorBoundary}>Try again</button>
        </div>
      )}
    >
      {children}
    </ErrorBoundary>
  );
}
```

## Examples

### Virtual Keys Management

```typescript
// app/components/virtual-keys/virtual-keys-page.tsx
'use client';

import { useVirtualKeys, useCreateVirtualKey, useDeleteVirtualKey } from '@conduit/admin-client/react';
import { useState } from 'react';

export function VirtualKeysPage() {
  const { data: keys, isLoading } = useVirtualKeys();
  const createKey = useCreateVirtualKey();
  const deleteKey = useDeleteVirtualKey();
  const [isCreating, setIsCreating] = useState(false);
  
  const handleCreate = async (formData: FormData) => {
    const data = {
      name: formData.get('name') as string,
      providers: formData.getAll('providers') as string[],
      maxRequestsPerMinute: parseInt(formData.get('rateLimit') as string)
    };
    
    await createKey.mutateAsync(data);
    setIsCreating(false);
  };
  
  const handleDelete = async (id: string) => {
    if (confirm('Are you sure?')) {
      await deleteKey.mutateAsync(id);
    }
  };
  
  if (isLoading) return <div>Loading...</div>;
  
  return (
    <div>
      <button onClick={() => setIsCreating(true)}>Create Key</button>
      
      {isCreating && (
        <form onSubmit={(e) => {
          e.preventDefault();
          handleCreate(new FormData(e.currentTarget));
        }}>
          <input name="name" placeholder="Key name" required />
          <select name="providers" multiple>
            <option value="openai">OpenAI</option>
            <option value="anthropic">Anthropic</option>
          </select>
          <input name="rateLimit" type="number" placeholder="Rate limit" />
          <button type="submit">Create</button>
        </form>
      )}
      
      <ul>
        {keys?.map(key => (
          <li key={key.id}>
            {key.name}
            <button onClick={() => handleDelete(key.id)}>Delete</button>
          </li>
        ))}
      </ul>
    </div>
  );
}
```

### Model Mappings CRUD

```typescript
// app/components/model-mappings/mappings-manager.tsx
'use client';

import {
  useModelMappings,
  useCreateModelMapping,
  useUpdateModelMapping,
  useDeleteModelMapping
} from '@conduit/admin-client/react';

export function MappingsManager({ providerId }: { providerId: string }) {
  const { data: mappings } = useModelMappings(providerId);
  const createMapping = useCreateModelMapping();
  const updateMapping = useUpdateModelMapping();
  const deleteMapping = useDeleteModelMapping();
  
  const handleCreate = async (data: any) => {
    await createMapping.mutateAsync({ providerId, ...data });
  };
  
  const handleUpdate = async (mappingId: string, data: any) => {
    await updateMapping.mutateAsync({ providerId, mappingId, data });
  };
  
  const handleDelete = async (mappingId: string) => {
    await deleteMapping.mutateAsync({ providerId, mappingId });
  };
  
  return (
    <div>
      {mappings?.map(mapping => (
        <MappingRow
          key={mapping.id}
          mapping={mapping}
          onUpdate={(data) => handleUpdate(mapping.id, data)}
          onDelete={() => handleDelete(mapping.id)}
        />
      ))}
      <CreateMappingForm onSubmit={handleCreate} />
    </div>
  );
}
```

### Chat Interface

```typescript
// app/components/chat/chat-interface.tsx
'use client';

import { useChat } from '@conduit/core-client/react';
import { useState } from 'react';

export function ChatInterface({ virtualKey }: { virtualKey: string }) {
  const [messages, setMessages] = useState<Array<{ role: string; content: string }>>([]);
  const chat = useChat({ apiKey: virtualKey });
  
  const sendMessage = async (content: string) => {
    const userMessage = { role: 'user', content };
    setMessages(prev => [...prev, userMessage]);
    
    try {
      const response = await chat.mutateAsync({
        messages: [...messages, userMessage],
        stream: true
      });
      
      // Handle streaming response
      let assistantMessage = { role: 'assistant', content: '' };
      setMessages(prev => [...prev, assistantMessage]);
      
      for await (const chunk of response) {
        assistantMessage.content += chunk.content;
        setMessages(prev => [
          ...prev.slice(0, -1),
          { ...assistantMessage }
        ]);
      }
    } catch (error) {
      console.error('Chat error:', error);
    }
  };
  
  return (
    <div>
      {messages.map((msg, i) => (
        <div key={i} className={msg.role}>
          {msg.content}
        </div>
      ))}
      <ChatInput onSend={sendMessage} />
    </div>
  );
}
```

### Image Generation UI

```typescript
// app/components/images/image-generator.tsx
'use client';

import { useGenerateImage } from '@conduit/core-client/react';
import { useState } from 'react';

export function ImageGenerator({ virtualKey }: { virtualKey: string }) {
  const generateImage = useGenerateImage({ apiKey: virtualKey });
  const [images, setImages] = useState<string[]>([]);
  
  const handleGenerate = async (prompt: string) => {
    try {
      const result = await generateImage.mutateAsync({
        prompt,
        size: '1024x1024',
        n: 1
      });
      
      setImages(prev => [...prev, ...result.data.map(d => d.url)]);
    } catch (error) {
      console.error('Generation error:', error);
    }
  };
  
  return (
    <div>
      <form onSubmit={(e) => {
        e.preventDefault();
        const prompt = new FormData(e.currentTarget).get('prompt') as string;
        handleGenerate(prompt);
      }}>
        <input name="prompt" placeholder="Describe your image..." />
        <button type="submit" disabled={generateImage.isPending}>
          {generateImage.isPending ? 'Generating...' : 'Generate'}
        </button>
      </form>
      
      <div className="grid grid-cols-2 gap-4">
        {images.map((url, i) => (
          <img key={i} src={url} alt={`Generated ${i}`} />
        ))}
      </div>
    </div>
  );
}
```

## Performance Tips

### Query Optimization

```typescript
// Prefetch data on hover
import { useQueryClient } from '@tanstack/react-query';

export function KeyLink({ keyId }: { keyId: string }) {
  const queryClient = useQueryClient();
  
  return (
    <Link
      href={`/keys/${keyId}`}
      onMouseEnter={() => {
        queryClient.prefetchQuery({
          queryKey: ['virtualKey', keyId],
          queryFn: () => fetch(`/api/admin/virtual-keys/${keyId}`).then(r => r.json())
        });
      }}
    >
      View Key
    </Link>
  );
}
```

### Batch Operations

```typescript
// Process multiple items efficiently
const deleteMultiple = async (ids: string[]) => {
  await Promise.all(
    ids.map(id => deleteKey.mutateAsync(id))
  );
  
  // Invalidate once after all deletions
  queryClient.invalidateQueries({ queryKey: ['virtualKeys'] });
};
```

### Suspense Integration

```typescript
// app/components/providers/providers-list.tsx
import { Suspense } from 'react';
import { useProviders } from '@conduit/admin-client/react';

function ProvidersList() {
  const { data: providers } = useProviders({ suspense: true });
  
  return (
    <ul>
      {providers.map(p => <li key={p.id}>{p.name}</li>)}
    </ul>
  );
}

export function ProvidersPage() {
  return (
    <Suspense fallback={<div>Loading providers...</div>}>
      <ProvidersList />
    </Suspense>
  );
}
```

## Anti-Patterns to Avoid

### ❌ Don't Use Admin SDK in Client Components

```typescript
// ❌ WRONG - Exposes admin key to browser
'use client';
import { adminClient } from '@/lib/conduit';

export function BadComponent() {
  const handleClick = async () => {
    await adminClient.virtualKeys.create({ name: 'test' }); // ❌
  };
}

// ✅ CORRECT - Use route handler
'use client';
export function GoodComponent() {
  const handleClick = async () => {
    await fetch('/api/admin/virtual-keys', {
      method: 'POST',
      body: JSON.stringify({ name: 'test' })
    });
  };
}
```

### ❌ Don't Expose Virtual Keys in Code

```typescript
// ❌ WRONG - Hardcoded virtual key
const client = createCoreClient({
  apiKey: 'vk_abc123' // ❌ Never hardcode keys
});

// ✅ CORRECT - Get from user input or secure storage
const client = createCoreClient({
  apiKey: getCookieValue('userVirtualKey') // ✅
});
```

### ❌ Don't Wrap SDK Methods Unnecessarily

```typescript
// ❌ WRONG - Unnecessary wrapper
export async function getProviders() {
  return await adminClient.providers.list();
}

// ✅ CORRECT - Use SDK directly
import { useProviders } from '@conduit/admin-client/react';
```

### ❌ Don't Ignore Error States

```typescript
// ❌ WRONG - No error handling
export function BadList() {
  const { data } = useVirtualKeys();
  return <div>{data.map(...)}</div>; // Will crash if data is undefined
}

// ✅ CORRECT - Handle all states
export function GoodList() {
  const { data, isLoading, error } = useVirtualKeys();
  
  if (isLoading) return <Loading />;
  if (error) return <Error error={error} />;
  if (!data?.length) return <Empty />;
  
  return <div>{data.map(...)}</div>;
}
```

### ❌ Don't Mix Admin and Core Operations

```typescript
// ❌ WRONG - Mixing concerns
export function BadChat() {
  const { data: keys } = useVirtualKeys(); // Admin operation
  const chat = useChat({ apiKey: keys[0].key }); // Core operation
}

// ✅ CORRECT - Separate concerns
// Admin component
export function KeySelector({ onSelect }) {
  const { data: keys } = useVirtualKeys();
  return <select onChange={e => onSelect(e.target.value)}>...</select>;
}

// Core component
export function Chat({ virtualKey }) {
  const chat = useChat({ apiKey: virtualKey });
  // ...
}
```

## Troubleshooting

### Common Issues

1. **"Failed to fetch" errors**
   - Check CORS configuration
   - Verify API URL in environment variables
   - Ensure server is running

2. **"Unauthorized" errors**
   - Verify admin key is set correctly
   - Check key hasn't expired
   - Ensure using correct SDK (admin vs core)

3. **TypeScript errors**
   - Update to latest SDK version
   - Run `npm install` to sync types
   - Check tsconfig includes node_modules

4. **Real-time updates not working**
   - Verify SignalR hub URLs
   - Check WebSocket support
   - Look for connection errors in console

### Debug Mode

Enable detailed logging:

```typescript
// Enable query debugging
import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: process.env.NODE_ENV === 'production' ? 3 : 0,
      logger: {
        log: console.log,
        warn: console.warn,
        error: console.error
      }
    }
  }
});
```

## Next Steps

- Review the [API Reference](/docs/api-reference.md) for detailed method documentation
- Check out the [WebUI source code](https://github.com/knnlabs/Conduit/tree/main/ConduitLLM.WebUI) for more examples
- Join our [Discord community](https://discord.gg/conduit) for support

---

Last updated: 2025-01-08