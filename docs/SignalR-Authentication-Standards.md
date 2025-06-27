# SignalR Authentication Standards

This document outlines the standardized authentication patterns for SignalR hubs in Conduit.

## Overview

All SignalR hubs in the Core API (ConduitLLM.Http) follow a consistent authentication pattern using virtual keys. The Admin API uses its own master key authentication pattern.

## Core Components

### 1. Authentication Attributes

#### VirtualKeyHubAuthorizationAttribute
- Used for standard SignalR hubs requiring virtual key authentication
- Sets authentication scheme to "VirtualKey"
- Requires the "RequireVirtualKey" policy

```csharp
[VirtualKeyHubAuthorization]
public class MyHub : SecureHub
{
    // Hub implementation
}
```

#### AdminHubAuthorizationAttribute
- Used for SignalR hubs requiring admin privileges
- Sets authentication scheme to "VirtualKey"
- Requires the "RequireAdminVirtualKey" policy

### 2. Base Classes

#### SecureHub
- Base class for all authenticated SignalR hubs
- Provides common authentication functionality
- Uses ISignalRAuthenticationService for consistent auth checks
- Handles connection/disconnection logging with virtual key context

### 3. Authentication Service

#### ISignalRAuthenticationService
Provides standardized authentication methods:
- `GetAuthenticatedVirtualKeyAsync()` - Retrieves the authenticated virtual key
- `IsAdminAsync()` - Checks if the virtual key has admin privileges
- `CanAccessResourceAsync()` - Validates resource access permissions
- `GetVirtualKeyId()` - Gets the virtual key ID from context
- `GetVirtualKeyName()` - Gets the virtual key name from context

### 4. Hub Filters

#### VirtualKeyHubFilter
- Validates virtual keys on connection and method invocation
- Stores virtual key information in hub context
- Handles authentication failures with proper logging

#### VirtualKeySignalRRateLimitFilter
- Enforces rate limiting per virtual key
- Prevents abuse of SignalR connections

## Implementation Guidelines

### Creating a New Hub

1. **Inherit from SecureHub**:
```csharp
[VirtualKeyHubAuthorization]
public class MyNewHub : SecureHub
{
    public MyNewHub(
        ILogger<MyNewHub> logger,
        IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
    }
    
    protected override string GetHubName() => "MyNewHub";
}
```

2. **Use Authentication Methods**:
```csharp
public async Task MyHubMethod(string parameter)
{
    // Get authenticated virtual key ID
    var virtualKeyId = RequireVirtualKeyId();
    
    // Check resource access
    if (!await CanAccessTaskAsync(taskId))
    {
        throw new HubException("Unauthorized access");
    }
    
    // Check admin privileges
    if (await IsAdminAsync())
    {
        // Admin-only functionality
    }
}
```

3. **Group Management**:
```csharp
public override async Task OnConnectedAsync()
{
    await base.OnConnectedAsync();
    
    // Virtual key is automatically added to vkey-{id} group
    // Add to additional groups as needed
    await Groups.AddToGroupAsync(Context.ConnectionId, "custom-group");
}
```

## Authentication Flow

1. **Connection**:
   - Client provides virtual key via query string, header, or auth token
   - VirtualKeyHubFilter validates the key
   - Virtual key info stored in hub context
   - Connection added to virtual key group

2. **Method Invocation**:
   - VirtualKeyHubFilter validates virtual key is still valid
   - Hub method uses SecureHub methods to check permissions
   - Method executes if authorized

3. **Disconnection**:
   - SecureHub logs disconnection with virtual key context
   - Cleanup performed

## Security Considerations

1. **Always use SecureHub** for authenticated hubs
2. **Never expose sensitive data** in hub methods without authorization checks
3. **Use resource-specific access checks** for operations on specific entities
4. **Log all authentication failures** for security monitoring
5. **Implement rate limiting** to prevent abuse

## Testing

When testing SignalR hubs:
1. Mock ISignalRAuthenticationService for unit tests
2. Test both authenticated and unauthenticated scenarios
3. Verify resource access checks work correctly
4. Test rate limiting behavior

## Migration Guide

To migrate existing hubs to the standardized pattern:

1. Change base class from `Hub` to `SecureHub`
2. Add `[VirtualKeyHubAuthorization]` attribute
3. Replace direct context access with SecureHub methods
4. Update tests to use standardized mocking