# SignalR Event Publishing Patterns

This document describes the patterns and best practices for publishing SignalR events in Conduit.

## Overview

Conduit uses SignalR for real-time notifications across multiple hubs, each serving a specific domain:
- **AdminNotificationHub** - Administrative notifications (master key auth)
- **SystemNotificationHub** - System-wide notifications  
- **SpendNotificationHub** - Spending and budget notifications
- **VideoGenerationHub** - Video generation progress
- **ImageGenerationHub** - Image generation progress
- **WebhookDeliveryHub** - Webhook delivery tracking
- **TaskHub** - Unified async task notifications

## Event Publishing Patterns

### 1. Hub Context Pattern

Most services use `IHubContext<THub>` to send notifications:

```csharp
public class SpendNotificationService
{
    private readonly IHubContext<SpendNotificationHub> _hubContext;
    
    public async Task NotifySpendUpdateAsync(int virtualKeyId, decimal amount)
    {
        var groupName = $"vkey-{virtualKeyId}";
        await _hubContext.Clients.Group(groupName).SendAsync("SpendUpdate", notification);
    }
}
```

### 2. Group-Based Broadcasting

Events are sent to specific groups based on:
- **Virtual Key**: `vkey-{virtualKeyId}`
- **Task**: `task-{taskId}`, `video-{taskId}`, `image-{taskId}`
- **Provider**: `admin-provider-{providerName}`
- **Webhook**: `webhook_{host}_{path}`
- **Admin**: `admin` (all administrators)

### 3. Event Naming Conventions

#### Event Names
- Use PascalCase for event names: `SpendUpdate`, `BudgetAlert`
- Be descriptive: `VideoGenerationCompleted` not just `Completed`
- Include action: `ProviderHealthUpdate`, `VirtualKeyDeleted`

#### Notification DTOs
- Suffix with `Notification`: `SpendUpdateNotification`
- Include timestamp: `Timestamp = DateTime.UtcNow`
- Include relevant IDs and context

### 4. Logging Standards

All event publishers should log with a structured format:

```csharp
_logger.LogInformation(
    "[SignalR:{EventName}] Sent notification - {Key}: {Value}, Groups: [{Groups}]",
    "BudgetAlert",
    "VirtualKey", virtualKeyId,
    groupName);
```

Include:
- `[SignalR:EventName]` prefix for easy filtering
- Key parameters (IDs, amounts, thresholds)
- Target groups
- Use appropriate log levels (Information for normal, Warning for alerts)

### 5. Error Handling

Event publishing should never break the main flow:

```csharp
try
{
    await _hubContext.Clients.Group(groupName).SendAsync("EventName", data);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error sending {EventName} notification", "EventName");
    // Don't throw - notifications are non-critical
}
```

### 6. Event Throttling

For high-frequency events, implement throttling:

```csharp
// Budget alerts track sent thresholds
private readonly ConcurrentDictionary<int, HashSet<int>> _sentBudgetAlerts = new();

if (percentageUsed >= threshold && !sentAlerts.Contains(threshold))
{
    // Send alert only once per threshold
    await SendBudgetAlert(virtualKeyId, threshold);
    sentAlerts.Add(threshold);
}
```

### 7. Event Payloads

Keep payloads focused and lightweight:

```csharp
public class SpendUpdateNotification
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public decimal NewSpend { get; set; }      // Latest spend amount
    public decimal TotalSpend { get; set; }    // Running total
    public decimal? Budget { get; set; }        // Optional budget
    public decimal? BudgetPercentage { get; set; } // Calculated percentage
    public string Provider { get; set; }       // Context
    public string Model { get; set; }          // Context
}
```

## Hub-Specific Patterns

### AdminNotificationHub
- Requires master key authentication
- Groups: `admin`, `admin-vkey-{id}`, `admin-provider-{name}`
- High-priority events: SecurityAlert, HighSpendAlert

### SpendNotificationHub  
- Virtual key scoped: `vkey-{virtualKeyId}`
- Threshold-based alerts with state tracking
- Pattern analysis for unusual spending

### Task-Based Hubs (Video/Image/Task)
- Task-scoped groups: `{type}-{taskId}`
- Progress events with percentage and status
- Completion events with results/URLs

### WebhookDeliveryHub
- URL-based groups from webhook endpoints
- Delivery lifecycle events
- Circuit breaker state changes

## Event Flow Examples

### Budget Alert Flow
1. Spend update triggers threshold check
2. If threshold crossed and not previously sent:
   - Create BudgetAlertNotification
   - Send to `vkey-{id}` group
   - Log with [SignalR:BudgetAlert] prefix
   - Mark threshold as sent
3. Reset alerts when spend drops below 50%

### Provider Health Update Flow
1. Health check detects status change
2. Create ProviderHealthNotification
3. Send to `admin` and `admin-provider-{name}` groups
4. Log status change with response time
5. Hysteresis prevents notification flapping

## Testing Event Publishing

### Unit Tests
```csharp
[Test]
public async Task NotifyBudgetAlert_ShouldSendToCorrectGroup()
{
    // Arrange
    var mockHubContext = new Mock<IHubContext<SpendNotificationHub>>();
    var mockClients = new Mock<IHubClients>();
    var mockGroup = new Mock<IClientProxy>();
    
    mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
    mockClients.Setup(x => x.Group("vkey-123")).Returns(mockGroup.Object);
    
    // Act
    await service.NotifyBudgetAlert(123, alert);
    
    // Assert
    mockGroup.Verify(x => x.SendAsync("BudgetAlert", 
        It.IsAny<BudgetAlertNotification>(), 
        default), Times.Once);
}
```

### Integration Tests
- Use TestServer with real SignalR connections
- Verify event delivery end-to-end
- Test group isolation
- Validate event ordering

## Monitoring and Metrics

### Key Metrics
- Events published per hub/type
- Event publishing latency
- Failed event deliveries
- Active connections per hub
- Group membership counts

### Logging Queries
```kusto
// Find all SignalR events
traces
| where message contains "[SignalR:"
| summarize count() by extract(@"\[SignalR:(\w+)\]", 1, message)

// Track budget alerts
traces  
| where message contains "[SignalR:BudgetAlert]"
| project timestamp, virtualKeyId=extract(@"VirtualKey: (\d+)", 1, message),
         threshold=extract(@"Threshold: (\d+)%", 1, message)
```

## Best Practices

1. **Always use groups** - Never broadcast to all clients
2. **Include correlation IDs** - For tracing event flows
3. **Keep payloads small** - Only essential data
4. **Log comprehensively** - Include context for debugging
5. **Handle errors gracefully** - Don't break main flows
6. **Test event isolation** - Ensure proper scoping
7. **Monitor event rates** - Watch for notification storms
8. **Document event contracts** - Keep DTOs well-documented