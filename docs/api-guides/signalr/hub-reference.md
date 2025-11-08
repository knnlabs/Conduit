# SignalR Hub Responsibility Matrix

## Executive Summary

This document provides a comprehensive overview of all SignalR hubs in Conduit, their responsibilities, authentication methods, and event types. It serves as the authoritative reference for understanding hub boundaries and ensuring proper separation of concerns.

## Hub Architecture Overview

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

## Hub Responsibility Matrix

| Hub | Purpose | Authentication | Endpoint | Data Isolation |
|-----|---------|----------------|----------|----------------|
| **VideoGenerationHub** | Video generation progress tracking | Virtual Key | `/hubs/video-generation` | Per virtual key (`vkey-{id}`) |
| **ImageGenerationHub** | Image generation progress tracking | Virtual Key | `/hubs/image-generation` | Per virtual key (`vkey-{id}`) |
| **TaskHub** | Unified async task lifecycle management | Virtual Key | `/hubs/tasks` | Per virtual key (`vkey-{id}`) |
| **SystemNotificationHub** | System-wide notifications & alerts | Virtual Key | `/hubs/notifications` | Per virtual key (filtered) |
| **SpendNotificationHub** | Spend tracking & budget alerts | Virtual Key | `/hubs/spend` | Per virtual key (`vkey-{id}`) |
| **WebhookDeliveryHub** | Webhook delivery status tracking | Virtual Key | `/hubs/webhooks` | Per webhook URL |
| **AdminNotificationHub** | Administrative system management | Master Key | `/hubs/admin-notifications` | Global (no isolation) |

## Hub Details

### 1. VideoGenerationHub

**Purpose**: Provides real-time updates for video generation tasks

**Authentication**: Virtual Key (inherits from SecureHub)

**Client Methods**:
- `SubscribeToRequest(string requestId)` - Subscribe to specific video generation
- `UnsubscribeFromRequest(string requestId)` - Unsubscribe from updates

**Server Events**:
- Video generation progress updates
- Completion notifications
- Error notifications

**Group Patterns**:
- `vkey-{virtualKeyId}` - Automatic virtual key isolation
- `video-{requestId}` - Task-specific updates

**Use Cases**:
- Real-time progress bars for video generation
- Immediate notification on completion
- Error handling and retry status

---

### 2. ImageGenerationHub

**Purpose**: Provides real-time updates for image generation tasks

**Authentication**: Virtual Key (inherits from SecureHub)

**Client Methods**:
- `SubscribeToTask(string taskId)` - Subscribe to specific image generation
- `UnsubscribeFromTask(string taskId)` - Unsubscribe from updates

**Server Events**:
- Image generation started
- Progress updates
- Completion with image URLs
- Error notifications

**Group Patterns**:
- `vkey-{virtualKeyId}` - Automatic virtual key isolation
- `image-{taskId}` - Task-specific updates

**Use Cases**:
- Live image generation preview
- Batch generation progress
- Gallery updates

---

### 3. TaskHub

**Purpose**: Unified hub for tracking all asynchronous operations

**Authentication**: Virtual Key (inherits from SecureHub)

**Client Methods**:
- `SubscribeToTask(string taskId)` - Subscribe to specific task
- `UnsubscribeFromTask(string taskId)` - Unsubscribe from task
- `SubscribeToTaskType(string taskType)` - Subscribe to all tasks of a type
- `UnsubscribeFromTaskType(string taskType)` - Unsubscribe from task type

**Server Events** (ITaskHub interface):
- `TaskStarted(string taskId, string taskType, object metadata)`
- `TaskProgress(string taskId, int percentComplete, string? message)`
- `TaskCompleted(string taskId, object? result)`
- `TaskFailed(string taskId, string error, string? details)`
- `TaskCancelled(string taskId, string? reason)`
- `TaskTimedOut(string taskId)`

**Group Patterns**:
- `vkey-{virtualKeyId}` - Virtual key isolation
- `task-{taskId}` - Specific task updates
- `task-type-{taskType}` - Task type subscriptions

**Use Cases**:
- Batch operation progress tracking
- Long-running task monitoring
- Unified task dashboard
- Operation history

---

### 4. SystemNotificationHub

**Purpose**: System-wide notifications for all users

**Authentication**: Virtual Key (inherits from SecureHub)

**Client Methods**:
- `UpdatePreferences(NotificationPreferences preferences)` - Update notification settings

**Server Events** (ISystemNotificationHub interface):
- `ProviderHealthChanged(string provider, HealthStatus status, string? message)`
- `RateLimitWarning(string endpoint, int remaining, DateTime resetTime)`
- `SystemAnnouncement(string title, string message, string severity)`
- `ServiceDegraded(string service, string reason)`
- `ServiceRestored(string service)`
- `ModelMappingChanged(string model, string? newProvider)`
- `ModelCapabilitiesDiscovered(string provider, List<ModelCapability> capabilities)`
- `ModelAvailabilityChanged(string model, bool isAvailable)`

**Data Handling**:
- System events are broadcast to all connected users
- Filtered by virtual key groups for relevance
- Respects user notification preferences

**Use Cases**:
- Provider outage notifications
- Rate limit warnings
- System maintenance announcements
- Model availability updates

---

### 5. SpendNotificationHub

**Purpose**: Real-time spend tracking and budget management

**Authentication**: Virtual Key (inherits from SecureHub)

**Server Methods** (called by backend services):
- `SendSpendUpdate(SpendUpdateData data)` - Individual spend event
- `SendBudgetAlert(string alertType, AlertData data)` - Budget threshold alerts
- `SendSpendSummary(SpendPeriod period, SummaryData data)` - Periodic summaries
- `SendUnusualSpendingAlert(SpendAnalysis analysis)` - Anomaly detection

**Alert Cooldowns**:
- Prevents spam by tracking sent alerts
- 50%, 75%, 90%, 100% budget thresholds
- Configurable cooldown periods

**Group Patterns**:
- `vkey-{virtualKeyId}` - Strict virtual key isolation

**Use Cases**:
- Real-time spend dashboard
- Budget threshold notifications
- Usage analytics
- Cost anomaly detection

---

### 6. WebhookDeliveryHub

**Purpose**: Track webhook delivery status and retries

**Authentication**: Virtual Key (inherits from SecureHub)

**Client Methods**:
- `SubscribeToWebhooks(List<string> webhookUrls)` - Subscribe to webhook updates
- `UnsubscribeFromWebhooks(List<string> webhookUrls)` - Unsubscribe
- `RequestStatistics(string webhookUrl)` - Get delivery statistics

**Server Methods**:
- `BroadcastDeliveryAttempt(string url, DeliveryAttempt attempt)`
- `BroadcastDeliverySuccess(string url, DeliverySuccess success)`
- `BroadcastDeliveryFailure(string url, DeliveryFailure failure)`
- `BroadcastRetryScheduled(string url, RetryInfo retry)`

**Group Patterns**:
- `vkey-{virtualKeyId}` - Virtual key isolation
- `webhook-{sanitizedUrl}` - Per-webhook tracking

**Use Cases**:
- Webhook delivery monitoring
- Retry status tracking
- Delivery statistics dashboard
- Failure diagnostics

---

### 7. AdminNotificationHub

**Purpose**: Administrative control and monitoring

**Authentication**: Master Key (`[Authorize(Policy = "MasterKeyPolicy")]`)

**Client Methods**:
- `SubscribeToVirtualKey(int virtualKeyId)` - Monitor specific virtual key
- `UnsubscribeFromVirtualKey(int virtualKeyId)` - Stop monitoring
- `SubscribeToProvider(string providerName)` - Monitor provider
- `UnsubscribeFromProvider(string providerName)` - Stop monitoring
- `RefreshProviderHealth()` - Request health status update

**Server Events**:
- `InitialProviderHealth` - Initial health status on connection
- `ProviderHealthStatus` - Provider health updates
- `VirtualKeyCreated/Updated/Deleted` - Virtual key lifecycle
- `SystemAlert` - Critical system notifications
- `Error` - Error notifications

**Group Patterns**:
- `admin` - All admin connections
- `admin-vkey-{id}` - Virtual key monitoring
- `admin-provider-{name}` - Provider monitoring

**Use Cases**:
- System administration dashboard
- Provider health monitoring
- Virtual key management
- System-wide alerts

## Authentication Patterns

### Virtual Key Authentication (Core API)

```csharp
[Authorize]
public class SecureHub : Hub
{
    protected string VirtualKeyId => Context.UserIdentifier;
    
    public override async Task OnConnectedAsync()
    {
        // Automatically added to virtual key group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"vkey-{VirtualKeyId}");
    }
}
```

### Master Key Authentication (Admin API)

```csharp
[Authorize(Policy = "MasterKeyPolicy")]
public class AdminNotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Added to admin group for broadcast notifications
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
    }
}
```

## Event Naming Conventions

### Client-to-Server Methods
- Use verb phrases: `SubscribeToTask`, `UpdatePreferences`, `RequestStatistics`
- Include target in name: `SubscribeToVirtualKey`, `UnsubscribeFromProvider`

### Server-to-Client Events
- Use past tense for completed actions: `TaskCompleted`, `DeliverySucceeded`
- Use present continuous for ongoing: `TaskProgress`, `Reconnecting`
- Use noun+verb for state changes: `ProviderHealthChanged`, `ModelMappingChanged`

### Event Payload Conventions
- Include timestamps in UTC
- Use consistent property names across hubs
- Include correlation IDs for tracking
- Minimize payload size for performance

## Group Naming Patterns

| Pattern | Example | Purpose |
|---------|---------|---------|
| `vkey-{id}` | `vkey-123` | Virtual key isolation |
| `task-{id}` | `task-abc123` | Specific task updates |
| `task-type-{type}` | `task-type-video` | Task type subscriptions |
| `video-{id}` | `video-req123` | Video generation tracking |
| `image-{id}` | `image-task456` | Image generation tracking |
| `webhook-{url}` | `webhook-api-example-com-hook` | Webhook delivery tracking |
| `admin` | `admin` | All admin connections |
| `admin-vkey-{id}` | `admin-vkey-123` | Admin monitoring virtual key |
| `admin-provider-{name}` | `admin-provider-openai` | Admin monitoring provider |

## Security Considerations

### Data Isolation
- Virtual key groups ensure complete data isolation
- Task ownership verified before allowing subscriptions
- No cross-virtual-key data leakage possible

### Authentication
- Virtual keys validated on connection
- Master keys required for admin hubs
- Automatic disconnection on auth failure

### Rate Limiting
- Connection limits per virtual key
- Message rate limiting available
- Alert cooldowns prevent notification spam

### Audit Trail
- All hub connections logged
- Task subscriptions tracked
- Admin actions recorded

## Best Practices

### 1. Hub Selection
- Use specific hubs for specific features (don't overload SystemNotificationHub)
- Consider creating new hubs for new feature areas
- Keep hub responsibilities focused and clear

### 2. Event Design
- Keep payloads small and focused
- Use consistent naming across related events
- Include enough context for client handling

### 3. Group Management
- Always use consistent group naming patterns
- Clean up groups on disconnection
- Verify group membership before broadcasting

### 4. Error Handling
- Gracefully handle connection failures
- Provide meaningful error messages
- Implement retry logic where appropriate

### 5. Performance
- Use groups for efficient broadcasting
- Minimize payload sizes
- Consider batching for high-frequency updates

## Future Considerations

### Potential New Hubs
- **MetricsHub**: Real-time performance metrics
- **CollaborationHub**: Multi-user features
- **DebugHub**: Development and debugging tools

### Enhancements
- Message compression for large payloads
- Event replay for reconnections
- Hub-specific rate limiting policies
- Enhanced audit logging

## Conclusion

This matrix provides clear boundaries for each SignalR hub in Conduit. Following these guidelines ensures:
- Clear separation of concerns
- Consistent authentication patterns
- Proper data isolation
- Maintainable hub architecture

When adding new real-time features, consult this matrix to determine whether to use an existing hub or create a new one.