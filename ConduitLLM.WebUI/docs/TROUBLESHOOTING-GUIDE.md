# Conduit WebUI Troubleshooting Guide

## Overview

This guide helps diagnose and resolve common issues when working with the Conduit WebUI and SDK integration. Each section includes symptoms, causes, and step-by-step solutions.

## Table of Contents

1. [Connection Issues](#connection-issues)
2. [Authentication Problems](#authentication-problems)
3. [SDK Client Errors](#sdk-client-errors)
4. [SignalR/Real-time Issues](#signalr-real-time-issues)
5. [Performance Problems](#performance-problems)
6. [API Response Errors](#api-response-errors)
7. [Build and Deployment Issues](#build-and-deployment-issues)
8. [Database and Caching](#database-and-caching)
9. [Debugging Tools](#debugging-tools)
10. [Common Error Codes](#common-error-codes)

## Connection Issues

### Cannot Connect to Core/Admin API

**Symptoms:**
- `ECONNREFUSED` errors
- `fetch failed` messages
- 502 Bad Gateway responses

**Diagnosis:**
```bash
# Check if APIs are running
curl http://localhost:5000/v1/health
curl http://localhost:5002/v1/health

# Check Docker containers
docker ps

# Check network connectivity
ping api.yourdomain.com
```

**Solutions:**

1. **Local Development:**
```bash
# Ensure APIs are running
docker-compose up -d api admin

# Check logs
docker-compose logs api
docker-compose logs admin
```

2. **Environment Variables:**
```typescript
// Verify configuration
console.log('API URLs:', {
  core: process.env.CONDUIT_API_BASE_URL,
  admin: process.env.CONDUIT_ADMIN_API_BASE_URL,
  corePublic: process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL,
  adminPublic: process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL,
});
```

3. **Docker Networking:**
```yaml
# Ensure services are on same network
services:
  webui:
    networks:
      - conduit-network
  api:
    networks:
      - conduit-network

networks:
  conduit-network:
    driver: bridge
```

### CORS Errors

**Symptoms:**
- `Access-Control-Allow-Origin` errors
- Blocked cross-origin requests

**Solutions:**

1. **Configure CORS in API:**
```typescript
// In your API configuration
app.use(cors({
  origin: process.env.ALLOWED_ORIGINS?.split(',') || '*',
  credentials: true,
  methods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
  allowedHeaders: ['Content-Type', 'Authorization', 'x-virtual-key'],
}));
```

2. **Proxy Configuration:**
```javascript
// next.config.js
module.exports = {
  async rewrites() {
    return [
      {
        source: '/api/proxy/core/:path*',
        destination: `${process.env.CONDUIT_API_BASE_URL}/:path*`,
      },
      {
        source: '/api/proxy/admin/:path*',
        destination: `${process.env.CONDUIT_ADMIN_API_BASE_URL}/:path*`,
      },
    ];
  },
};
```

## Authentication Problems

### Session Not Persisting

**Symptoms:**
- Logged out after refresh
- `Unauthorized` errors after login
- Session cookie missing

**Diagnosis:**
```typescript
// Check session
const session = await getServerSession();
console.log('Session:', session);

// Check cookies
console.log('Cookies:', request.cookies.getAll());
```

**Solutions:**

1. **NextAuth Configuration:**
```typescript
// lib/auth/config.ts
export const authOptions: NextAuthOptions = {
  session: {
    strategy: 'jwt',
    maxAge: 30 * 24 * 60 * 60, // 30 days
  },
  cookies: {
    sessionToken: {
      name: `__Secure-next-auth.session-token`,
      options: {
        httpOnly: true,
        sameSite: 'lax',
        path: '/',
        secure: process.env.NODE_ENV === 'production',
      },
    },
  },
};
```

2. **Check Secret Configuration:**
```bash
# Generate new secret
openssl rand -base64 32

# Set in environment
NEXTAUTH_SECRET=your-generated-secret
```

### Master Key Not Working

**Symptoms:**
- 401 errors on admin endpoints
- "Invalid master key" messages

**Solutions:**

1. **Verify Key Format:**
```typescript
// Ensure proper format
const masterKey = process.env.CONDUIT_MASTER_KEY;
console.log('Master key format:', {
  length: masterKey?.length,
  prefix: masterKey?.substring(0, 5),
  hasBearer: masterKey?.startsWith('Bearer'),
});
```

2. **Check Admin Client:**
```typescript
// Test admin client directly
const adminClient = getServerAdminClient();
try {
  const result = await adminClient.health.check();
  console.log('Admin client working:', result);
} catch (error) {
  console.error('Admin client error:', error);
}
```

## SDK Client Errors

### Client Initialization Failures

**Symptoms:**
- `Cannot read properties of undefined`
- SDK client is null/undefined

**Diagnosis:**
```typescript
// Debug client initialization
try {
  const client = getServerCoreClient(virtualKey);
  console.log('Client initialized:', !!client);
  console.log('Client config:', client.config);
} catch (error) {
  console.error('Client init error:', error);
}
```

**Solutions:**

1. **Check Environment:**
```typescript
// Validate required environment variables
const required = [
  'CONDUIT_API_BASE_URL',
  'CONDUIT_ADMIN_API_BASE_URL',
  'CONDUIT_MASTER_KEY',
];

const missing = required.filter(key => !process.env[key]);
if (missing.length > 0) {
  throw new Error(`Missing environment variables: ${missing.join(', ')}`);
}
```

2. **Connection Pool Issues:**
```typescript
// Clear connection pool if needed
export function clearClientPool() {
  clientPool.clear();
  console.log('Client pool cleared');
}

// Monitor pool size
export function getPoolStats() {
  return {
    size: clientPool.size,
    clients: Array.from(clientPool.entries()).map(([key, { lastUsed }]) => ({
      key,
      age: Date.now() - lastUsed,
    })),
  };
}
```

### Type Errors with SDK

**Symptoms:**
- TypeScript compilation errors
- Type mismatches with SDK methods

**Solutions:**

1. **Update SDK Types:**
```bash
# Update to latest SDK version
npm update @knn_labs/conduit-core-client @knn_labs/conduit-admin-client

# Regenerate types
npm run build
```

2. **Type Assertions:**
```typescript
// Use type assertions when needed
import type { VirtualKey, ChatCompletionRequest } from '@knn_labs/conduit-core-client';

const request = body as ChatCompletionRequest;
const key = result as VirtualKey;
```

## SignalR/Real-time Issues

### SignalR Not Connecting

**Symptoms:**
- No real-time updates
- WebSocket connection failures
- Fallback to polling

**Diagnosis:**
```typescript
// Check SignalR status
const { isConnected, connectionStatus } = useRealTimeStatus();
console.log('SignalR status:', { isConnected, connectionStatus });

// Browser console
// Look for WebSocket errors
```

**Solutions:**

1. **WebSocket Support:**
```nginx
# Nginx configuration for WebSocket
location /hubs/ {
    proxy_pass http://backend;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_read_timeout 86400;
}
```

2. **Fallback Configuration:**
```typescript
// Configure transports
const config: SDKSignalRConfig = {
  coreApiUrl: process.env.NEXT_PUBLIC_CONDUIT_API_EXTERNAL_URL,
  reconnectInterval: [0, 2000, 10000, 30000, 60000],
  transports: ['WebSockets', 'ServerSentEvents', 'LongPolling'],
};
```

3. **Debug SignalR:**
```typescript
// Enable SignalR logging
import { LogLevel } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
  .withUrl(hubUrl)
  .configureLogging(LogLevel.Debug)
  .build();
```

### Real-time Updates Not Working

**Symptoms:**
- Events not triggering
- Stale data in UI
- Missing notifications

**Solutions:**

1. **Check Event Registration:**
```typescript
// Verify event handlers are registered
useEffect(() => {
  const signalRManager = getSDKSignalRManager();
  
  // Add debug logging
  const originalHandler = handleNavigationStateUpdate;
  const debugHandler = (update: NavigationStateUpdate) => {
    console.log('Navigation update received:', update);
    originalHandler(update);
  };
  
  signalRManager.on('onNavigationStateUpdate', debugHandler);
  
  return () => {
    signalRManager.off('onNavigationStateUpdate');
  };
}, []);
```

2. **Verify Server Events:**
```csharp
// Server-side: Ensure events are published
await _hubContext.Clients.All.SendAsync("NavigationStateUpdate", updateData);
```

## Performance Problems

### Slow API Responses

**Symptoms:**
- Long loading times
- Timeouts
- Poor user experience

**Diagnosis:**
```typescript
// Measure API response times
const start = Date.now();
const result = await withSDKErrorHandling(
  async () => client.someOperation(),
  'operation'
);
const duration = Date.now() - start;
console.log(`Operation took ${duration}ms`);
```

**Solutions:**

1. **Enable Caching:**
```typescript
// Implement response caching
const cacheKey = `resource:${id}`;
const cached = await redis.get(cacheKey);

if (cached) {
  return transformSDKResponse(JSON.parse(cached), {
    meta: { cached: true },
  });
}

const result = await fetchResource(id);
await redis.setex(cacheKey, 300, JSON.stringify(result));
```

2. **Query Optimization:**
```typescript
// Use pagination
const result = await adminClient.virtualKeys.list({
  page: 1,
  pageSize: 20, // Limit page size
  select: ['id', 'name', 'status'], // Only needed fields
});
```

3. **Connection Pooling:**
```typescript
// Monitor connection pool performance
setInterval(() => {
  const stats = getPoolStats();
  console.log('Connection pool:', {
    activeConnections: stats.size,
    oldestConnection: Math.max(...stats.clients.map(c => c.age)),
  });
}, 60000);
```

### Memory Leaks

**Symptoms:**
- Increasing memory usage
- Application crashes
- Performance degradation

**Solutions:**

1. **Clean Up Subscriptions:**
```typescript
// Always clean up in useEffect
useEffect(() => {
  const subscription = eventEmitter.on('event', handler);
  
  return () => {
    subscription.unsubscribe();
  };
}, []);
```

2. **Monitor Memory:**
```typescript
// Add memory monitoring endpoint
export async function GET() {
  const usage = process.memoryUsage();
  
  return new Response(
    JSON.stringify({
      rss: `${Math.round(usage.rss / 1024 / 1024)}MB`,
      heapTotal: `${Math.round(usage.heapTotal / 1024 / 1024)}MB`,
      heapUsed: `${Math.round(usage.heapUsed / 1024 / 1024)}MB`,
      external: `${Math.round(usage.external / 1024 / 1024)}MB`,
    }),
    { headers: { 'Content-Type': 'application/json' } }
  );
}
```

## API Response Errors

### 500 Internal Server Errors

**Diagnosis:**
```typescript
// Enhanced error logging
export async function withSDKErrorHandling<T>(
  operation: () => Promise<T>,
  context: string
): Promise<T> {
  try {
    return await operation();
  } catch (error: any) {
    logger.error(`SDK operation failed: ${context}`, {
      error: {
        message: error.message,
        code: error.code,
        statusCode: error.statusCode,
        stack: error.stack,
        details: error.details,
      },
    });
    throw error;
  }
}
```

**Solutions:**

1. **Add Request Logging:**
```typescript
// Log all requests for debugging
export function createLoggingMiddleware() {
  return async (request: NextRequest) => {
    const start = Date.now();
    const requestId = crypto.randomUUID();
    
    logger.info('Request started', {
      requestId,
      method: request.method,
      url: request.url,
      headers: Object.fromEntries(request.headers.entries()),
    });
    
    try {
      const response = await next(request);
      const duration = Date.now() - start;
      
      logger.info('Request completed', {
        requestId,
        status: response.status,
        duration,
      });
      
      return response;
    } catch (error) {
      logger.error('Request failed', {
        requestId,
        error,
      });
      throw error;
    }
  };
}
```

### Rate Limiting Errors

**Symptoms:**
- 429 Too Many Requests
- `Rate limit exceeded` messages

**Solutions:**

1. **Implement Retry Logic:**
```typescript
// Exponential backoff retry
export async function retryWithBackoff<T>(
  operation: () => Promise<T>,
  maxRetries = 3
): Promise<T> {
  let lastError: any;
  
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await operation();
    } catch (error: any) {
      lastError = error;
      
      if (error.statusCode === 429) {
        const retryAfter = error.headers?.['retry-after'] || Math.pow(2, i);
        await new Promise(resolve => setTimeout(resolve, retryAfter * 1000));
      } else {
        throw error;
      }
    }
  }
  
  throw lastError;
}
```

2. **Request Queuing:**
```typescript
// Queue requests to avoid rate limits
class RequestQueue {
  private queue: Array<() => Promise<any>> = [];
  private processing = false;
  private requestsPerMinute = 60;
  
  async add<T>(request: () => Promise<T>): Promise<T> {
    return new Promise((resolve, reject) => {
      this.queue.push(async () => {
        try {
          const result = await request();
          resolve(result);
        } catch (error) {
          reject(error);
        }
      });
      
      this.process();
    });
  }
  
  private async process() {
    if (this.processing) return;
    this.processing = true;
    
    while (this.queue.length > 0) {
      const request = this.queue.shift()!;
      await request();
      await new Promise(resolve => 
        setTimeout(resolve, 60000 / this.requestsPerMinute)
      );
    }
    
    this.processing = false;
  }
}
```

## Build and Deployment Issues

### Build Failures

**Symptoms:**
- TypeScript errors
- Module not found
- Build timeouts

**Solutions:**

1. **Clear Build Cache:**
```bash
# Clear Next.js cache
rm -rf .next
rm -rf node_modules/.cache

# Reinstall dependencies
rm -rf node_modules package-lock.json
npm install

# Rebuild
npm run build
```

2. **Check TypeScript:**
```bash
# Run type checking
npm run type-check

# Fix common issues
npm run lint -- --fix
```

3. **Memory Issues:**
```bash
# Increase Node memory for build
NODE_OPTIONS="--max-old-space-size=4096" npm run build
```

### Environment Variable Issues

**Diagnosis:**
```typescript
// Debug environment variables
console.log('Build-time env:', {
  NODE_ENV: process.env.NODE_ENV,
  NEXT_PUBLIC_vars: Object.keys(process.env)
    .filter(key => key.startsWith('NEXT_PUBLIC_'))
    .reduce((acc, key) => ({ ...acc, [key]: process.env[key] }), {}),
});
```

**Solutions:**

1. **Validate at Build Time:**
```javascript
// next.config.js
module.exports = {
  env: {
    REQUIRED_VAR: process.env.REQUIRED_VAR || (() => {
      throw new Error('REQUIRED_VAR is not set');
    })(),
  },
};
```

2. **Runtime Validation:**
```typescript
// lib/config/validate.ts
export function validateEnvironment() {
  const required = {
    CONDUIT_API_BASE_URL: process.env.CONDUIT_API_BASE_URL,
    CONDUIT_MASTER_KEY: process.env.CONDUIT_MASTER_KEY,
  };
  
  const missing = Object.entries(required)
    .filter(([_, value]) => !value)
    .map(([key]) => key);
  
  if (missing.length > 0) {
    throw new Error(
      `Missing required environment variables: ${missing.join(', ')}\n` +
      `Please check your .env.local file`
    );
  }
}
```

## Database and Caching

### Redis Connection Issues

**Symptoms:**
- `ECONNREFUSED` to Redis
- Cache misses
- Session issues

**Solutions:**

1. **Test Redis Connection:**
```typescript
// lib/redis/health.ts
import { createClient } from 'redis';

export async function checkRedisHealth() {
  const client = createClient({
    url: process.env.REDIS_URL,
  });
  
  try {
    await client.connect();
    await client.ping();
    await client.disconnect();
    return { status: 'healthy' };
  } catch (error) {
    return { status: 'unhealthy', error: error.message };
  }
}
```

2. **Fallback Strategy:**
```typescript
// Graceful degradation without Redis
class CacheService {
  private redis: RedisClient | null = null;
  private inMemoryCache = new Map();
  
  async get(key: string): Promise<any> {
    try {
      if (this.redis) {
        return await this.redis.get(key);
      }
    } catch (error) {
      console.warn('Redis get failed, using memory cache:', error);
    }
    
    return this.inMemoryCache.get(key);
  }
  
  async set(key: string, value: any, ttl?: number): Promise<void> {
    try {
      if (this.redis) {
        await this.redis.setex(key, ttl || 3600, JSON.stringify(value));
      }
    } catch (error) {
      console.warn('Redis set failed, using memory cache:', error);
    }
    
    this.inMemoryCache.set(key, value);
    if (ttl) {
      setTimeout(() => this.inMemoryCache.delete(key), ttl * 1000);
    }
  }
}
```

## Debugging Tools

### Enable Debug Logging

```typescript
// lib/utils/debug.ts
const DEBUG = process.env.NODE_ENV === 'development';

export function debug(namespace: string) {
  return (...args: any[]) => {
    if (DEBUG || process.env.DEBUG?.includes(namespace)) {
      console.log(`[${namespace}]`, ...args);
    }
  };
}

// Usage
const debugAuth = debug('auth');
debugAuth('Session validated:', session);
```

### Request Tracing

```typescript
// middleware.ts
import { NextResponse } from 'next/server';

export function middleware(request: NextRequest) {
  const requestId = crypto.randomUUID();
  const start = Date.now();
  
  // Add request ID to headers
  const response = NextResponse.next();
  response.headers.set('x-request-id', requestId);
  
  // Log request
  console.log(`[${requestId}] ${request.method} ${request.url}`);
  
  // Log response time
  response.headers.set('x-response-time', `${Date.now() - start}ms`);
  
  return response;
}
```

### Browser DevTools

```typescript
// Add debug helpers to window
if (typeof window !== 'undefined' && process.env.NODE_ENV === 'development') {
  (window as any).conduitDebug = {
    getClients: () => ({
      core: coreClient,
      admin: adminClient,
    }),
    getStores: () => ({
      auth: useAuthStore.getState(),
      connection: useConnectionStore.getState(),
    }),
    clearCache: () => {
      queryClient.clear();
      console.log('Query cache cleared');
    },
    testAPI: async (endpoint: string) => {
      const response = await fetch(endpoint);
      console.log('Response:', response.status, await response.json());
    },
  };
}
```

## Common Error Codes

### SDK Error Codes

| Code | Description | Solution |
|------|-------------|----------|
| `UNAUTHORIZED` | Invalid or missing authentication | Check API keys and session |
| `FORBIDDEN` | Insufficient permissions | Verify user permissions |
| `NOT_FOUND` | Resource doesn't exist | Check resource ID |
| `RATE_LIMITED` | Too many requests | Implement retry with backoff |
| `INVALID_REQUEST` | Malformed request | Validate input data |
| `INTERNAL_ERROR` | Server error | Check server logs |
| `TIMEOUT` | Request timeout | Increase timeout or optimize |
| `CONNECTION_ERROR` | Network issue | Check connectivity |

### HTTP Status Codes

| Status | Description | Common Causes |
|--------|-------------|---------------|
| 400 | Bad Request | Invalid input, missing fields |
| 401 | Unauthorized | Invalid/expired token |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Wrong endpoint/resource |
| 429 | Too Many Requests | Rate limiting |
| 500 | Internal Server Error | Server crash/bug |
| 502 | Bad Gateway | Proxy/upstream error |
| 503 | Service Unavailable | Server overload/maintenance |

## Getting Help

### Gathering Information

When reporting issues, include:

1. **Environment Info:**
```bash
npm run diagnostics
```

2. **Error Details:**
- Full error message and stack trace
- Request/response details
- Browser console logs
- Network tab screenshots

3. **Reproduction Steps:**
- Minimal code to reproduce
- Environment configuration
- Expected vs actual behavior

### Support Channels

1. **GitHub Issues**: Bug reports and feature requests
2. **Documentation**: Check latest docs
3. **Community Forums**: Ask questions
4. **Support Email**: For critical issues

## Conclusion

This troubleshooting guide covers the most common issues with the Conduit WebUI. Remember to:

1. **Check logs first** - Most issues leave traces
2. **Isolate the problem** - Test components individually
3. **Use debugging tools** - Browser DevTools, logging
4. **Keep dependencies updated** - Many issues are fixed in newer versions
5. **Document solutions** - Help others with similar issues

For issues not covered here, please contribute to this guide by submitting a pull request.