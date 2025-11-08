# Conduit SDK Best Practices & Anti-Patterns

This guide covers best practices and common mistakes when using the Conduit SDK in Next.js applications.

## Table of Contents

1. [Security Best Practices](#security-best-practices)
2. [Performance Optimization](#performance-optimization)
3. [Error Handling Patterns](#error-handling-patterns)
4. [State Management](#state-management)
5. [Common Anti-Patterns](#common-anti-patterns)
6. [Testing Strategies](#testing-strategies)
7. [Production Considerations](#production-considerations)

## Security Best Practices

### 1. Keep Admin SDK Server-Side Only

**✅ CORRECT: Admin operations through API routes**

```typescript
// app/api/admin/providers/route.ts
import { adminClient } from '@/lib/conduit-server'; // Server-only import

export async function GET() {
  const providers = await adminClient.providers.list();
  return NextResponse.json(providers);
}

// app/components/providers-list.tsx
'use client';

export function ProvidersList() {
  const { data } = useQuery({
    queryKey: ['providers'],
    queryFn: () => fetch('/api/admin/providers').then(r => r.json())
  });
  
  return <div>{/* render providers */}</div>;
}
```

**❌ WRONG: Admin SDK in client components**

```typescript
// app/components/providers-list.tsx
'use client';

import { adminClient } from '@/lib/conduit'; // ❌ Exposes admin key!

export function ProvidersList() {
  const [providers, setProviders] = useState([]);
  
  useEffect(() => {
    adminClient.providers.list().then(setProviders); // ❌ Never do this!
  }, []);
}
```

### 2. Virtual Key Management

**✅ CORRECT: Virtual keys from user input or secure storage**

```typescript
// app/components/chat/chat-provider.tsx
'use client';

import { createCoreClient } from '@conduit/core-client';
import { createContext, useContext } from 'react';

const ChatContext = createContext<any>(null);

export function ChatProvider({ children }: { children: React.ReactNode }) {
  const [virtualKey, setVirtualKey] = useState(() => {
    // Get from secure cookie or localStorage
    return getCookie('userVirtualKey') || null;
  });
  
  const client = useMemo(() => {
    if (!virtualKey) return null;
    return createCoreClient({ apiKey: virtualKey });
  }, [virtualKey]);
  
  return (
    <ChatContext.Provider value={{ client, setVirtualKey }}>
      {children}
    </ChatContext.Provider>
  );
}
```

**❌ WRONG: Hardcoded virtual keys**

```typescript
// ❌ Never hardcode keys
const client = createCoreClient({
  apiKey: 'vk_1234567890' // ❌ Security vulnerability!
});

// ❌ Don't commit keys to environment variables
const client = createCoreClient({
  apiKey: process.env.NEXT_PUBLIC_VIRTUAL_KEY // ❌ Exposed to client!
});
```

### 3. API Route Protection

**✅ CORRECT: Proper authentication checks**

```typescript
// app/api/admin/[...path]/route.ts
import { NextRequest } from 'next/server';
import { verifyAdminToken } from '@/lib/auth';

async function withAuth(
  request: NextRequest,
  handler: (req: NextRequest) => Promise<Response>
) {
  const token = request.headers.get('authorization')?.split(' ')[1];
  
  if (!token || !await verifyAdminToken(token)) {
    return NextResponse.json(
      { error: 'Unauthorized' },
      { status: 401 }
    );
  }
  
  return handler(request);
}

export const GET = (req: NextRequest) => withAuth(req, async (req) => {
  // Protected handler logic
});
```

**❌ WRONG: No authentication**

```typescript
// ❌ Unprotected admin endpoint
export async function GET() {
  const keys = await adminClient.virtualKeys.list();
  return NextResponse.json(keys); // ❌ Anyone can access!
}
```

## Performance Optimization

### 1. Query Optimization

**✅ CORRECT: Efficient data fetching**

```typescript
// Parallel queries for independent data
export function Dashboard() {
  // These queries run in parallel
  const providersQuery = useProviders();
  const keysQuery = useVirtualKeys();
  const statsQuery = useStats();
  
  // Use React.Suspense for better UX
  return (
    <div>
      <Suspense fallback={<Skeleton />}>
        <ProvidersList />
      </Suspense>
      <Suspense fallback={<Skeleton />}>
        <KeysList />
      </Suspense>
      <Suspense fallback={<Skeleton />}>
        <StatsChart />
      </Suspense>
    </div>
  );
}

// Prefetch on hover
export function ProviderLink({ id, children }: Props) {
  const queryClient = useQueryClient();
  
  return (
    <Link
      href={`/providers/${id}`}
      onMouseEnter={() => {
        queryClient.prefetchQuery({
          queryKey: ['provider', id],
          queryFn: () => fetchProvider(id),
          staleTime: 30000, // Cache for 30 seconds
        });
      }}
    >
      {children}
    </Link>
  );
}
```

**❌ WRONG: Waterfall requests**

```typescript
// ❌ Sequential loading
export function Dashboard() {
  const [providers, setProviders] = useState();
  const [keys, setKeys] = useState();
  
  useEffect(() => {
    // These run sequentially!
    fetch('/api/providers')
      .then(r => r.json())
      .then(data => {
        setProviders(data);
        // Only then fetch keys
        return fetch('/api/keys');
      })
      .then(r => r.json())
      .then(setKeys);
  }, []);
}
```

### 2. Mutation Optimization

**✅ CORRECT: Optimistic updates**

```typescript
export function TodoList() {
  const queryClient = useQueryClient();
  const deleteTodo = useDeleteTodo();
  
  const handleDelete = (id: string) => {
    deleteTodo.mutate(id, {
      // Optimistically remove from cache
      onMutate: async (deletedId) => {
        await queryClient.cancelQueries({ queryKey: ['todos'] });
        
        const previous = queryClient.getQueryData(['todos']);
        
        queryClient.setQueryData(['todos'], (old: any) =>
          old?.filter((todo: any) => todo.id !== deletedId)
        );
        
        return { previous };
      },
      // Rollback on error
      onError: (err, deletedId, context) => {
        queryClient.setQueryData(['todos'], context?.previous);
        toast.error('Failed to delete');
      },
      // Always refetch after mutation
      onSettled: () => {
        queryClient.invalidateQueries({ queryKey: ['todos'] });
      },
    });
  };
}
```

**❌ WRONG: No optimistic updates**

```typescript
// ❌ Poor UX - waits for server response
export function TodoList() {
  const deleteTodo = useDeleteTodo();
  
  const handleDelete = async (id: string) => {
    await deleteTodo.mutateAsync(id); // User waits...
    window.location.reload(); // ❌ Full page reload!
  };
}
```

### 3. Bundle Size Optimization

**✅ CORRECT: Tree-shakeable imports**

```typescript
// Only import what you need
import { useProviders, useCreateProvider } from '@conduit/admin-client/react';

// Dynamic imports for large components
const HeavyChart = dynamic(() => import('./HeavyChart'), {
  loading: () => <Skeleton />,
  ssr: false,
});
```

**❌ WRONG: Import everything**

```typescript
// ❌ Imports entire library
import * as ConduitSDK from '@conduit/admin-client';

// ❌ Importing server code in client
import { adminClient } from '@/lib/conduit'; // Contains Node.js dependencies
```

## Error Handling Patterns

### 1. Graceful Degradation

**✅ CORRECT: Handle all states**

```typescript
export function ProviderConfig({ id }: { id: string }) {
  const { data, isLoading, error } = useProvider(id);
  
  // Loading state
  if (isLoading) {
    return <ConfigSkeleton />;
  }
  
  // Error state with retry
  if (error) {
    return (
      <ErrorCard
        message={error.message}
        onRetry={() => window.location.reload()}
      />
    );
  }
  
  // Empty state
  if (!data) {
    return (
      <EmptyState
        message="Provider not found"
        action={
          <Link href="/providers/new">Create Provider</Link>
        }
      />
    );
  }
  
  // Success state
  return <ConfigForm provider={data} />;
}
```

**❌ WRONG: Assume success**

```typescript
// ❌ Will crash if data is undefined
export function ProviderConfig({ id }: { id: string }) {
  const { data } = useProvider(id);
  
  return (
    <div>
      <h1>{data.name}</h1> {/* ❌ Cannot read property 'name' of undefined */}
      <p>{data.description}</p>
    </div>
  );
}
```

### 2. Form Error Handling

**✅ CORRECT: User-friendly error messages**

```typescript
export function CreateKeyForm() {
  const createKey = useCreateVirtualKey();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  
  const handleSubmit = async (data: FormData) => {
    try {
      setFieldErrors({});
      await createKey.mutateAsync(data);
      toast.success('Virtual key created successfully');
      router.push('/keys');
    } catch (error: any) {
      // Handle validation errors
      if (error.status === 400 && error.details) {
        const errors: Record<string, string> = {};
        error.details.forEach((detail: any) => {
          errors[detail.field] = detail.message;
        });
        setFieldErrors(errors);
      } else if (error.status === 409) {
        setFieldErrors({ name: 'A key with this name already exists' });
      } else {
        toast.error('Failed to create key. Please try again.');
      }
    }
  };
  
  return (
    <form onSubmit={handleSubmit}>
      <input
        name="name"
        aria-invalid={!!fieldErrors.name}
        aria-describedby={fieldErrors.name ? 'name-error' : undefined}
      />
      {fieldErrors.name && (
        <span id="name-error" className="error">
          {fieldErrors.name}
        </span>
      )}
    </form>
  );
}
```

**❌ WRONG: Generic error messages**

```typescript
// ❌ Poor UX
export function CreateKeyForm() {
  const handleSubmit = async (data: FormData) => {
    try {
      await createKey(data);
    } catch (error) {
      alert('Error!'); // ❌ Not helpful
    }
  };
}
```

## State Management

### 1. Server State vs Client State

**✅ CORRECT: Separate concerns**

```typescript
// Server state: Use React Query
export function ProvidersList() {
  const { data: providers } = useProviders(); // Server state
  
  // Client state: Use React state
  const [filter, setFilter] = useState(''); // UI state
  const [selected, setSelected] = useState<Set<string>>(new Set()); // Selection state
  
  const filtered = providers?.filter(p => 
    p.name.toLowerCase().includes(filter.toLowerCase())
  );
  
  return (
    <div>
      <input
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        placeholder="Filter providers..."
      />
      <ProviderTable
        providers={filtered}
        selected={selected}
        onSelect={setSelected}
      />
    </div>
  );
}
```

**❌ WRONG: Mixing concerns**

```typescript
// ❌ Don't sync server state to local state
export function ProvidersList() {
  const { data: serverProviders } = useProviders();
  const [providers, setProviders] = useState([]); // ❌ Duplicate state
  
  useEffect(() => {
    if (serverProviders) {
      setProviders(serverProviders); // ❌ Unnecessary sync
    }
  }, [serverProviders]);
}
```

### 2. Global State Management

**✅ CORRECT: Use React Query for server state**

```typescript
// Shared server state across components
export function useCurrentUser() {
  return useQuery({
    queryKey: ['currentUser'],
    queryFn: fetchCurrentUser,
    staleTime: 5 * 60 * 1000, // Consider fresh for 5 minutes
  });
}

// Component A
function Header() {
  const { data: user } = useCurrentUser(); // Cached
  return <div>Welcome, {user?.name}</div>;
}

// Component B
function Settings() {
  const { data: user } = useCurrentUser(); // Same cached data
  return <div>Email: {user?.email}</div>;
}
```

**❌ WRONG: Props drilling or context for server state**

```typescript
// ❌ Don't use Context for server state
const UserContext = createContext();

function App() {
  const [user, setUser] = useState();
  
  useEffect(() => {
    fetchUser().then(setUser); // ❌ No caching, refetching, etc.
  }, []);
  
  return (
    <UserContext.Provider value={user}>
      {/* Props drilling or context consumption */}
    </UserContext.Provider>
  );
}
```

## Common Anti-Patterns

### 1. Over-fetching

**❌ WRONG: Fetching unnecessary data**

```typescript
// ❌ Fetching all fields when you only need a few
export function KeysList() {
  const { data } = useQuery({
    queryKey: ['virtualKeys'],
    queryFn: () => fetch('/api/admin/virtual-keys?expand=all'), // ❌ Too much data
  });
  
  // Only using name and id
  return data?.map(key => <div key={key.id}>{key.name}</div>);
}
```

**✅ CORRECT: Fetch only what you need**

```typescript
export function KeysList() {
  const { data } = useVirtualKeys({
    select: ['id', 'name'], // Only fetch required fields
  });
  
  return data?.map(key => <div key={key.id}>{key.name}</div>);
}
```

### 2. Inefficient Polling

**❌ WRONG: Polling everything**

```typescript
// ❌ Wasteful polling
export function Dashboard() {
  // Polls every 5 seconds even if user is not looking
  const { data: providers } = useProviders({
    refetchInterval: 5000, // ❌ Always polling
  });
}
```

**✅ CORRECT: Smart polling**

```typescript
export function Dashboard() {
  const [isVisible, setIsVisible] = useState(true);
  
  // Only poll when visible and focused
  const { data: providers } = useProviders({
    refetchInterval: isVisible ? 5000 : false,
    refetchIntervalInBackground: false,
  });
  
  useEffect(() => {
    const handleVisibilityChange = () => {
      setIsVisible(!document.hidden);
    };
    
    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, []);
}
```

### 3. Memory Leaks

**❌ WRONG: Not cleaning up subscriptions**

```typescript
// ❌ Memory leak
export function RealtimeComponent() {
  const [data, setData] = useState();
  
  useEffect(() => {
    const connection = createConnection();
    connection.on('update', setData);
    connection.start();
    // ❌ No cleanup!
  }, []);
}
```

**✅ CORRECT: Proper cleanup**

```typescript
export function RealtimeComponent() {
  const [data, setData] = useState();
  
  useEffect(() => {
    const connection = createConnection();
    connection.on('update', setData);
    connection.start();
    
    return () => {
      connection.off('update', setData);
      connection.stop();
    };
  }, []);
}
```

## Testing Strategies

### 1. Mock SDK in Tests

**✅ CORRECT: Proper mocking**

```typescript
// __tests__/components/ProvidersList.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { useProviders } from '@conduit/admin-client/react';

// Mock the SDK
jest.mock('@conduit/admin-client/react', () => ({
  useProviders: jest.fn(),
}));

const mockUseProviders = useProviders as jest.MockedFunction<typeof useProviders>;

describe('ProvidersList', () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });
  
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
  
  it('renders providers list', async () => {
    mockUseProviders.mockReturnValue({
      data: [
        { id: '1', name: 'OpenAI' },
        { id: '2', name: 'Anthropic' },
      ],
      isLoading: false,
      error: null,
    } as any);
    
    render(<ProvidersList />, { wrapper });
    
    await waitFor(() => {
      expect(screen.getByText('OpenAI')).toBeInTheDocument();
      expect(screen.getByText('Anthropic')).toBeInTheDocument();
    });
  });
});
```

### 2. Integration Tests

**✅ CORRECT: Test with MSW**

```typescript
// __tests__/integration/providers.test.tsx
import { setupServer } from 'msw/node';
import { rest } from 'msw';

const server = setupServer(
  rest.get('/api/admin/providers', (req, res, ctx) => {
    return res(
      ctx.json([
        { id: '1', name: 'OpenAI' },
        { id: '2', name: 'Anthropic' },
      ])
    );
  })
);

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

test('creates provider', async () => {
  server.use(
    rest.post('/api/admin/providers', (req, res, ctx) => {
      return res(ctx.json({ id: '3', name: 'New Provider' }));
    })
  );
  
  // Test your component with real network calls
});
```

## Production Considerations

### 1. Error Boundaries

**✅ CORRECT: Graceful error handling**

```typescript
// app/providers.tsx
export function RootProviders({ children }: { children: React.ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <ErrorBoundary
        fallback={<ErrorFallback />}
        onReset={() => queryClient.clear()}
      >
        {children}
      </ErrorBoundary>
    </QueryClientProvider>
  );
}

function ErrorFallback({ error, resetErrorBoundary }: any) {
  useEffect(() => {
    // Log to error reporting service
    console.error('Application error:', error);
  }, [error]);
  
  return (
    <div role="alert">
      <h2>Something went wrong</h2>
      <pre>{error.message}</pre>
      <button onClick={resetErrorBoundary}>Try again</button>
    </div>
  );
}
```

### 2. Performance Monitoring

**✅ CORRECT: Track performance metrics**

```typescript
// lib/monitoring.ts
export function trackApiCall(endpoint: string, duration: number, status: number) {
  // Send to analytics
  if (window.gtag) {
    window.gtag('event', 'api_call', {
      endpoint,
      duration,
      status,
      timestamp: Date.now(),
    });
  }
  
  // Log slow requests
  if (duration > 3000) {
    console.warn(`Slow API call to ${endpoint}: ${duration}ms`);
  }
}

// In your API client
const response = await fetch(url);
trackApiCall(url, performance.now() - startTime, response.status);
```

### 3. Security Headers

**✅ CORRECT: Implement security headers**

```typescript
// next.config.js
module.exports = {
  async headers() {
    return [
      {
        source: '/api/:path*',
        headers: [
          {
            key: 'X-Content-Type-Options',
            value: 'nosniff',
          },
          {
            key: 'X-Frame-Options',
            value: 'DENY',
          },
          {
            key: 'X-XSS-Protection',
            value: '1; mode=block',
          },
          {
            key: 'Referrer-Policy',
            value: 'strict-origin-when-cross-origin',
          },
        ],
      },
    ];
  },
};
```

## Summary

### Do's

1. ✅ Keep admin SDK server-side only
2. ✅ Use proper error boundaries
3. ✅ Implement optimistic updates
4. ✅ Handle all loading/error states
5. ✅ Use Suspense for better UX
6. ✅ Cache and prefetch strategically
7. ✅ Clean up subscriptions/connections
8. ✅ Test with proper mocks
9. ✅ Monitor performance in production
10. ✅ Follow security best practices

### Don'ts

1. ❌ Don't expose admin keys to client
2. ❌ Don't hardcode virtual keys
3. ❌ Don't sync server state to local state
4. ❌ Don't ignore error states
5. ❌ Don't over-fetch data
6. ❌ Don't poll unnecessarily
7. ❌ Don't create memory leaks
8. ❌ Don't use generic error messages
9. ❌ Don't mix concerns (server/client state)
10. ❌ Don't skip authentication checks

---

Last updated: 2025-01-08