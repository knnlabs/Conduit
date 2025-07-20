# SignalR Authentication Guide

This document provides a comprehensive guide to authentication in Conduit's SignalR hubs, covering virtual key authentication, admin authentication, and anonymous access patterns.

## Overview

Conduit's SignalR implementation supports three distinct authentication patterns:

1. **Virtual Key Authentication** - For customer-facing hubs requiring virtual key authorization
2. **Admin Authentication** - For internal admin hubs using master key authentication
3. **Anonymous Access** - For public health monitoring hubs

All SignalR hubs follow a consistent authentication pattern using standardized attributes, base classes, and service interfaces.

## Authentication Patterns

### 1. Virtual Key Authentication (Most Common)

Virtual key authentication is used for customer-facing hubs that provide real-time updates for image generation, video generation, and other customer-specific operations.

#### Supported Hubs
- **Image Generation Hub** (`/hubs/image-generation`) - Real-time image generation updates
- **Video Generation Hub** (`/hubs/video-generation`) - Real-time video generation updates

#### Implementation

```csharp
[VirtualKeyHubAuthorization]
public class ImageGenerationHub : SecureHub
{
    public ImageGenerationHub(
        ILogger<ImageGenerationHub> logger,
        IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
    }
    
    protected override string GetHubName() => "ImageGenerationHub";
    
    public async Task SubscribeToTask(string taskId)
    {
        // Get authenticated virtual key ID
        var virtualKeyId = RequireVirtualKeyId();
        
        // Verify task ownership
        if (!await CanAccessTaskAsync(taskId))
        {
            throw new HubException("Unauthorized access to task");
        }
        
        await Groups.AddToGroupAsync(Context.ConnectionId, $"task-{taskId}");
    }
}
```

#### Client Implementation

**JavaScript/TypeScript:**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/image-generation", {
        accessTokenFactory: () => "condt_your_virtual_key_here"
    })
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Connect and subscribe
await connection.start();
await connection.invoke("SubscribeToTask", taskId);

// Listen for updates
connection.on("TaskProgress", (taskId, progress) => {
    console.log(`Task ${taskId} is ${progress}% complete`);
});

connection.on("TaskCompleted", (taskId, result) => {
    console.log(`Task ${taskId} completed:`, result);
});
```

**.NET Client:**
```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://api.conduit.im/hubs/video-generation", options =>
    {
        options.Headers.Add("Authorization", "Bearer condt_your_virtual_key");
    })
    .Build();

await connection.StartAsync();
await connection.InvokeAsync("SubscribeToRequest", requestId);

connection.On<string, int>("RequestProgress", (requestId, progress) =>
{
    Console.WriteLine($"Request {requestId} is {progress}% complete");
});
```

**Query String Authentication (Alternative):**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/image-generation?api_key=condt_your_virtual_key")
    .build();
```

### 2. Admin Authentication

Admin authentication uses master key authentication for internal administrative hubs.

#### Supported Hubs
- **Navigation State Hub** (`/hubs/navigation-state`) - WebUI navigation state updates

#### Implementation

```csharp
[AdminHubAuthorization]
public class NavigationStateHub : SecureHub
{
    public NavigationStateHub(
        ILogger<NavigationStateHub> logger,
        IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
    }
    
    protected override string GetHubName() => "NavigationStateHub";
    
    public async Task SubscribeToVirtualKeys()
    {
        // Verify admin privileges
        if (!await IsAdminAsync())
        {
            throw new HubException("Admin privileges required");
        }
        
        await Groups.AddToGroupAsync(Context.ConnectionId, "virtual-keys");
    }
}
```

#### Client Implementation

**JavaScript/TypeScript:**
```javascript
// Admin hub authentication using master key
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/navigation-state", {
        accessTokenFactory: () => "your_master_key_here"
    })
    .build();

await connection.start();
await connection.invoke("SubscribeToVirtualKeys");
```

#### Authentication Methods Supported

The admin authentication handler supports multiple authentication methods to accommodate SignalR's WebSocket limitations:

1. **Bearer Token** (preferred for initial HTTP handshake)
2. **Query String** (automatically used by SignalR for WebSocket upgrade)
3. **X-API-Key Header** (backward compatibility)
4. **X-Master-Key Header** (legacy support)

### 3. Anonymous Access

Some hubs allow anonymous access for public health monitoring.

#### Supported Hubs
- **Health Hub** (`/hubs/health`) - Public system health monitoring

#### Implementation

```csharp
// No authorization attribute needed for anonymous hubs
public class HealthHub : Hub
{
    private readonly ILogger<HealthHub> _logger;
    
    public HealthHub(ILogger<HealthHub> logger)
    {
        _logger = logger;
    }
    
    public async Task SubscribeToHealthUpdates()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "health-updates");
        _logger.LogInformation("Anonymous connection subscribed to health updates");
    }
}
```

## Core Components

### Authentication Attributes

#### VirtualKeyHubAuthorizationAttribute
- Sets authentication scheme to "VirtualKey"
- Requires the "RequireVirtualKey" policy
- Used for customer-facing hubs

#### AdminHubAuthorizationAttribute
- Sets authentication scheme to "VirtualKey"
- Requires the "RequireAdminVirtualKey" policy
- Used for admin-only hubs

### Base Classes

#### SecureHub
Base class for all authenticated SignalR hubs providing:
- Common authentication functionality via `ISignalRAuthenticationService`
- Automatic virtual key group management
- Connection/disconnection logging with virtual key context
- Helper methods for authorization checks

**Key Methods:**
```csharp
protected string RequireVirtualKeyId() // Gets authenticated virtual key ID
protected async Task<bool> IsAdminAsync() // Checks admin privileges
protected async Task<bool> CanAccessTaskAsync(string taskId) // Resource access validation
protected string GetVirtualKeyName() // Gets virtual key name from context
```

### Authentication Service

#### ISignalRAuthenticationService

Provides standardized authentication methods:

```csharp
public interface ISignalRAuthenticationService
{
    Task<VirtualKeyDto?> GetAuthenticatedVirtualKeyAsync();
    Task<bool> IsAdminAsync();
    Task<bool> CanAccessResourceAsync(string resourceType, string resourceId);
    string? GetVirtualKeyId();
    string? GetVirtualKeyName();
}
```

### Hub Filters

#### VirtualKeyHubFilter
- Validates virtual keys on connection and method invocation
- Stores virtual key information in hub context
- Handles authentication failures with proper logging

#### VirtualKeySignalRRateLimitFilter
- Enforces rate limiting per virtual key
- Prevents abuse of SignalR connections

## Authentication Flow

### Virtual Key Authentication Flow

1. **Connection Establishment**:
   - Client provides virtual key via `accessTokenFactory` or query string
   - `VirtualKeyHubFilter` validates the key against the database
   - Virtual key information stored in hub context
   - Connection automatically added to virtual key group (`vkey-{id}`)

2. **Method Invocation**:
   - `VirtualKeyHubFilter` validates virtual key is still active
   - Hub method uses `SecureHub` methods to check permissions
   - Resource-specific access checks performed if needed
   - Method executes if authorized

3. **Disconnection**:
   - `SecureHub` logs disconnection with virtual key context
   - Automatic cleanup performed

### Admin Authentication Flow

1. **Initial HTTP Request**: SignalR sends master key in Authorization header as `Bearer {token}`
2. **WebSocket Upgrade**: SignalR automatically switches to query string `?access_token={token}`
3. **Server Validation**: Authentication handlers check all possible locations:
   - Authorization: Bearer header (SignalR HTTP)
   - access_token query parameter (SignalR WebSocket)
   - X-API-Key header (backward compatibility)
   - X-Master-Key header (legacy support)

## Security Features

### Virtual Key Security
1. **Authentication Required**: All customer-facing hubs require valid virtual keys
2. **Task Ownership Verification**: Customers can only access their own resources
3. **Connection Isolation**: Each virtual key's connections are grouped separately
4. **Automatic Cleanup**: Connections cleaned up on disconnect
5. **Rate Limiting**: Per-virtual-key rate limiting prevents abuse

### Admin Security
1. **Master Key Authentication**: Requires valid master key for admin operations
2. **Privilege Verification**: Admin-only methods check `IsAdminAsync()`
3. **Secure Transport**: Uses HTTPS/WSS in production
4. **Query String Logging**: Debug logging for query string authentication

### General Security
1. **Encrypted Transport**: All production connections use WSS (WebSocket Secure)
2. **Token Protection**: Proper handling of authentication tokens
3. **Connection Monitoring**: Authentication failures logged for security monitoring
4. **Circuit Breakers**: Protective measures against excessive connections

## Implementation Guidelines

### Creating a New Authenticated Hub

1. **Choose the Right Base and Attribute**:
```csharp
// For customer-facing hubs
[VirtualKeyHubAuthorization]
public class MyCustomerHub : SecureHub

// For admin-only hubs
[AdminHubAuthorization]
public class MyAdminHub : SecureHub

// For public hubs
public class MyPublicHub : Hub
```

2. **Implement Required Methods**:
```csharp
public MyCustomerHub(
    ILogger<MyCustomerHub> logger,
    IServiceProvider serviceProvider)
    : base(logger, serviceProvider)
{
}

protected override string GetHubName() => "MyCustomerHub";
```

3. **Use Authentication Helpers**:
```csharp
public async Task MyHubMethod(string resourceId)
{
    // Get authenticated virtual key ID
    var virtualKeyId = RequireVirtualKeyId();
    
    // Check resource access
    if (!await CanAccessResourceAsync("task", resourceId))
    {
        throw new HubException("Unauthorized access");
    }
    
    // Check admin privileges if needed
    if (await IsAdminAsync())
    {
        // Admin-only functionality
    }
    
    // Business logic here
}
```

### Group Management Best Practices

```csharp
public override async Task OnConnectedAsync()
{
    await base.OnConnectedAsync(); // This adds to virtual key group automatically
    
    // Add to additional groups as needed
    var virtualKeyId = RequireVirtualKeyId();
    await Groups.AddToGroupAsync(Context.ConnectionId, $"notifications-{virtualKeyId}");
}

public override async Task OnDisconnectedAsync(Exception? exception)
{
    // Custom cleanup if needed
    await base.OnDisconnectedAsync(exception); // Handles standard cleanup
}
```

## Error Handling

### Authentication Errors

**Connection Failures:**
- Invalid or expired virtual key
- Disabled virtual key
- Missing authentication credentials
- Connection immediately closed with appropriate error

**Authorization Errors:**
- Hub methods throw `HubException` for authorization failures
- Attempting to access another customer's resources
- Resource not found or invalid format

### Client-Side Error Handling

```javascript
connection.onclose(async (error) => {
    if (error) {
        console.error("Connection closed due to error:", error);
        
        // Check if it's an authentication error
        if (error.message.includes("Unauthorized")) {
            // Handle authentication failure
            await refreshToken();
            return;
        }
        
        // Implement reconnection logic
        await reconnectWithBackoff();
    }
});
```

## Best Practices

### Connection Management
1. **Reuse Connections**: Use single connection for multiple subscriptions per virtual key
2. **Implement Reconnection**: Use exponential backoff for connection failures
3. **Clean Shutdown**: Properly close connections when no longer needed
4. **State Management**: Track connection state to avoid redundant operations

### Performance Optimization
1. **Efficient Subscriptions**: Subscribe only to needed updates
2. **Unsubscribe Properly**: Remove subscriptions when no longer needed
3. **Connection Pooling**: Avoid creating multiple connections for same virtual key
4. **Batch Operations**: Group related hub method calls when possible

### Security Best Practices
1. **Secure Token Storage**: Store authentication tokens securely on client
2. **Token Rotation**: Implement token refresh for long-lived connections
3. **Transport Security**: Always use HTTPS/WSS in production
4. **Error Logging**: Log authentication failures for security monitoring
5. **Rate Limiting**: Respect rate limits and implement client-side throttling

### Error Handling Best Practices
1. **Graceful Degradation**: Handle connection failures gracefully
2. **Retry Logic**: Implement appropriate retry strategies
3. **User Feedback**: Provide clear feedback for authentication issues
4. **Monitoring**: Monitor connection health and authentication success rates

## Testing Authentication

### Unit Testing

```csharp
[Test]
public async Task SubscribeToTask_WithValidVirtualKey_Succeeds()
{
    // Arrange
    var mockAuthService = new Mock<ISignalRAuthenticationService>();
    mockAuthService.Setup(x => x.GetVirtualKeyId()).Returns("test-key-id");
    mockAuthService.Setup(x => x.CanAccessResourceAsync("task", "task-123"))
               .ReturnsAsync(true);
    
    // Act & Assert
    await hub.SubscribeToTask("task-123");
    // Verify group membership, etc.
}

[Test]
public async Task SubscribeToTask_WithInvalidVirtualKey_ThrowsException()
{
    // Arrange
    var mockAuthService = new Mock<ISignalRAuthenticationService>();
    mockAuthService.Setup(x => x.GetVirtualKeyId()).Returns((string)null);
    
    // Act & Assert
    var exception = await Assert.ThrowsAsync<HubException>(
        () => hub.SubscribeToTask("task-123"));
    Assert.Contains("Unauthorized", exception.Message);
}
```

### Integration Testing

Create test clients to verify end-to-end authentication:

```javascript
// Test virtual key authentication
const testConnection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/image-generation", {
        accessTokenFactory: () => TEST_VIRTUAL_KEY
    })
    .build();

await testConnection.start();
console.log("Virtual key authentication successful");

// Test admin authentication
const adminConnection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/navigation-state", {
        accessTokenFactory: () => TEST_MASTER_KEY
    })
    .build();

await adminConnection.start();
console.log("Admin authentication successful");
```

## Troubleshooting

### Common Issues

**Connection Fails Immediately:**
- Verify virtual key format (starts with `condt_`)
- Check virtual key is enabled and not expired
- Confirm hub URL is correct
- Verify SSL certificate in production

**Cannot Subscribe to Resources:**
- Verify resource ID format and existence
- Ensure resource belongs to your virtual key
- Check for proper error handling in hub methods

**No Updates Received:**
- Confirm connection is established (`connection.state === "Connected"`)
- Verify subscription to correct resource ID
- Check browser console for JavaScript errors
- Ensure WebSocket connections not blocked by firewall/proxy

**Admin Authentication Fails:**
- Verify master key is correct
- Check server logs for authentication attempts
- Ensure admin privileges are properly configured
- Test with REST API first to verify master key

### Debug Logging

Enable debug logging to troubleshoot authentication issues:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.AspNetCore.SignalR": "Debug",
      "ConduitLLM.Http.Authentication": "Debug"
    }
  }
}
```

## Related Documentation

- [SignalR Architecture](./architecture.md) - Overall SignalR system architecture
- [Hub Reference](./hub-reference.md) - Complete hub method reference
- [Configuration Guide](./configuration.md) - SignalR configuration options
- [API Reference](../api-reference/API-REFERENCE.md) - REST API documentation
- [Virtual Key Management](../virtual-keys.md) - Virtual key management guide
- [Security Guidelines](../Security-Guidelines.md) - General security practices