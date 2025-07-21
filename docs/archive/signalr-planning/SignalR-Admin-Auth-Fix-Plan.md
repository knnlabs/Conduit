# SignalR Admin Hub Authentication Fix Plan

## Problem Analysis

The AdminNotificationHub has `[Authorize(Policy = "MasterKeyPolicy")]` but SignalR authentication has special requirements:

1. **WebSocket Limitation**: Custom headers (like X-API-Key) cannot be sent after the initial handshake
2. **Current Implementation Issues**:
   - MasterKeyAuthenticationHandler expects headers that won't be available for WebSocket
   - The client is trying to send both `accessTokenFactory` and custom headers
   - SignalR needs the authentication to work via query string or bearer token

## Current Implementation Problems

### Client Side (conduit-signalr-service.js)
```javascript
options.accessTokenFactory = () => authKey;
options.headers = {
    'X-API-Key': authKey  // This won't work for WebSocket!
};
```

### Server Side (MasterKeyAuthenticationHandler.cs)
- Only checks headers: `X-API-Key` or `X-Master-Key`
- Doesn't handle query string parameters
- Doesn't handle Bearer token format

## Solution Options

### Option 1: Use Query String Authentication (Recommended)
**Pros**:
- Works with WebSockets
- Simple to implement
- Clear security model

**Implementation**:
1. Update client to send master key as query parameter
2. Update MasterKeyAuthenticationHandler to check query string
3. Ensure query string is only used for SignalR hubs

### Option 2: Use Bearer Token Format
**Pros**:
- Standard OAuth2 pattern
- Works with accessTokenFactory

**Cons**:
- Master key isn't really a bearer token
- May confuse with virtual key authentication

### Option 3: Create Separate SignalR Authentication
**Pros**:
- Dedicated auth for SignalR
- Can handle special cases

**Cons**:
- More complex
- Duplicates authentication logic

## Recommended Implementation (Option 1)

### 1. Update Client (conduit-signalr-service.js)
```javascript
_buildConnectionOptions(authKey, isMasterKey = false) {
    const options = {};
    
    if (authKey) {
        if (isMasterKey) {
            // For master key, use query string for SignalR compatibility
            options.accessTokenFactory = () => authKey;
        } else {
            // For virtual key, use bearer token
            options.accessTokenFactory = () => authKey;
        }
    }
    
    return options;
}
```

### 2. Update Server (MasterKeyAuthenticationHandler.cs)
```csharp
protected override Task<AuthenticateResult> HandleAuthenticateAsync()
{
    // ... existing code ...
    
    // Check for master key in headers (for regular API calls)
    if (Context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValues))
    {
        providedKey = apiKeyValues.FirstOrDefault();
    }
    // Check for Authorization header with Bearer token (for SignalR)
    else if (Context.Request.Headers.TryGetValue("Authorization", out var authValues))
    {
        var authHeader = authValues.FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            providedKey = authHeader.Substring("Bearer ".Length).Trim();
        }
    }
    // Check query string for SignalR WebSocket connections
    else if (Context.Request.Query.TryGetValue("access_token", out var tokenValues))
    {
        providedKey = tokenValues.FirstOrDefault();
    }
    
    // ... rest of validation ...
}
```

### 3. Security Considerations
- Only allow query string auth for SignalR hub endpoints
- Log when query string auth is used
- Consider adding hub-specific validation

## Testing Plan
1. Test regular API endpoints still work with X-API-Key header
2. Test SignalR hub connections with master key
3. Test unauthorized access is properly rejected
4. Test WebSocket upgrade works correctly

## Alternative: SignalR-Specific Authentication Filter
If we want to be more explicit, we could create a SignalR-specific auth filter that handles the special cases for hub authentication.