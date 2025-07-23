# Actual Request Flow in ConduitLLM WebUI

This document explains how requests actually flow through the system, without the marketing bullshit.

## The Real Architecture

```
Browser → Next.js Route → Admin SDK → Admin API (port 5002) → Database
```

## Example: Testing a Provider Connection

### 1. User clicks "Test connection" button
- File: `src/app/llm-providers/page.tsx`
- Calls: `testProvider.mutateAsync(provider.id)`

### 2. Frontend Hook
- File: `src/hooks/api/useAdminApi.ts` (line ~799)
- Makes POST request to: `/api/providers-test/${providerId}`

### 3. Next.js API Route
- File: `src/app/api/providers-test/[id]/route.ts`
- Does:
  1. Checks authentication (or redirects to login)
  2. Parses provider ID from URL
  3. Calls Admin SDK: `adminClient.providers.testConnectionById(numericId)`

### 4. Admin SDK
- File: `SDKs/Node/Admin/src/services/ProviderService.ts`
- Makes HTTP request to: `POST /api/providercredentials/test/{id}`
- Target: Admin API on port 5002

### 5. Admin API (.NET)
- Project: `ConduitLLM.Admin`
- Controller: `ProviderCredentialsController`
- Method: `TestProviderConnection(int id)`
- Actually tests the provider connection

## Common Issues

### "Unexpected token '<'" Error
- **Cause**: Getting HTML instead of JSON (usually a 404 page)
- **Why**: Route not found, authentication failed, or middleware redirect
- **Fix**: Check the route exists, restart dev server, check authentication

### 404 Errors on API Routes
- **Cause**: Next.js hasn't picked up new route files
- **Fix**: Restart the dev server
- **Docker Fix**: Rebuild the container (routes are baked into the image)

### Authentication Loops
- **Cause**: Middleware redirecting API routes to login page
- **Fix**: API routes should return 401 JSON, not redirect

## Debugging Tips

1. **Check the actual URL being called**
   - Open browser DevTools Network tab
   - Look at the request URL and response

2. **Add console.log everywhere**
   - In the route handler
   - In the SDK calls
   - In the middleware

3. **Check Docker logs**
   ```bash
   docker logs conduit-webui-1
   docker logs conduit-admin-1
   ```

4. **Verify the route exists**
   ```bash
   npm run build
   # Look for your route in the output
   ```

## Services and Ports

- **WebUI**: http://localhost:3000 (Next.js)
- **Admin API**: http://localhost:5002 (.NET)
- **Core API**: http://localhost:5000 (.NET)
- **PostgreSQL**: localhost:5432
- **Redis**: localhost:6379
- **RabbitMQ**: localhost:5672 (AMQP), localhost:15672 (Management)

## Environment Variables

### WebUI (.env.local)
```
NEXT_PUBLIC_ADMIN_API_URL=http://localhost:5002
NEXT_PUBLIC_CORE_API_URL=http://localhost:5000
CONDUIT_ADMIN_API_KEY=your-admin-key
```

### Admin API
```
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=alpha
DATABASE_URL=postgresql://conduit:conduitpass@postgres:5432/conduitdb
```

## The Truth About the Architecture

1. **It's overcomplicated** - Too many abstraction layers for an internal tool
2. **The SDK adds no value** - Just makes debugging harder
3. **Docker makes it worse** - Can't see changes without rebuilding
4. **The middleware is problematic** - Redirects API calls to HTML pages

## Quick Fixes When Shit Breaks

1. **Restart everything**
   ```bash
   docker compose down
   docker compose up -d
   ```

2. **Rebuild the WebUI**
   ```bash
   docker compose build webui
   docker compose up -d webui
   ```

3. **Check if services are healthy**
   ```bash
   docker ps
   # Look for (healthy) or (unhealthy)
   ```

4. **Nuclear option**
   ```bash
   docker compose down -v
   docker compose build --no-cache
   docker compose up -d
   ```