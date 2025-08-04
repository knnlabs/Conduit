# SignalR Quick Reference Guide

## Hub Selection Guide

### "Which hub should I use for..."

| Feature | Hub to Use | Endpoint |
|---------|------------|----------|
| Video generation progress | VideoGenerationHub | `/hubs/video-generation` |
| Image generation progress | ImageGenerationHub | `/hubs/image-generation` |
| Any async operation tracking | TaskHub | `/hubs/tasks` |
| Provider health updates | SystemNotificationHub | `/hubs/notifications` |
| Model availability changes | SystemNotificationHub | `/hubs/notifications` |
| Spend tracking | SpendNotificationHub | `/hubs/spend` |
| Budget alerts | SpendNotificationHub | `/hubs/spend` |
| Webhook delivery status | WebhookDeliveryHub | `/hubs/webhooks` |
| Admin system monitoring | AdminNotificationHub | `/hubs/admin-notifications` |

## Authentication Quick Reference

### Virtual Key Authentication (User Hubs)
```javascript
// JavaScript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/tasks", {
        accessTokenFactory: () => virtualKey
    })
    .build();
```

### Master Key Authentication (Admin Hubs)
```javascript
// JavaScript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://admin-api/hubs/admin-notifications", {
        accessTokenFactory: () => masterKey
    })
    .build();
```

## Common Connection Patterns

### Basic Connection (JavaScript)
```javascript
// Using centralized service
const signalR = window.conduitSignalR;
signalR.setVirtualKey(virtualKey);
await signalR.connectToHub('spend-notifications');
```

### Basic Connection (C#/Blazor)
```csharp
@inject SignalRConnectionManager SignalR

// Connect
await SignalR.SetVirtualKeyAsync(virtualKey);
await SignalR.ConnectToHubAsync("spend-notifications");
```

### Event Subscription (JavaScript)
```javascript
// Subscribe to specific task
await connection.invoke("SubscribeToTask", taskId);

// Handle events
connection.on("TaskProgress", (taskId, progress, message) => {
    console.log(`Task ${taskId}: ${progress}% - ${message}`);
});
```

### Event Subscription (C#/Blazor)
```csharp
// Using notification listener component
<SpendNotificationListenerV2 
    VirtualKey="@virtualKey"
    OnSpendUpdate="HandleSpendUpdate"
    OnBudgetAlert="HandleBudgetAlert" />

// Handle events
private async Task HandleSpendUpdate(SpendUpdateNotification notification)
{
    // Process spend update
}
```

## Event Reference by Hub

### TaskHub Events
```javascript
// Client receives
connection.on("TaskStarted", (taskId, taskType, metadata) => { });
connection.on("TaskProgress", (taskId, percentComplete, message) => { });
connection.on("TaskCompleted", (taskId, result) => { });
connection.on("TaskFailed", (taskId, error, details) => { });
```

### SystemNotificationHub Events
```javascript
// Client receives
connection.on("ProviderHealthChanged", (provider, status, message) => { });
connection.on("RateLimitWarning", (endpoint, remaining, resetTime) => { });
connection.on("ModelAvailabilityChanged", (model, isAvailable) => { });
```

### SpendNotificationHub Events
```javascript
// Client receives (via hub proxy)
hubProxy.onSpendUpdate((data) => { });
hubProxy.onBudgetAlert((data) => { });
hubProxy.onSpendSummary((data) => { });
hubProxy.onUnusualSpending((data) => { });
```

## Group Patterns Quick Reference

| Group Pattern | Example | Used By | Purpose |
|---------------|---------|---------|---------|
| `vkey-{id}` | `vkey-123` | All SecureHub | Virtual key isolation |
| `task-{id}` | `task-abc123` | TaskHub | Specific task updates |
| `video-{id}` | `video-req123` | VideoGenerationHub | Video tracking |
| `image-{id}` | `image-task456` | ImageGenerationHub | Image tracking |
| `webhook-{url}` | `webhook-api-example-com` | WebhookDeliveryHub | Webhook tracking |
| `admin` | `admin` | AdminNotificationHub | All admins |

## Common Mistakes to Avoid

### ❌ Don't: Create multiple connections to the same hub
```javascript
// Bad - creates duplicate connections
const conn1 = await createConnection("/hubs/tasks");
const conn2 = await createConnection("/hubs/tasks");
```

### ✅ Do: Reuse connections via centralized service
```javascript
// Good - reuses existing connection
const signalR = window.conduitSignalR;
await signalR.connectToHub("tasks");
```

### ❌ Don't: Forget to unsubscribe
```javascript
// Bad - memory leak
await connection.invoke("SubscribeToTask", taskId);
// Never unsubscribes...
```

### ✅ Do: Clean up subscriptions
```javascript
// Good - proper cleanup
await connection.invoke("SubscribeToTask", taskId);
// When done...
await connection.invoke("UnsubscribeFromTask", taskId);
```

### ❌ Don't: Mix authentication types
```javascript
// Bad - using virtual key for admin hub
await connectToHub("/hubs/admin-notifications", virtualKey);
```

### ✅ Do: Use correct authentication
```javascript
// Good - master key for admin hub
signalR.setMasterKey(masterKey);
await signalR.connectToHub("admin-notifications");
```

## Debugging Tips

### Check Connection State
```javascript
// Browser console
conduitSignalR.getConnectionState('spend-notifications')
conduitSignalR.getActiveConnections()
```

### Enable Debug Logging
```javascript
// Browser console
conduitSignalR.setDebugMode(true)
```

### Monitor Events
```javascript
// Listen to all state changes
window.addEventListener('conduit:spend-notifications:stateChanged', (e) => {
    console.log('State changed:', e.detail);
});
```

### Check Hub Groups (Server-side)
```csharp
// In hub method
var groups = Context.Items["Groups"] as List<string>;
_logger.LogInformation("User in groups: {Groups}", string.Join(", ", groups));
```

## Performance Best Practices

### 1. Connection Management
- Use one connection per hub per client
- Leverage connection pooling via centralized service
- Implement automatic reconnection

### 2. Event Handling
- Keep event handlers lightweight
- Avoid blocking operations in handlers
- Use debouncing for high-frequency events

### 3. Data Transfer
- Keep payloads small
- Use pagination for large datasets
- Consider compression for large messages

### 4. Scaling
- Enable Redis backplane for multi-instance
- Use groups for efficient broadcasting
- Monitor connection counts per instance

## Security Checklist

- [ ] Always validate virtual key ownership before task subscriptions
- [ ] Never expose sensitive data in events
- [ ] Use HTTPS/WSS in production
- [ ] Implement rate limiting for connections
- [ ] Log authentication failures
- [ ] Regularly rotate master keys
- [ ] Monitor for unusual connection patterns

## Need Help?

1. **Check the full documentation**: [SignalR-Hub-Responsibility-Matrix.md](./SignalR-Hub-Responsibility-Matrix.md)
2. **View architecture diagrams**: [SignalR-Hub-Architecture-Diagram.md](./SignalR-Hub-Architecture-Diagram.md)
3. **Review implementation examples**: [SignalR-Event-Publishing-Patterns.md](./SignalR-Event-Publishing-Patterns.md)
4. **Migration guide**: [SignalR-Migration-Example.md](./SignalR-Migration-Example.md)