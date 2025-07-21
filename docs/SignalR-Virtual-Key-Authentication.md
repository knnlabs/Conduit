# SignalR Virtual Key Authentication

This document describes how to authenticate with Conduit's SignalR hubs using virtual keys.

## Overview

Conduit provides real-time updates for image and video generation tasks through SignalR hubs. These hubs require virtual key authentication to ensure secure access to customer data.

## Available Hubs

### Customer-Facing Hubs (Authentication Required)

1. **Image Generation Hub** (`/hubs/image-generation`)
   - Provides real-time updates for image generation tasks
   - Methods: `SubscribeToTask(taskId)`, `UnsubscribeFromTask(taskId)`

2. **Video Generation Hub** (`/hubs/video-generation`)
   - Provides real-time updates for video generation requests
   - Methods: `SubscribeToRequest(requestId)`, `UnsubscribeFromRequest(requestId)`

### Internal Hubs (No Authentication)

1. **Navigation State Hub** (`/hubs/navigation-state`)
   - Internal admin use only
   - Updates navigation states in the WebUI

## Authentication Methods

### JavaScript/TypeScript Client

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/image-generation", {
        accessTokenFactory: () => "condt_your_virtual_key_here"
    })
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Connect and subscribe to a task
await connection.start();
await connection.invoke("SubscribeToTask", taskId);

// Listen for updates
connection.on("TaskProgress", (taskId, progress) => {
    console.log(`Task ${taskId} is ${progress}% complete`);
});

connection.on("TaskCompleted", (taskId, result) => {
    console.log(`Task ${taskId} completed:`, result);
});

connection.on("TaskFailed", (taskId, error) => {
    console.error(`Task ${taskId} failed:`, error);
});
```

### .NET Client

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://api.conduit.im/hubs/video-generation", options =>
    {
        options.Headers.Add("Authorization", "Bearer condt_your_virtual_key");
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
    })
    .Build();

// Connect and subscribe
await connection.StartAsync();
await connection.InvokeAsync("SubscribeToRequest", requestId);

// Listen for updates
connection.On<string, int>("RequestProgress", (requestId, progress) =>
{
    Console.WriteLine($"Request {requestId} is {progress}% complete");
});
```

### Query String Authentication

For clients that don't support headers, you can pass the virtual key as a query parameter:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/image-generation?api_key=condt_your_virtual_key")
    .build();
```

## Security Features

1. **Authentication Required**: All customer-facing hubs require valid virtual keys
2. **Task Ownership Verification**: Customers can only subscribe to their own tasks/requests
3. **Encrypted Transport**: Use WSS (WebSocket Secure) in production
4. **Connection Isolation**: Each virtual key's connections are grouped separately
5. **Automatic Cleanup**: Connections are cleaned up on disconnect

## Error Handling

### Authentication Errors

If authentication fails, the connection will be immediately closed. Common causes:
- Invalid or expired virtual key
- Disabled virtual key
- Missing authentication credentials

### Authorization Errors

Hub methods will throw `HubException` for authorization failures:
- Attempting to subscribe to another customer's task
- Task/request not found
- Invalid task ID format

## Best Practices

1. **Connection Management**
   - Reuse connections for multiple subscriptions
   - Implement reconnection logic with exponential backoff
   - Close connections when no longer needed

2. **Error Handling**
   - Handle connection failures gracefully
   - Implement retry logic for transient failures
   - Log errors for debugging

3. **Performance**
   - Don't create multiple connections for the same virtual key
   - Unsubscribe from tasks when updates are no longer needed
   - Use connection state to avoid redundant operations

## Example: Complete Image Generation Flow

```javascript
// 1. Start image generation via REST API
const response = await fetch('/v1/images/generations/async', {
    method: 'POST',
    headers: {
        'Authorization': 'Bearer condt_your_virtual_key',
        'Content-Type': 'application/json'
    },
    body: JSON.stringify({
        model: 'dall-e-3',
        prompt: 'A beautiful sunset over mountains'
    })
});

const { taskId } = await response.json();

// 2. Connect to SignalR for real-time updates
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/image-generation", {
        accessTokenFactory: () => "condt_your_virtual_key"
    })
    .build();

// 3. Set up event handlers
connection.on("TaskProgress", (id, progress) => {
    if (id === taskId) {
        updateProgressBar(progress);
    }
});

connection.on("TaskCompleted", (id, result) => {
    if (id === taskId) {
        displayImage(result.imageUrl);
        connection.stop();
    }
});

connection.on("TaskFailed", (id, error) => {
    if (id === taskId) {
        showError(error);
        connection.stop();
    }
});

// 4. Connect and subscribe
await connection.start();
await connection.invoke("SubscribeToTask", taskId);
```

## Rate Limiting

While SignalR connections themselves are not rate-limited, the following limits apply:
- Virtual key rate limits (RPM/RPD) apply to the initial REST API requests
- Connection limits may be enforced per virtual key (see configuration)
- Excessive connection/disconnection may trigger protective measures

## Troubleshooting

### Connection Fails Immediately
- Check virtual key is valid and starts with `condt_`
- Ensure virtual key is enabled and not expired
- Verify the hub URL is correct

### Cannot Subscribe to Task
- Verify the task ID is correct
- Ensure the task belongs to your virtual key
- Check that the task exists and hasn't expired

### No Updates Received
- Confirm the connection is established
- Verify you're subscribed to the correct task ID
- Check browser console for JavaScript errors
- Ensure WebSocket connections are not blocked

## Related Documentation
- [API Reference](./API-Reference.md)
- [Virtual Key Management](./Virtual-Key-Management.md)
- [Rate Limiting](./Rate-Limiting.md)