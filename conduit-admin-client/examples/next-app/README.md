# Conduit Admin Client - Next.js Example

This example demonstrates how to use the Conduit Admin Client in a Next.js application.

## Setup

1. Install dependencies:
```bash
npm install @conduit/admin-client
```

2. Set environment variables in `.env.local`:
```
CONDUIT_MASTER_KEY=your-master-key
CONDUIT_ADMIN_API_URL=http://localhost:5002
```

## Server-Side Usage (App Router)

```typescript
// app/api/keys/route.ts
import { ConduitAdminClient } from '@conduit/admin-client';
import { NextResponse } from 'next/server';

const client = ConduitAdminClient.fromEnvironment();

export async function GET() {
  try {
    const keys = await client.virtualKeys.list({
      pageSize: 20,
      isEnabled: true,
    });
    return NextResponse.json(keys);
  } catch (error) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const result = await client.virtualKeys.create(body);
    return NextResponse.json(result, { status: 201 });
  } catch (error) {
    return NextResponse.json({ error: error.message }, { status: 400 });
  }
}
```

## Server Components

```typescript
// app/dashboard/page.tsx
import { ConduitAdminClient } from '@conduit/admin-client';

const client = ConduitAdminClient.fromEnvironment();

export default async function DashboardPage() {
  const [costSummary, health, keys] = await Promise.all([
    client.analytics.getTodayCosts(),
    client.system.getHealth(),
    client.virtualKeys.list({ pageSize: 5 }),
  ]);

  return (
    <div>
      <h1>Conduit Dashboard</h1>
      
      <section>
        <h2>Today's Costs</h2>
        <p>Total: ${costSummary.totalCost.toFixed(2)}</p>
      </section>

      <section>
        <h2>System Health</h2>
        <p>Status: {health.status}</p>
      </section>

      <section>
        <h2>Recent Keys</h2>
        <ul>
          {keys.items.map(key => (
            <li key={key.id}>
              {key.keyName} - ${key.currentSpend}/${key.maxBudget}
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
```

## API Wrapper Pattern

```typescript
// lib/conduit.ts
import { ConduitAdminClient } from '@conduit/admin-client';
import { cache } from 'react';

let client: ConduitAdminClient;

export function getClient() {
  if (!client) {
    client = ConduitAdminClient.fromEnvironment();
  }
  return client;
}

// Cache results for server components
export const getVirtualKeys = cache(async () => {
  const client = getClient();
  return client.virtualKeys.list({ pageSize: 100 });
});

export const getCostSummary = cache(async (days = 30) => {
  const client = getClient();
  const endDate = new Date();
  const startDate = new Date();
  startDate.setDate(startDate.getDate() - days);
  
  return client.analytics.getCostSummary({
    startDate: startDate.toISOString(),
    endDate: endDate.toISOString(),
  });
});
```

## Client Components with Server Actions

```typescript
// app/keys/KeyManager.tsx
'use client';

import { useState } from 'react';
import { createKey, deleteKey } from './actions';

export function KeyManager() {
  const [keyName, setKeyName] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    
    try {
      const result = await createKey({
        keyName,
        maxBudget: 100,
        budgetDuration: 'Monthly',
      });
      alert(`Created key: ${result.virtualKey}`);
      setKeyName('');
    } catch (error) {
      alert(`Error: ${error.message}`);
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleCreate}>
      <input
        type="text"
        value={keyName}
        onChange={(e) => setKeyName(e.target.value)}
        placeholder="Key name"
        required
      />
      <button type="submit" disabled={loading}>
        Create Key
      </button>
    </form>
  );
}

// app/keys/actions.ts
'use server';

import { getClient } from '@/lib/conduit';
import { revalidatePath } from 'next/cache';

export async function createKey(data: any) {
  const client = getClient();
  const result = await client.virtualKeys.create(data);
  revalidatePath('/keys');
  return result;
}

export async function deleteKey(id: number) {
  const client = getClient();
  await client.virtualKeys.delete(id);
  revalidatePath('/keys');
}
```

## Middleware for Admin Protection

```typescript
// middleware.ts
import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';
import { ConduitAdminClient } from '@conduit/admin-client';

export async function middleware(request: NextRequest) {
  // Protect /admin routes
  if (request.nextUrl.pathname.startsWith('/admin')) {
    const masterKey = request.headers.get('x-master-key');
    
    if (!masterKey) {
      return NextResponse.json(
        { error: 'Master key required' },
        { status: 401 }
      );
    }

    try {
      const client = new ConduitAdminClient({
        masterKey,
        adminApiUrl: process.env.CONDUIT_ADMIN_API_URL!,
      });
      
      // Validate by attempting to get system info
      await client.system.getSystemInfo();
    } catch (error) {
      return NextResponse.json(
        { error: 'Invalid master key' },
        { status: 403 }
      );
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: '/admin/:path*',
};
```

## Real-time Updates with Polling

```typescript
// app/monitoring/HealthMonitor.tsx
'use client';

import { useEffect, useState } from 'react';

export function HealthMonitor() {
  const [health, setHealth] = useState(null);

  useEffect(() => {
    async function checkHealth() {
      const res = await fetch('/api/health');
      const data = await res.json();
      setHealth(data);
    }

    checkHealth();
    const interval = setInterval(checkHealth, 30000); // Every 30 seconds

    return () => clearInterval(interval);
  }, []);

  if (!health) return <div>Loading...</div>;

  return (
    <div className={`health-status ${health.status}`}>
      System Status: {health.status}
    </div>
  );
}

// app/api/health/route.ts
import { getClient } from '@/lib/conduit';

export async function GET() {
  const client = getClient();
  const health = await client.system.getHealth();
  return Response.json(health);
}
```

## Error Handling

```typescript
// lib/error-handler.ts
import { 
  ConduitError,
  ValidationError,
  AuthenticationError,
  NotFoundError 
} from '@conduit/admin-client';

export function handleConduitError(error: unknown): {
  message: string;
  status: number;
} {
  if (error instanceof ValidationError) {
    return {
      message: error.message,
      status: 400,
    };
  }
  
  if (error instanceof AuthenticationError) {
    return {
      message: 'Authentication failed',
      status: 401,
    };
  }
  
  if (error instanceof NotFoundError) {
    return {
      message: 'Resource not found',
      status: 404,
    };
  }
  
  if (error instanceof ConduitError) {
    return {
      message: error.message,
      status: error.statusCode || 500,
    };
  }
  
  return {
    message: 'Internal server error',
    status: 500,
  };
}
```

## Tips

1. **Environment Variables**: Always use environment variables for sensitive data
2. **Caching**: Use React's `cache()` function for server components
3. **Error Boundaries**: Implement error boundaries for client components
4. **Loading States**: Show loading indicators during API calls
5. **Revalidation**: Use `revalidatePath()` after mutations
6. **Type Safety**: Leverage TypeScript for all API interactions