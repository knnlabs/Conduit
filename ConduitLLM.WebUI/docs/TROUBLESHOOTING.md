# Troubleshooting Guide

## Common Issues and Solutions

### Authentication Issues

#### Problem: "No virtual key available" error
**Symptoms:**
- API calls fail with authentication errors
- Virtual key is null in auth store

**Solutions:**
1. Check if logged in properly:
   ```typescript
   // In browser console
   const authStore = window.__ZUSTAND_DEVTOOLS__?.stores.get('authStore');
   console.log(authStore?.getState());
   ```

2. Verify WebUI virtual key exists:
   - Navigate to Virtual Keys page
   - Look for "WebUI Admin Access" key
   - If missing, log out and log back in

3. Force key recreation:
   ```typescript
   // Log out and clear storage
   localStorage.clear();
   sessionStorage.clear();
   window.location.href = '/login';
   ```

#### Problem: "Invalid admin key" on login
**Symptoms:**
- Cannot log into WebUI
- 401 error on login attempt

**Solutions:**
1. Verify `CONDUIT_WEBUI_AUTH_KEY` is set:
   ```bash
   # Check environment variable
   echo $CONDUIT_WEBUI_AUTH_KEY
   ```

2. Ensure keys are different:
   - `CONDUIT_WEBUI_AUTH_KEY` ≠ `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`
   - These must be different values

3. Check for trailing spaces or quotes in env file

### SDK Hook Issues

#### Problem: Hooks return undefined
**Symptoms:**
- `useProviders()` returns undefined
- No network requests in DevTools

**Solutions:**
1. Ensure providers are wrapped correctly:
   ```typescript
   // Check app layout includes ConduitProviders
   <ConduitProviders>
     {children}
   </ConduitProviders>
   ```

2. Verify virtual key is available:
   ```typescript
   const virtualKey = useAuthStore(state => state.virtualKey);
   console.log('Virtual Key:', virtualKey);
   ```

3. Check provider configuration:
   ```typescript
   // Ensure baseUrl is set correctly
   console.log('Core API URL:', process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL);
   console.log('Admin API URL:', process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL);
   ```

#### Problem: TypeScript errors with SDK hooks
**Symptoms:**
- Type errors when using SDK hooks
- Parameter type mismatches

**Solutions:**
1. Update to latest SDK versions:
   ```bash
   npm update @knn_labs/conduit-core-client @knn_labs/conduit-admin-client
   ```

2. Check for breaking changes in SDK changelog

3. Use correct import paths:
   ```typescript
   // Correct
   import { useProviders } from '@knn_labs/conduit-admin-client/react-query';
   
   // Incorrect
   import { useProviders } from '@knn_labs/conduit-admin-client';
   ```

### Rate Limiting Issues

#### Problem: 429 Too Many Requests errors
**Symptoms:**
- API returns 429 status
- "Too many requests" error messages

**Solutions:**
1. Check rate limit headers:
   ```typescript
   // In network tab, check response headers
   X-RateLimit-Limit: 100
   X-RateLimit-Remaining: 0
   X-RateLimit-Reset: 2024-01-01T00:00:00Z
   ```

2. Implement retry logic:
   ```typescript
   const { data } = useProviders({
     retry: (failureCount, error) => {
       if (error.status === 429) {
         const retryAfter = error.headers?.['retry-after'] || 60;
         setTimeout(() => {}, retryAfter * 1000);
         return true;
       }
       return failureCount < 3;
     }
   });
   ```

3. Reduce request frequency or batch operations

### Network Issues

#### Problem: CORS errors in browser
**Symptoms:**
- "Access-Control-Allow-Origin" errors
- Blocked cross-origin requests

**Solutions:**
1. Verify API URLs are correct:
   ```typescript
   // Should match your API deployment
   NEXT_PUBLIC_CONDUIT_CORE_API_URL=https://api.example.com
   ```

2. Check API CORS configuration allows WebUI origin

3. Use proxy in development:
   ```javascript
   // next.config.js
   module.exports = {
     async rewrites() {
       return [
         {
           source: '/api/conduit/:path*',
           destination: 'http://localhost:5000/:path*'
         }
       ];
     }
   };
   ```

#### Problem: "Failed to fetch" errors
**Symptoms:**
- Network errors with no response
- ERR_CONNECTION_REFUSED

**Solutions:**
1. Verify APIs are running:
   ```bash
   curl http://localhost:5000/health
   curl http://localhost:5002/health
   ```

2. Check Docker containers if using Docker:
   ```bash
   docker ps
   docker logs conduit-core-api
   docker logs conduit-admin-api
   ```

3. Verify network connectivity between services

### Session Issues

#### Problem: Session expires unexpectedly
**Symptoms:**
- Logged out randomly
- "Session expired" messages

**Solutions:**
1. Check session refresh is working:
   ```typescript
   // Should see refresh calls in network tab
   // Look for calls to /api/auth/refresh
   ```

2. Verify session cookie settings:
   ```typescript
   // In Application > Cookies
   // Check conduit_session cookie exists
   // Verify expiry time is correct
   ```

3. Check for clock skew between client and server

### Performance Issues

#### Problem: Slow API responses
**Symptoms:**
- Long loading times
- Timeouts on requests

**Solutions:**
1. Enable React Query DevTools:
   ```typescript
   import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
   
   // Add to your app
   <ReactQueryDevtools initialIsOpen={false} />
   ```

2. Check for unnecessary refetches:
   ```typescript
   // Adjust stale time if needed
   const { data } = useProviders({
     staleTime: 5 * 60 * 1000, // 5 minutes
   });
   ```

3. Implement pagination for large datasets

### Development Issues

#### Problem: Hot reload not working
**Symptoms:**
- Changes don't appear without manual refresh
- Next.js fast refresh fails

**Solutions:**
1. Clear Next.js cache:
   ```bash
   rm -rf .next
   npm run dev
   ```

2. Check for syntax errors in code

3. Restart development server

#### Problem: Build failures
**Symptoms:**
- `npm run build` fails
- TypeScript errors during build

**Solutions:**
1. Run type check separately:
   ```bash
   npm run type-check
   ```

2. Fix all TypeScript errors before building

3. Clear node_modules and reinstall:
   ```bash
   rm -rf node_modules package-lock.json
   npm install
   ```

## Debugging Tools

### Browser DevTools
1. **Console**: Check for JavaScript errors
2. **Network**: Monitor API calls and responses
3. **Application**: Inspect cookies and storage
4. **React DevTools**: Examine component state

### Logging
```typescript
// Enable debug logging
if (process.env.NODE_ENV === 'development') {
  console.log('Auth State:', useAuthStore.getState());
  console.log('API URLs:', {
    core: process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL,
    admin: process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL
  });
}
```

### React Query DevTools
```typescript
// See query cache state
// Monitor active queries
// Manually trigger refetches
```

## Getting Help

If you're still experiencing issues:

1. Check the [GitHub Issues](https://github.com/knnlabs/Conduit/issues)
2. Review the [Documentation](./README.md)
3. Enable debug logging and collect logs
4. Create a minimal reproduction example
5. Open a new issue with details

### Information to Include
- Browser and version
- Error messages and stack traces
- Network request/response details
- Steps to reproduce
- Environment configuration