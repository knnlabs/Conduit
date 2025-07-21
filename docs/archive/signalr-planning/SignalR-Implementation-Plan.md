# SignalR Implementation Plan for Conduit

## Executive Summary

This document outlines a comprehensive plan to expand SignalR usage in Conduit for real-time updates across the platform. While Conduit already has a robust SignalR implementation for image and video generation, this plan identifies opportunities to enhance the user experience through additional real-time features.

## Current State Analysis

### Existing SignalR Infrastructure

#### Implemented Hubs
1. **NavigationStateHub** (`/hubs/navigation-state`)
   - Real-time navigation updates for model mappings and provider health
   - Used by admin dashboard

2. **VideoGenerationHub** (`/hubs/video-generation`)
   - Progress tracking for video generation tasks
   - Task-based subscription model

3. **ImageGenerationHub** (`/hubs/image-generation`)
   - Progress tracking for async image generation
   - Supports both sync and async workflows

#### Key Strengths
- **Robust Authentication**: Virtual key-based auth with proper validation
- **Scalable Architecture**: Redis backplane support for horizontal scaling
- **Event-Driven Design**: Leverages MassTransit for loose coupling
- **Security-First**: Task ownership verification, rate limiting, and group isolation
- **Comprehensive Clients**: Well-implemented JavaScript clients with reconnection logic

## Proposed SignalR Enhancements

### 1. Core API Enhancements

#### A. Unified Task Hub
Create a generic `TaskHub` to handle all async operations:

```csharp
public interface ITaskHub
{
    Task TaskStarted(string taskId, string taskType, object metadata);
    Task TaskProgress(string taskId, int progress, string message);
    Task TaskCompleted(string taskId, object result);
    Task TaskFailed(string taskId, string error);
}
```

**Benefits:**
- Single subscription point for all async operations
- Consistent task tracking across different operation types
- Reduced client complexity

#### B. System Notifications Hub
New hub for system-wide notifications:

```csharp
public interface ISystemNotificationHub
{
    Task ProviderHealthChanged(string provider, HealthStatus status);
    Task RateLimitWarning(int remaining, DateTime resetTime);
    Task SpendAlert(decimal amount, decimal budget);
    Task WebhookDeliveryStatus(string webhookId, DeliveryStatus status);
}
```

### 2. Feature-Specific Implementations

#### A. Provider Health Monitoring
- **Current**: Health checks run every 5 minutes
- **Enhancement**: Push real-time health updates when status changes
- **Implementation**:
  ```csharp
  // In ProviderHealthMonitoringService
  if (healthStatus != previousStatus)
  {
      await _notificationService.NotifyHealthChange(provider, healthStatus);
  }
  ```

#### B. Spend Tracking & Alerts
- **Current**: Spend updates via events
- **Enhancement**: Real-time spend notifications with budget alerts
- **Use Cases**:
  - Alert when 80% of budget consumed
  - Real-time spend dashboard updates
  - Daily/weekly spend summaries

#### C. Webhook Delivery Tracking
- **Current**: Fire-and-forget webhook delivery
- **Enhancement**: Real-time delivery status updates
- **Features**:
  - Delivery confirmation
  - Retry status
  - Failure notifications

#### D. Model Discovery Updates
- **Current**: Manual refresh required
- **Enhancement**: Push updates when new models discovered
- **Benefits**:
  - Immediate availability of new models
  - No need for polling or manual refresh

### 3. WebUI Integration Strategy

#### A. SignalR Service Abstraction
Create a centralized SignalR service for all hubs:

```typescript
class ConduitSignalRService {
    private connections: Map<string, signalR.HubConnection>;
    
    async connectToHub(hubName: string, virtualKey: string) {
        // Centralized connection management
        // Automatic reconnection
        // Error handling
    }
    
    async subscribeToTask(taskId: string, callbacks: TaskCallbacks) {
        // Generic task subscription
    }
}
```

#### B. Blazor Component Integration
Enhance existing components with real-time capabilities:

1. **VirtualKeyDashboard**
   - Real-time spend updates
   - Live request counts
   - Budget alert notifications

2. **ProviderConfiguration**
   - Live health status indicators
   - Real-time model discovery alerts

3. **WebhookConfiguration**
   - Delivery status tracking
   - Retry progress visualization

### 4. Implementation Roadmap

#### Phase 1: Foundation (Week 1-2)
- [ ] Create unified TaskHub interface
- [ ] Implement generic task notification service
- [ ] Update existing hubs to use shared patterns
- [ ] Create comprehensive logging/metrics

#### Phase 2: Core Features (Week 3-4)
- [ ] Implement SystemNotificationHub
- [ ] Add spend tracking notifications
- [ ] Integrate provider health push updates
- [ ] Create webhook delivery tracking

#### Phase 3: WebUI Integration (Week 5-6)
- [ ] Build ConduitSignalRService abstraction
- [ ] Update VirtualKeyDashboard with real-time data
- [ ] Add live notifications to admin panels
- [ ] Implement connection status indicators

#### Phase 4: Advanced Features (Week 7-8)
- [ ] Add batch operation progress tracking
- [ ] Implement model discovery notifications
- [ ] Create real-time metrics dashboard
- [ ] Add system health monitoring

### 5. Security Considerations

#### Authentication & Authorization
1. **Maintain Virtual Key Auth**: Continue using existing authentication
2. **Enhanced Claims**: Add task type permissions to claims
3. **Group Isolation**: Ensure proper group membership for notifications
4. **Rate Limiting**: Apply rate limits per notification type

#### Data Security
1. **Payload Sanitization**: Strip sensitive data from notifications
2. **Encryption**: Use TLS for all SignalR connections
3. **Audit Logging**: Log all SignalR subscriptions and notifications
4. **Access Control**: Verify permissions for each notification type

### 6. Technical Guidelines

#### Hub Design Patterns
```csharp
public abstract class SecureHub : Hub
{
    protected string VirtualKeyId => Context.UserIdentifier;
    
    protected async Task<bool> CanAccessTask(string taskId)
    {
        var task = await _taskService.GetTaskAsync(taskId);
        return task?.VirtualKeyId == VirtualKeyId;
    }
    
    protected async Task AddToVirtualKeyGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"vkey-{VirtualKeyId}");
    }
}
```

#### Client Connection Pattern
```javascript
class SignalRConnection {
    constructor(hubUrl, virtualKey) {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, { 
                accessTokenFactory: () => virtualKey,
                withCredentials: false
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: (retryContext) => {
                    return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                }
            })
            .build();
    }
}
```

#### Event Handler Pattern
```csharp
public class TaskEventHandler : IConsumer<TaskProgressEvent>
{
    private readonly IHubContext<TaskHub> _hubContext;
    
    public async Task Consume(ConsumeContext<TaskProgressEvent> context)
    {
        var @event = context.Message;
        await _hubContext.Clients
            .Group($"vkey-{@event.VirtualKeyId}")
            .SendAsync("TaskProgress", @event.TaskId, @event.Progress);
    }
}
```

### 7. Testing Strategy

#### Unit Tests
- Mock SignalR hub context for event handlers
- Test authentication and authorization logic
- Verify group membership management

#### Integration Tests
- Test end-to-end message flow
- Verify Redis backplane functionality
- Test reconnection scenarios

#### Load Tests
- Simulate 1000+ concurrent connections
- Test message throughput
- Verify horizontal scaling

#### Security Tests
- Attempt cross-virtual-key access
- Test rate limiting effectiveness
- Verify authentication failures

### 8. Monitoring & Observability

#### Metrics to Track
- Active connections per hub
- Messages sent/received per minute
- Reconnection frequency
- Authentication failures
- Average message latency

#### Health Checks
```csharp
public class SignalRHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context)
    {
        // Check Redis connection
        // Verify hub availability
        // Test authentication
    }
}
```

### 9. Migration Considerations

#### Backward Compatibility
- Maintain existing hub endpoints
- Support legacy client connections
- Gradual migration path

#### Feature Flags
```csharp
if (_featureFlags.IsEnabled("UnifiedTaskHub"))
{
    services.AddSignalR().AddHub<TaskHub>("/hubs/tasks");
}
```

### 10. Success Metrics

- **User Experience**: Reduced page refreshes, faster feedback
- **Performance**: Lower API polling requests
- **Reliability**: 99.9% message delivery rate
- **Scalability**: Support 10K+ concurrent connections
- **Developer Experience**: Simplified real-time integration

## Conclusion

This plan leverages Conduit's existing SignalR infrastructure to provide comprehensive real-time updates across the platform. By following the phased approach and adhering to security best practices, we can enhance user experience while maintaining system reliability and security.