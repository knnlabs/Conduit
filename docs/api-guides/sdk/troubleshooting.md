# Conduit SDK Troubleshooting Guide

This guide helps you diagnose and fix common issues when using the Conduit SDK with Next.js.

## Table of Contents

1. [Installation Issues](#installation-issues)
2. [Authentication Errors](#authentication-errors)
3. [Network & CORS Issues](#network--cors-issues)
4. [TypeScript Errors](#typescript-errors)
5. [React Query Issues](#react-query-issues)
6. [Real-time Connection Problems](#real-time-connection-problems)
7. [Performance Issues](#performance-issues)
8. [Build & Deployment Errors](#build--deployment-errors)
9. [Common Runtime Errors](#common-runtime-errors)
10. [Debugging Tools & Techniques](#debugging-tools--techniques)

## Installation Issues

### Problem: Module not found errors

```
Module not found: Can't resolve '@conduit/admin-client'
```

**Solution:**

```bash
# Clear npm cache
npm cache clean --force

# Delete node_modules and lock file
rm -rf node_modules package-lock.json

# Reinstall
npm install
npm install @conduit/admin-client @conduit/core-client
```

### Problem: Peer dependency conflicts

```
npm ERR! peer dep missing: react@^18.0.0
```

**Solution:**

```bash
# Install with legacy peer deps flag
npm install --legacy-peer-deps

# Or update your React version
npm install react@^18.0.0 react-dom@^18.0.0
```

### Problem: TypeScript declaration files not found

```
Could not find a declaration file for module '@conduit/admin-client'
```

**Solution:**

```bash
# Ensure TypeScript is installed
npm install --save-dev typescript @types/react @types/node

# Generate declaration files
npx tsc --init
```

## Authentication Errors

### Problem: 401 Unauthorized in admin routes

```
Error: Request failed with status code 401
```

**Solution 1: Check environment variables**

```typescript
// .env.local
CONDUIT_API_URL=http://localhost:5074
CONDUIT_WEBUI_AUTH_KEY=your-admin-key-here

// Verify in your code
console.log('API URL:', process.env.CONDUIT_API_URL);
console.log('Has auth key:', !!process.env.CONDUIT_WEBUI_AUTH_KEY);
```

**Solution 2: Verify key format**

```typescript
// ❌ Wrong - Including "Bearer" prefix
const adminClient = createAdminClient({
  apiKey: 'Bearer sk-xxx' // Remove "Bearer"
});

// ✅ Correct
const adminClient = createAdminClient({
  apiKey: 'sk-xxx'
});
```

**Solution 3: Check server-side vs client-side**

```typescript
// ❌ Wrong - Using admin key in client component
'use client';
const client = createAdminClient({ apiKey: process.env.CONDUIT_WEBUI_AUTH_KEY });

// ✅ Correct - Use in API route only
// app/api/admin/providers/route.ts
import { adminClient } from '@/lib/conduit-server';
```

### Problem: Virtual key not working

```
Error: Invalid virtual key
```

**Solution:**

```typescript
// Check virtual key format
const isValidVirtualKey = (key: string) => {
  return key.startsWith('vk_') && key.length > 10;
};

// Debug virtual key source
console.log('Virtual key from:', {
  cookie: getCookie('virtualKey'),
  localStorage: localStorage.getItem('virtualKey'),
  prop: props.virtualKey
});
```

## Network & CORS Issues

### Problem: CORS errors in browser

```
Access to fetch at 'http://localhost:5074/api' from origin 'http://localhost:3000' has been blocked by CORS policy
```

**Solution 1: Use Next.js API routes as proxy**

```typescript
// app/api/proxy/[...path]/route.ts
export async function GET(
  request: Request,
  { params }: { params: { path: string[] } }
) {
  const path = params.path.join('/');
  const response = await fetch(`${process.env.CONDUIT_API_URL}/${path}`, {
    headers: {
      'Authorization': `Bearer ${process.env.CONDUIT_WEBUI_AUTH_KEY}`,
    },
  });
  
  const data = await response.json();
  return NextResponse.json(data);
}
```

**Solution 2: Configure CORS on Conduit server**

```csharp
// In your Conduit API Startup.cs
services.AddCors(options =>
{
    options.AddPolicy("NextJsApp",
        builder => builder
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
```

### Problem: Network timeouts

```
Error: timeout of 60000ms exceeded
```

**Solution:**

```typescript
// Increase timeout in client configuration
const adminClient = createAdminClient({
  baseUrl: process.env.CONDUIT_API_URL!,
  apiKey: process.env.CONDUIT_WEBUI_AUTH_KEY!,
  timeout: 120000, // 2 minutes
});

// Or configure React Query globally
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 3,
      retryDelay: attemptIndex => Math.min(1000 * 2 ** attemptIndex, 30000),
      networkMode: 'offlineFirst',
    },
  },
});
```

## TypeScript Errors

### Problem: Type inference not working

```typescript
// Types are 'any'
const { data } = useProviders();
```

**Solution 1: Update tsconfig.json**

```json
{
  "compilerOptions": {
    "strict": true,
    "moduleResolution": "node",
    "esModuleInterop": true,
    "skipLibCheck": true,
    "types": ["@conduit/admin-client", "@conduit/core-client"]
  }
}
```

**Solution 2: Explicitly type responses**

```typescript
import type { Provider } from '@conduit/admin-client/types';

const { data } = useProviders() as { data: Provider[] | undefined };
```

### Problem: Generic type errors with hooks

```
TS2345: Argument of type 'X' is not assignable to parameter of type 'Y'
```

**Solution:**

```typescript
// Explicitly type your mutations
const createKey = useCreateVirtualKey<CreateVirtualKeyInput>();

// Or type the hook response
interface KeyFormData {
  name: string;
  providers: string[];
}

const createKey = useCreateVirtualKey();
const handleSubmit = (data: KeyFormData) => {
  createKey.mutate(data);
};
```

## React Query Issues

### Problem: Queries not refetching

**Diagnosis:**

```typescript
// Add React Query Devtools
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';

export function Providers({ children }: Props) {
  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
```

**Solution:**

```typescript
// Force refetch
const { refetch } = useProviders();
await refetch();

// Or invalidate queries
const queryClient = useQueryClient();
queryClient.invalidateQueries({ queryKey: ['providers'] });

// Check stale time configuration
const { data } = useProviders({
  staleTime: 0, // Always considered stale
  cacheTime: 5 * 60 * 1000, // Cache for 5 minutes
});
```

### Problem: Optimistic updates not working

**Solution:**

```typescript
// Ensure you return context from onMutate
const updateKey = useUpdateVirtualKey();

updateKey.mutate(data, {
  onMutate: async (newData) => {
    // MUST await cancel queries
    await queryClient.cancelQueries({ queryKey: ['virtualKeys'] });
    
    const previousData = queryClient.getQueryData(['virtualKeys']);
    
    // MUST return context for rollback
    return { previousData };
  },
  onError: (err, newData, context) => {
    // Use returned context
    if (context?.previousData) {
      queryClient.setQueryData(['virtualKeys'], context.previousData);
    }
  },
});
```

### Problem: Infinite loops with queries

**Solution:**

```typescript
// ❌ Wrong - Creates new object every render
const { data } = useProviders({
  select: data => data.filter(p => p.enabled), // New function each time!
});

// ✅ Correct - Stable reference
const selectEnabled = useCallback(
  (data: Provider[]) => data.filter(p => p.enabled),
  []
);

const { data } = useProviders({ select: selectEnabled });

// Or use useMemo for derived data
const { data: allProviders } = useProviders();
const enabledProviders = useMemo(
  () => allProviders?.filter(p => p.enabled) ?? [],
  [allProviders]
);
```

## Real-time Connection Problems

### Problem: SignalR connection failing

```
Error: Failed to start the connection
```

**Solution 1: Check hub URL**

```typescript
// Debug connection URL
const hubUrl = `${process.env.NEXT_PUBLIC_CONDUIT_API_URL}/hubs/navigation-state`;
console.log('Connecting to:', hubUrl);

// Ensure no trailing slash
const baseUrl = process.env.NEXT_PUBLIC_CONDUIT_API_URL!.replace(/\/$/, '');
```

**Solution 2: Handle connection lifecycle**

```typescript
const [connectionState, setConnectionState] = useState<string>('disconnected');

useEffect(() => {
  const connection = new HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Debug) // Enable debug logging
    .build();
  
  connection.onreconnecting(() => setConnectionState('reconnecting'));
  connection.onreconnected(() => setConnectionState('connected'));
  connection.onclose(() => setConnectionState('disconnected'));
  
  connection.start()
    .then(() => setConnectionState('connected'))
    .catch(err => {
      console.error('SignalR connection error:', err);
      setConnectionState('error');
    });
    
  return () => {
    connection.stop();
  };
}, []);
```

### Problem: Missing real-time updates

**Solution:**

```typescript
// Ensure you're subscribed to the right events
connection.on('VirtualKeyUpdated', (data) => {
  console.log('Received update:', data);
  // Update your state
});

// Check you've joined the correct group
await connection.invoke('JoinVirtualKeyGroup', virtualKeyId);

// Verify server is sending updates
connection.on('Debug', console.log); // If server sends debug events
```

## Performance Issues

### Problem: Slow initial page load

**Solution 1: Code splitting**

```typescript
// Dynamic imports for heavy components
const HeavyDashboard = dynamic(
  () => import('./components/HeavyDashboard'),
  {
    loading: () => <DashboardSkeleton />,
    ssr: false, // Disable SSR for client-only components
  }
);
```

**Solution 2: Optimize bundle size**

```typescript
// Only import what you need
import { useProviders } from '@conduit/admin-client/react';
// NOT: import * as ConduitSDK from '@conduit/admin-client';

// Tree-shake unused code
// next.config.js
module.exports = {
  swcMinify: true,
  experimental: {
    optimizePackageImports: ['@conduit/admin-client'],
  },
};
```

### Problem: Too many API requests

**Solution:**

```typescript
// Batch requests where possible
const { data: dashboardData } = useQuery({
  queryKey: ['dashboard'],
  queryFn: async () => {
    // Single endpoint that returns all dashboard data
    const response = await fetch('/api/admin/dashboard');
    return response.json();
  },
  staleTime: 5 * 60 * 1000, // Consider fresh for 5 minutes
});

// Use React Query's built-in deduplication
// Multiple components can call useProviders()
// but only one request will be made
```

## Build & Deployment Errors

### Problem: Build fails with hydration errors

```
Error: Hydration failed because the initial UI does not match what was rendered on the server
```

**Solution:**

```typescript
// Ensure client-only code is wrapped properly
const [mounted, setMounted] = useState(false);

useEffect(() => {
  setMounted(true);
}, []);

if (!mounted) {
  return <LoadingSkeleton />; // Server-safe placeholder
}

// Client-only code here
return <ClientOnlyComponent />;
```

### Problem: Environment variables not available in production

**Solution:**

```typescript
// Vercel/Netlify: Add to dashboard
// Docker: Use build args

// next.config.js - Validate at build time
module.exports = {
  env: {
    NEXT_PUBLIC_CONDUIT_API_URL: process.env.NEXT_PUBLIC_CONDUIT_API_URL,
  },
  // Fail build if missing
  webpack: (config, { isServer }) => {
    if (!process.env.NEXT_PUBLIC_CONDUIT_API_URL) {
      throw new Error('Missing NEXT_PUBLIC_CONDUIT_API_URL');
    }
    return config;
  },
};
```

## Common Runtime Errors

### Problem: "Cannot read property 'x' of undefined"

**Solution:**

```typescript
// Always check for data existence
const { data } = useProvider(id);

// ❌ Wrong
return <h1>{data.name}</h1>;

// ✅ Correct
return <h1>{data?.name || 'Loading...'}</h1>;

// Or use early return
if (!data) return <Loading />;
return <h1>{data.name}</h1>;
```

### Problem: Memory leaks warning

```
Warning: Can't perform a React state update on an unmounted component
```

**Solution:**

```typescript
useEffect(() => {
  let cancelled = false;
  
  async function fetchData() {
    const result = await someAsyncOperation();
    
    if (!cancelled) {
      setState(result);
    }
  }
  
  fetchData();
  
  return () => {
    cancelled = true;
  };
}, []);
```

## Debugging Tools & Techniques

### 1. Enable React Query Devtools

```typescript
// app/providers.tsx
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';

export function Providers({ children }: Props) {
  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <ReactQueryDevtools 
        initialIsOpen={false}
        position="bottom-right"
      />
    </QueryClientProvider>
  );
}
```

### 2. Add comprehensive logging

```typescript
// lib/debug.ts
export const debugLog = (category: string, ...args: any[]) => {
  if (process.env.NODE_ENV === 'development') {
    console.log(`[${category}]`, ...args);
  }
};

// Usage
debugLog('AUTH', 'Checking authentication', { userId });
```

### 3. Network request interceptor

```typescript
// lib/api-client.ts
const client = axios.create({
  baseURL: process.env.NEXT_PUBLIC_CONDUIT_API_URL,
});

// Request interceptor
client.interceptors.request.use(
  (config) => {
    console.log('API Request:', config.method?.toUpperCase(), config.url);
    return config;
  },
  (error) => {
    console.error('API Request Error:', error);
    return Promise.reject(error);
  }
);

// Response interceptor
client.interceptors.response.use(
  (response) => {
    console.log('API Response:', response.status, response.config.url);
    return response;
  },
  (error) => {
    console.error('API Response Error:', error.response?.status, error.config?.url);
    return Promise.reject(error);
  }
);
```

### 4. Performance profiling

```typescript
// lib/performance.ts
export function measurePerformance(name: string, fn: () => Promise<any>) {
  return async (...args: any[]) => {
    const start = performance.now();
    try {
      const result = await fn(...args);
      const duration = performance.now() - start;
      console.log(`[PERF] ${name}: ${duration.toFixed(2)}ms`);
      return result;
    } catch (error) {
      const duration = performance.now() - start;
      console.error(`[PERF] ${name} failed after ${duration.toFixed(2)}ms`);
      throw error;
    }
  };
}

// Usage
const fetchProviders = measurePerformance(
  'fetchProviders',
  async () => adminClient.providers.list()
);
```

### 5. Error boundary with reporting

```typescript
// app/components/error-boundary.tsx
class ErrorBoundary extends React.Component<Props, State> {
  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Error caught by boundary:', error, errorInfo);
    
    // Send to error reporting service
    if (typeof window !== 'undefined' && window.Sentry) {
      window.Sentry.captureException(error, {
        contexts: {
          react: {
            componentStack: errorInfo.componentStack,
          },
        },
      });
    }
  }
  
  render() {
    if (this.state.hasError) {
      return (
        <div className="error-fallback">
          <h2>Something went wrong</h2>
          <details>
            <summary>Error details</summary>
            <pre>{this.state.error?.toString()}</pre>
          </details>
          <button onClick={() => window.location.reload()}>
            Reload page
          </button>
        </div>
      );
    }
    
    return this.props.children;
  }
}
```

## Quick Fixes Checklist

When encountering issues, check these common problems first:

- [ ] Environment variables are set correctly
- [ ] Using correct SDK (admin vs core) in the right context
- [ ] API server is running and accessible
- [ ] CORS is configured if needed
- [ ] Authentication keys are valid and not expired
- [ ] TypeScript strict mode is enabled
- [ ] React Query is configured properly
- [ ] Network requests are going to the correct URL
- [ ] Error boundaries are in place
- [ ] Loading and error states are handled

---

Last updated: 2025-01-08