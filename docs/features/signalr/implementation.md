# SignalR Implementation Guide

*Last Updated: 2025-08-01*

This guide consolidates SignalR implementation patterns, hub architecture, and development practices in Conduit.

## Table of Contents
- [Hub Architecture Overview](#hub-architecture-overview)
- [Implementation Patterns](#implementation-patterns)
- [Authentication Standards](#authentication-standards)
- [Event Publishing Patterns](#event-publishing-patterns)
- [Connection Management](#connection-management)
- [Error Handling](#error-handling)
- [Testing and Validation](#testing-and-validation)

## Hub Architecture Overview

### Hub Responsibility Matrix

```
┌─────────────────────────────────────────────────────────────┐
│                        Clients                              │
│  (WebUI, Admin UI, External Apps, SDK)                      │
└─────────────────┬───────────────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────────────┐
│                    SignalR Hubs                             │
├─────────────────────────────────────────────────────────────┤
│  Core API Hubs (Virtual Key Auth)    │  Admin Hubs (Master) │
├──────────────────────────────────────┼──────────────────────┤
│ • VideoGenerationHub                 │ • AdminNotificationHub│
│ • ImageGenerationHub                 │                      │
│ • TaskHub                            │                      │
│ • SystemNotificationHub              │                      │
│ • SpendNotificationHub               │                      │
│ • WebhookDeliveryHub                 │                      │
└──────────────────────────────────────┴──────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────────────┐
│              Backend Services                               │
│  (Task Processing, Spend Tracking, Webhooks, etc.)          │
└─────────────────────────────────────────────────────────────┘
```

### Hub Responsibilities

| Hub | Purpose | Authentication | Key Events |
|-----|---------|----------------|------------|
| **VideoGenerationHub** | Video generation progress tracking | Virtual Key | VideoProgress, VideoCompleted, VideoFailed |
| **ImageGenerationHub** | Image generation progress tracking | Virtual Key | ImageProgress, ImageCompleted, ImageFailed |
| **TaskHub** | Generic async task notifications | Virtual Key | TaskStarted, TaskProgress, TaskCompleted |
| **SystemNotificationHub** | System health and provider status | Virtual Key | ProviderHealthChanged, RateLimitWarning |
| **SpendNotificationHub** | Budget and spend alerts | Virtual Key | SpendUpdate, BudgetAlert, UnusualSpending |
| **WebhookDeliveryHub** | Webhook delivery status | Virtual Key | WebhookDelivered, WebhookFailed |
| **AdminNotificationHub** | Admin system monitoring | Master Key | SystemAlert, ConfigurationChanged |

### Navigation State Hub (Planned)

The NavigationStateHub will track user journey through conversations:

```csharp
[Authorize(AuthenticationSchemes = "VirtualKeySignalR")]
public class NavigationStateHub : Hub
{
    public async Task UpdateNavigation(NavigationStateDto state)
    {
        await Clients.Group($"vkey-{Context.UserIdentifier}")
            .SendAsync("NavigationUpdated", state);
    }
}
```

## Implementation Patterns

### Pattern 1: Task Progress Tracking

For operations that take time (video/image generation, bulk operations):

```csharp
// Hub Implementation
[Authorize(AuthenticationSchemes = "VirtualKeySignalR")]
public class VideoGenerationHub : Hub
{
    private readonly IAsyncTaskService _taskService;

    public async Task SubscribeToVideoGeneration(string requestId)
    {
        // Validate ownership
        var canAccess = await _taskService.CanUserAccessTaskAsync(
            Context.UserIdentifier, requestId);
        
        if (!canAccess)
            throw new HubException("Access denied to video generation request");

        await Groups.AddToGroupAsync(ConnectionId, $"video-{requestId}");
    }

    public async Task UnsubscribeFromVideoGeneration(string requestId)
    {
        await Groups.RemoveFromGroupAsync(ConnectionId, $"video-{requestId}");
    }
}

// Service Integration
public class VideoGenerationService
{
    private readonly IHubContext<VideoGenerationHub> _hubContext;

    public async Task NotifyProgress(string requestId, int percentage, string status)
    {
        await _hubContext.Clients.Group($"video-{requestId}")
            .SendAsync("VideoProgress", new
            {
                RequestId = requestId,
                Percentage = percentage,
                Status = status,
                Timestamp = DateTime.UtcNow
            });
    }
}
```

### Pattern 2: Real-Time Spend Notifications

For budget monitoring and spend alerts:

```csharp
// Hub Implementation
[Authorize(AuthenticationSchemes = "VirtualKeySignalR")]
public class SpendNotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Join virtual key group for spend notifications
        var virtualKeyId = Context.UserIdentifier;
        await Groups.AddToGroupAsync(ConnectionId, $"vkey-{virtualKeyId}");
        await base.OnConnectedAsync();
    }
}

// Event Handler
public class SpendNotificationHandler : ISpendUpdateHandler
{
    private readonly IHubContext<SpendNotificationHub> _hubContext;

    public async Task HandleSpendUpdate(SpendUpdateEvent eventData)
    {
        await _hubContext.Clients.Group($"vkey-{eventData.VirtualKeyId}")
            .SendAsync("SpendUpdate", new SpendUpdateNotification
            {
                VirtualKeyId = eventData.VirtualKeyId,
                CurrentSpend = eventData.NewTotal,
                PreviousSpend = eventData.PreviousTotal,
                Delta = eventData.Delta,
                Timestamp = eventData.Timestamp
            });
    }
}
```

### Pattern 3: System-Wide Notifications

For provider health, rate limits, and system alerts:

```csharp
[Authorize(AuthenticationSchemes = "VirtualKeySignalR")]
public class SystemNotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // All connected users receive system notifications
        await Groups.AddToGroupAsync(ConnectionId, "system-notifications");
        await base.OnConnectedAsync();
    }
}

// Usage in health monitoring
public class ProviderHealthMonitor
{
    private readonly IHubContext<SystemNotificationHub> _hubContext;

    public async Task NotifyProviderHealthChange(string provider, HealthStatus status)
    {
        await _hubContext.Clients.Group("system-notifications")
            .SendAsync("ProviderHealthChanged", new
            {
                Provider = provider,
                Status = status.ToString(),
                Timestamp = DateTime.UtcNow,
                Message = $"Provider {provider} is now {status}"
            });
    }
}
```

## Authentication Standards

### Virtual Key Authentication

Most hubs use virtual key authentication for user-specific features:

```csharp
[Authorize(AuthenticationSchemes = "VirtualKeySignalR")]
public class MyHub : Hub
{
    // Context.UserIdentifier contains the virtual key ID
    // Use for group membership and access control
}
```

### Master Key Authentication (Admin Only)

Admin hubs require master key authentication:

```csharp
[Authorize(AuthenticationSchemes = "MasterKeySignalR")]
public class AdminNotificationHub : Hub
{
    // Only connections with valid master key can access
}
```

### Client-Side Authentication

JavaScript client authentication:

```javascript
// Virtual Key
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/spend-notifications", {
        accessTokenFactory: () => virtualKey
    })
    .build();

// Master Key (Admin)
const adminConnection = new signalR.HubConnectionBuilder()
    .withUrl("https://admin-api/hubs/admin-notifications", {
        accessTokenFactory: () => masterKey
    })
    .build();
```

## Event Publishing Patterns

### Pattern 1: Direct Hub Context Usage

For immediate notifications from services:

```csharp
public class MediaGenerationService
{
    private readonly IHubContext<ImageGenerationHub> _imageHub;
    private readonly IHubContext<VideoGenerationHub> _videoHub;

    public async Task NotifyImageProgress(string requestId, int progress)
    {
        await _imageHub.Clients.Group($"image-{requestId}")
            .SendAsync("ImageProgress", new { RequestId = requestId, Progress = progress });
    }
}
```

### Pattern 2: Event-Driven Publishing

For decoupled notifications via domain events:

```csharp
// Domain Event
public class TaskProgressEvent : DomainEvent
{
    public string TaskId { get; set; }
    public string TaskType { get; set; }
    public int PercentComplete { get; set; }
    public string Message { get; set; }
}

// Event Handler
public class TaskProgressNotificationHandler : INotificationHandler<TaskProgressEvent>
{
    private readonly IHubContext<TaskHub> _hubContext;

    public async Task Handle(TaskProgressEvent notification, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.Group($"task-{notification.TaskId}")
            .SendAsync("TaskProgress", notification, cancellationToken);
    }
}
```

### Pattern 3: Batch Notifications

For high-frequency events that need batching:

```csharp
public class SpendNotificationBatcher
{
    private readonly IHubContext<SpendNotificationHub> _hubContext;
    private readonly Timer _batchTimer;
    private readonly ConcurrentDictionary<string, SpendUpdate> _pendingUpdates;

    public void QueueSpendUpdate(string virtualKeyId, decimal delta)
    {
        _pendingUpdates.AddOrUpdate(virtualKeyId, 
            new SpendUpdate { VirtualKeyId = virtualKeyId, Delta = delta },
            (key, existing) => { existing.Delta += delta; return existing; });
    }

    private async Task FlushBatch()
    {
        var updates = _pendingUpdates.ToList();
        _pendingUpdates.Clear();

        var tasks = updates.Select(async kvp =>
        {
            await _hubContext.Clients.Group($"vkey-{kvp.Key}")
                .SendAsync("SpendUpdate", kvp.Value);
        });

        await Task.WhenAll(tasks);
    }
}
```

## Connection Management

### Centralized Connection Service (Client-Side)

```javascript
class ConduitSignalRService {
    constructor() {
        this.connections = new Map();
        this.virtualKey = null;
        this.masterKey = null;
    }

    setVirtualKey(key) {
        this.virtualKey = key;
    }

    setMasterKey(key) {
        this.masterKey = key;
    }

    async connectToHub(hubName) {
        if (this.connections.has(hubName)) {
            return this.connections.get(hubName);
        }

        const hubUrl = this.getHubUrl(hubName);
        const token = this.getTokenForHub(hubName);

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => token
            })
            .withAutomaticReconnect()
            .build();

        await connection.start();
        this.connections.set(hubName, connection);
        return connection;
    }

    getHubUrl(hubName) {
        const hubMappings = {
            'spend-notifications': '/hubs/spend-notifications',
            'task-notifications': '/hubs/tasks',
            'admin-notifications': 'https://admin-api/hubs/admin-notifications'
        };
        return hubMappings[hubName];
    }

    getTokenForHub(hubName) {
        const adminHubs = ['admin-notifications'];
        return adminHubs.includes(hubName) ? this.masterKey : this.virtualKey;
    }
}

// Global instance
window.conduitSignalR = new ConduitSignalRService();
```

### Connection Lifecycle Management

```csharp
public class SignalRConnectionManager
{
    private readonly ConcurrentDictionary<string, HubConnection> _connections;

    public async Task<HubConnection> GetOrCreateConnection(string hubName, string authToken)
    {
        return await _connections.GetOrAdd(hubName, async _ =>
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(GetHubUrl(hubName), options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(authToken);
                })
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync();
            return connection;
        });
    }
}
```

## Error Handling

### Hub-Side Error Handling

```csharp
public class VideoGenerationHub : Hub
{
    private readonly ILogger<VideoGenerationHub> _logger;

    public async Task SubscribeToVideoGeneration(string requestId)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(requestId))
                throw new HubException("Request ID is required");

            // Validate ownership
            var canAccess = await ValidateAccess(requestId);
            if (!canAccess)
                throw new HubException("Access denied to video generation request");

            await Groups.AddToGroupAsync(ConnectionId, $"video-{requestId}");
            
            _logger.LogInformation("User {UserId} subscribed to video generation {RequestId}", 
                Context.UserIdentifier, requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to video generation {RequestId}", requestId);
            throw new HubException("Failed to subscribe to video generation updates");
        }
    }

    private async Task<bool> ValidateAccess(string requestId)
    {
        // Implementation depends on your access control logic
        return true; // Placeholder
    }
}
```

### Client-Side Error Handling

```javascript
const connection = await conduitSignalR.connectToHub('video-generation');

connection.onclose((error) => {
    console.error('Connection closed:', error);
    // Implement reconnection logic if needed
});

connection.onreconnecting((error) => {
    console.warn('Connection lost, reconnecting...', error);
    // Show user notification
});

connection.onreconnected((connectionId) => {
    console.log('Reconnected with ID:', connectionId);
    // Re-subscribe to relevant events
});

// Handle specific errors
try {
    await connection.invoke('SubscribeToVideoGeneration', requestId);
} catch (error) {
    console.error('Failed to subscribe:', error);
    // Show user-friendly error message
}
```

## Testing and Validation

### Unit Testing Hubs

```csharp
[Test]
public async Task SubscribeToVideoGeneration_ValidRequest_AddsToGroup()
{
    // Arrange
    var mockGroups = new Mock<IGroupManager>();
    var mockContext = new Mock<HubCallerContext>();
    var mockClients = new Mock<IHubCallerClients>();
    
    mockContext.Setup(x => x.ConnectionId).Returns("test-connection");
    mockContext.Setup(x => x.UserIdentifier).Returns("test-user");
    
    var hub = new VideoGenerationHub()
    {
        Context = mockContext.Object,
        Groups = mockGroups.Object,
        Clients = mockClients.Object
    };

    // Act
    await hub.SubscribeToVideoGeneration("test-request");

    // Assert
    mockGroups.Verify(x => x.AddToGroupAsync("test-connection", "video-test-request", default), 
        Times.Once);
}
```

### Integration Testing with Redis Backplane

```csharp
[Test]
public async Task VideoProgress_WithRedisBackplane_NotifiesAllInstances()
{
    // Arrange
    using var app1 = CreateTestApp(redisConnectionString);
    using var app2 = CreateTestApp(redisConnectionString);
    
    var client1 = CreateSignalRClient(app1);
    var client2 = CreateSignalRClient(app2);
    
    var progressReceived = new TaskCompletionSource<VideoProgressDto>();
    client2.On<VideoProgressDto>("VideoProgress", progress => 
    {
        progressReceived.SetResult(progress);
    });

    await client1.StartAsync();
    await client2.StartAsync();
    
    await client2.InvokeAsync("SubscribeToVideoGeneration", "test-request");

    // Act - Send progress from instance 1
    var hubContext = app1.Services.GetRequiredService<IHubContext<VideoGenerationHub>>();
    await hubContext.Clients.Group("video-test-request")
        .SendAsync("VideoProgress", new VideoProgressDto { RequestId = "test-request", Progress = 50 });

    // Assert - Client connected to instance 2 should receive the message
    var result = await progressReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.That(result.Progress, Is.EqualTo(50));
}
```

### Event Verification Matrix

To ensure all events are properly routed, maintain this verification matrix:

| Hub | Event | Group Pattern | Tested | Notes |
|-----|-------|---------------|--------|-------|
| VideoGenerationHub | VideoProgress | `video-{requestId}` | ✅ | |
| VideoGenerationHub | VideoCompleted | `video-{requestId}` | ✅ | |
| ImageGenerationHub | ImageProgress | `image-{requestId}` | ✅ | |
| TaskHub | TaskStarted | `task-{taskId}` | ✅ | |
| SpendNotificationHub | SpendUpdate | `vkey-{virtualKeyId}` | 🔄 | In progress |
| SystemNotificationHub | ProviderHealthChanged | `system-notifications` | ❌ | Not implemented |

## Performance Considerations

### Group Management Optimization

```csharp
// Efficient group cleanup on disconnect
public override async Task OnDisconnectedAsync(Exception exception)
{
    // Remove from all groups this connection might be in
    var connectionId = Context.ConnectionId;
    var userId = Context.UserIdentifier;
    
    // Clean up user-specific groups
    await Groups.RemoveFromGroupAsync(connectionId, $"vkey-{userId}");
    
    // Clean up any task-specific subscriptions
    // This requires tracking active subscriptions per connection
    await CleanupTaskSubscriptions(connectionId);
    
    await base.OnDisconnectedAsync(exception);
}
```

### Message Batching for High-Frequency Events

```csharp
public class BatchedSpendNotificationService
{
    private readonly IHubContext<SpendNotificationHub> _hubContext;
    private readonly ConcurrentDictionary<string, List<SpendUpdate>> _batchedUpdates;
    private readonly Timer _flushTimer;

    public BatchedSpendNotificationService(IHubContext<SpendNotificationHub> hubContext)
    {
        _hubContext = hubContext;
        _batchedUpdates = new ConcurrentDictionary<string, List<SpendUpdate>>();
        _flushTimer = new Timer(FlushBatches, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void QueueSpendUpdate(string virtualKeyId, SpendUpdate update)
    {
        _batchedUpdates.AddOrUpdate(virtualKeyId, 
            new List<SpendUpdate> { update },
            (key, existing) => { existing.Add(update); return existing; });
    }

    private async void FlushBatches(object state)
    {
        var currentBatches = new Dictionary<string, List<SpendUpdate>>();
        
        foreach (var kvp in _batchedUpdates.ToList())
        {
            if (_batchedUpdates.TryRemove(kvp.Key, out var updates))
            {
                currentBatches[kvp.Key] = updates;
            }
        }

        var tasks = currentBatches.Select(async batch =>
        {
            var aggregatedUpdate = AggregateUpdates(batch.Value);
            await _hubContext.Clients.Group($"vkey-{batch.Key}")
                .SendAsync("SpendUpdate", aggregatedUpdate);
        });

        await Task.WhenAll(tasks);
    }
}
```

## Constants and Configuration

### Hub Constants

```csharp
public static class HubConstants
{
    public const string VideoGenerationHub = "/hubs/video-generation";
    public const string ImageGenerationHub = "/hubs/image-generation";
    public const string TaskHub = "/hubs/tasks";
    public const string SystemNotificationHub = "/hubs/notifications";
    public const string SpendNotificationHub = "/hubs/spend-notifications";
    public const string WebhookDeliveryHub = "/hubs/webhooks";
    public const string AdminNotificationHub = "/hubs/admin-notifications";
    public const string NavigationStateHub = "/hubs/navigation-state";
}

public static class GroupPatterns
{
    public static string VirtualKeyGroup(string virtualKeyId) => $"vkey-{virtualKeyId}";
    public static string TaskGroup(string taskId) => $"task-{taskId}";
    public static string VideoGroup(string requestId) => $"video-{requestId}";
    public static string ImageGroup(string requestId) => $"image-{requestId}";
    public static string WebhookGroup(string webhookUrl) => $"webhook-{webhookUrl.GetHashCode()}";
    public const string SystemNotifications = "system-notifications";
    public const string AdminGroup = "admin";
}
```

### Configuration

```csharp
// Program.cs
services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
}).AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Redis backplane for scaling
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = connectionString;
});
```

## Migration Guide

### From Individual Event Handlers to SignalR

If migrating from polling or individual event handlers:

1. **Identify Event Sources**: Map existing events to appropriate hubs
2. **Plan Group Strategy**: Design group membership for proper message routing
3. **Implement Authentication**: Ensure proper virtual key validation
4. **Add Client Libraries**: Update front-end to use SignalR connections
5. **Test Event Flow**: Verify events reach the correct clients
6. **Monitor Performance**: Watch for connection counts and message throughput

### Example Migration: Spend Notifications

**Before** (Polling):
```javascript
setInterval(async () => {
    const response = await fetch('/api/spend/current');
    const spend = await response.json();
    updateSpendDisplay(spend);
}, 5000);
```

**After** (SignalR):
```javascript
const connection = await conduitSignalR.connectToHub('spend-notifications');

connection.on('SpendUpdate', (update) => {
    updateSpendDisplay(update);
});

// Connection automatically maintained, no polling needed
```

## Related Documentation

- [SignalR Quick Reference](./quick-reference.md) - Common tasks and troubleshooting
- [SignalR Configuration](../../signalr/configuration.md) - Server setup and Redis backplane
- [Authentication Guide](./authentication.md) - Virtual key and master key authentication
- [Performance Optimization](./performance.md) - Scaling and optimization strategies

---

*This document consolidates information from multiple SignalR implementation files. Last updated: 2025-08-01*