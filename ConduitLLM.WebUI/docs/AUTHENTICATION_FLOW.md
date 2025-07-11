# Conduit WebUI Authentication Flow

## Overview

The WebUI uses a two-tier authentication system:

1. **User Authentication** (Browser → WebUI)
   - Cookie-based sessions
   - Login with CONDUIT_ADMIN_LOGIN_PASSWORD
   - Sessions stored server-side

2. **Service Authentication** (WebUI → Backend)
   - Master key authentication
   - Uses CONDUIT_API_TO_API_BACKEND_AUTH_KEY
   - Configured in SDK clients

## Authentication Flow Diagram

```
Browser          WebUI Server         Core API    Admin API
   |                  |                   |           |
   |--POST /login---> |                   |           |
   |                  |                   |           |
   |<--Set Cookie---- |                   |           |
   |                  |                   |           |
   |--GET /api/keys-> |                   |           |
   |  (with cookie)   |                   |           |
   |                  |--GET /keys-------> |           |
   |                  | (with master key) |           |
   |                  |<--Keys data-------| |           |
   |<--Keys data----- |                   |           |
```

## Environment Variables

### Required for WebUI:
- `CONDUIT_ADMIN_LOGIN_PASSWORD`: Password for admin login
- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`: Master key for backend

### Never Expose to Frontend:
- Any `*_AUTH_KEY` variables
- Any `*_API_KEY` variables
- Database credentials

## Security Best Practices

1. **Never expose backend auth keys to frontend**
2. **Always validate sessions on each request**
3. **Use httpOnly cookies for session storage**
4. **Implement CSRF protection**
5. **Rate limit authentication attempts**

## Common Mistakes

❌ **DON'T**: Use the same key for both purposes
```env
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=secret123
CONDUIT_ADMIN_LOGIN_PASSWORD=secret123  # WRONG!
```

✅ **DO**: Use different, strong values
```env
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=sk_live_xxxxxxxxxxx
CONDUIT_ADMIN_LOGIN_PASSWORD=MyStr0ng!AdminP@ssw0rd
```

❌ **DON'T**: Pass auth keys to frontend
```typescript
// WRONG - Never do this!
return NextResponse.json({
  apiKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY
});
```

✅ **DO**: Keep auth keys server-side only
```typescript
// RIGHT - Use SDK on server
const client = getServerAdminClient(); // Uses key internally
const data = await client.virtualKeys.list();
return NextResponse.json(data);
```

## Implementation Details

### Session Management

Sessions are stored as JSON in cookies with the following structure:
```typescript
interface Session {
  id: string;
  isAdmin: boolean;
  isAuthenticated: boolean;
  expiresAt: number; // Unix timestamp
}
```

### Authentication Middleware

The WebUI provides several authentication helpers:

1. **requireAuth()** - Requires any authenticated user
2. **requireAdmin()** - Requires admin privileges
3. **optionalAuth()** - Returns session if available, null otherwise
4. **withAuth()** - Wrapper for route handlers
5. **withAdmin()** - Wrapper for admin route handlers

### API Route Pattern

All protected API routes follow this pattern:
```typescript
import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/sdk-config';
import { requireAuth } from '@/lib/auth';

export async function GET(req: NextRequest) {
  try {
    // Authentication check (throws AuthError if not authenticated)
    await requireAuth(req);
    
    // SDK operations
    const adminClient = getServerAdminClient();
    const data = await adminClient.virtualKeys.list();
    
    return NextResponse.json(data);
  } catch (error) {
    return handleSDKError(error);
  }
}
```

### Public Routes

Routes that don't require authentication:
- `/api/auth/login` - Login endpoint
- `/api/health` - Health check (documented)

### Error Handling

Authentication errors are handled by the `handleSDKError` function which returns appropriate HTTP status codes:
- 401 Unauthorized - No valid session
- 403 Forbidden - Insufficient permissions

## SDK Configuration

The SDK clients are configured centrally in `/src/lib/server/sdk-config.ts`:

```typescript
export const SDK_CONFIG = {
  // Master key for backend communication
  masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
  
  // Base URLs
  adminBaseURL: process.env.NODE_ENV === 'production' 
    ? 'http://admin:8080' 
    : (process.env.CONDUIT_ADMIN_API_BASE_URL || 'http://localhost:5002'),
    
  coreBaseURL: process.env.NODE_ENV === 'production' 
    ? 'http://api:8080' 
    : (process.env.CONDUIT_API_BASE_URL || 'http://localhost:5000'),
  
  // Common settings
  timeout: 30000,
  maxRetries: 3,
  
  // Disable SignalR for server-side usage
  signalR: {
    enabled: false
  }
} as const;
```

## Testing Authentication

To test authentication locally:

1. Set environment variables:
```bash
export CONDUIT_ADMIN_LOGIN_PASSWORD="test-password"
export CONDUIT_API_TO_API_BACKEND_AUTH_KEY="test-master-key"
```

2. Login via API:
```bash
curl -X POST http://localhost:3000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"password": "test-password"}'
```

3. Use the session cookie for subsequent requests:
```bash
curl http://localhost:3000/api/virtualkeys \
  -H "Cookie: conduit_session=..."
```

## Security Checklist

- [x] All API routes use requireAuth() or document why not
- [x] SDK configuration validates env vars on startup
- [x] No auth keys in frontend code
- [x] Session validation on every request
- [x] Proper error messages (don't leak info)
- [ ] Rate limiting on auth endpoints (TODO)

## Future Considerations

- Consider implementing refresh tokens
- Monitor for suspicious authentication patterns
- Regular key rotation recommended
- Use strong, unique passwords
- Consider adding MFA support