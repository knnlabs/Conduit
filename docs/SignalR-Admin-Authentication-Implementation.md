# SignalR Admin Hub Authentication Implementation

## Overview

This document describes the implementation of master key authentication for SignalR admin hubs, addressing the WebSocket authentication limitations.

## Problem Summary

SignalR WebSocket connections cannot send custom headers after the initial handshake, which prevented the existing header-based master key authentication from working with SignalR hubs.

## Solution Implementation

### 1. Server-Side Changes

#### MasterKeyAuthenticationHandler.cs Updates

Added support for Bearer token and query string authentication methods:

```csharp
// Check Authorization header for Bearer token (SignalR support)
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
    
    // Log when query string auth is used for SignalR
    if (!string.IsNullOrEmpty(providedKey) && Context.Request.Path.StartsWithSegments("/hubs"))
    {
        Logger.LogDebug("Using query string authentication for SignalR hub: {Path}", 
            Context.Request.Path.ToString().Replace(Environment.NewLine, ""));
    }
}
```

#### MasterKeyAuthorizationHandler.cs Updates

Added similar support for Bearer token and query string authentication:

```csharp
// Check Authorization header for Bearer token (SignalR support)
if (httpContext.Request.Headers.TryGetValue("Authorization", out var authValues))
{
    var authHeader = authValues.FirstOrDefault();
    if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
    {
        var bearerToken = authHeader.Substring("Bearer ".Length).Trim();
        if (bearerToken == masterKey)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}

// Check query string for SignalR WebSocket connections
if (httpContext.Request.Query.TryGetValue("access_token", out var tokenValues))
{
    var queryToken = tokenValues.FirstOrDefault();
    if (queryToken == masterKey)
    {
        // Log when query string auth is used for SignalR
        if (httpContext.Request.Path.StartsWithSegments("/hubs"))
        {
            _logger.LogDebug("Authorized SignalR hub connection via query string: {Path}", 
                httpContext.Request.Path.ToString().Replace(Environment.NewLine, ""));
        }
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

### 2. Client-Side Changes

#### conduit-signalr-service.js Updates

Simplified the authentication to use only `accessTokenFactory`, which SignalR automatically handles:

```javascript
_buildConnectionOptions(authKey, isMasterKey = false) {
    const options = {};
    
    if (authKey) {
        // For both master key and virtual key, use accessTokenFactory
        // The server will handle it properly - Bearer token or query string
        options.accessTokenFactory = () => authKey;
        
        // Note: Do NOT add custom headers for SignalR connections
        // They won't work with WebSocket transport after handshake
        // The accessTokenFactory will send the token as a query parameter
        // or in the Authorization header during the initial HTTP request
    }
    
    // Add credentials for cross-origin requests
    options.withCredentials = false;
    
    return options;
}
```

## Authentication Flow

1. **Initial HTTP Request**: SignalR sends the token in the Authorization header as `Bearer {token}`
2. **WebSocket Upgrade**: SignalR switches to query string `?access_token={token}` for WebSocket connections
3. **Server Validation**: Both authentication handlers check all possible locations:
   - X-API-Key header (for backward compatibility)
   - X-Master-Key header (legacy support)
   - Authorization: Bearer header (SignalR HTTP)
   - access_token query parameter (SignalR WebSocket)

## Security Considerations

1. **Query String Logging**: The implementation includes debug logging when query string authentication is used for SignalR hubs
2. **Transport Security**: Always use HTTPS/WSS in production to protect tokens in transit
3. **Token Exposure**: Query string tokens may appear in server logs - ensure proper log security
4. **Backward Compatibility**: Existing header-based authentication continues to work for non-SignalR endpoints

## Testing

A test page has been created at `/test-admin-signalr.html` that demonstrates:
- Connecting to the admin hub with master key authentication
- Subscribing to virtual keys and providers
- Receiving real-time notifications
- Connection state management

### How to Test

1. Start both Core API and Admin API services
2. Navigate to `http://localhost:5002/test-admin-signalr.html`
3. Enter your master key
4. Click "Connect" to establish SignalR connection
5. Click "Test Subscriptions" to verify hub methods work
6. Check the event log for real-time updates

## Benefits

1. **WebSocket Compatibility**: Works with all SignalR transports including WebSockets
2. **Unified Authentication**: Same authentication handler supports both REST and SignalR
3. **Backward Compatible**: No breaking changes to existing authentication
4. **Debug Support**: Includes logging for troubleshooting authentication issues

## Future Enhancements

1. Consider implementing token rotation for long-lived connections
2. Add connection-specific claims for fine-grained authorization
3. Implement rate limiting per connection
4. Add metrics for authentication success/failure rates