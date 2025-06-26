# ConduitSignalRService Documentation

## Overview

ConduitSignalRService is a centralized JavaScript service that abstracts SignalR connection management for Conduit's WebUI. It provides a unified interface for managing multiple SignalR hub connections with features like automatic reconnection, message queuing, and performance monitoring.

## Features

- **Connection Pooling**: Manage multiple hub connections from a single service
- **Automatic Reconnection**: Exponential backoff with configurable retry limits
- **Message Queuing**: Queue messages when disconnected, process when reconnected
- **State Management**: Track connection states with event notifications
- **Performance Monitoring**: Built-in metrics for connection and method invocation times
- **Debug Mode**: Detailed logging for troubleshooting
- **TypeScript Support**: Full type definitions for IntelliSense
- **Hub Proxies**: Strongly-typed wrappers for common hubs

## Quick Start

### Basic Usage

```javascript
// Get the singleton instance
const signalR = window.conduitSignalR;

// Set virtual key for authentication
signalR.setVirtualKey('your-virtual-key');

// Connect to a hub
await signalR.connectToHub('spend-notifications');

// Register event handlers
signalR.on('spend-notifications', 'SpendUpdate', (notification) => {
    console.log('Spend update:', notification);
});

// Invoke hub methods
await signalR.invoke('spend-notifications', 'RequestSummary');

// Disconnect when done
await signalR.disconnectFromHub('spend-notifications');
```

### Using Hub Proxies

```javascript
// Create a typed hub proxy
const spendHub = window.conduitHubs.createSpendNotificationsHub();

// Connect with virtual key
await spendHub.connect('your-virtual-key');

// Register typed event handlers
spendHub.onSpendUpdate((notification) => {
    console.log(`New spend: $${notification.newSpend}`);
    console.log(`Total spend: $${notification.totalSpend}`);
});

spendHub.onBudgetAlert((alert) => {
    console.warn(`Budget alert: ${alert.message}`);
});
```

## Connection Management

### Connection Options

```javascript
const options = {
    maxReconnectAttempts: 10,      // Maximum reconnection attempts
    baseReconnectDelay: 5000,      // Base delay in milliseconds
    maxReconnectDelay: 30000,      // Maximum delay between attempts
    enableMessageQueuing: true,     // Queue messages when disconnected
    enableAutoReconnect: true       // Enable automatic reconnection
};

await signalR.connectToHub('video-generation', virtualKey, options);
```

### Connection States

Monitor connection state changes:

```javascript
// Check current state
const state = signalR.getConnectionState('video-generation');
console.log('Current state:', state);

// Listen for state changes
window.addEventListener('conduit:video-generation:stateChanged', (event) => {
    console.log('State changed from', event.detail.previousState, 
                'to', event.detail.currentState);
});
```

Available states:
- `disconnected` - Not connected
- `connecting` - Connection in progress
- `connected` - Successfully connected
- `reconnecting` - Attempting to reconnect
- `failed` - Connection failed after max attempts

## Event Handling

### Hub Events

```javascript
// Register event handler
signalR.on('image-generation', 'TaskProgress', (taskId, progress, message) => {
    console.log(`Task ${taskId}: ${progress}% - ${message}`);
});

// Remove specific handler
signalR.off('image-generation', 'TaskProgress', handler);

// Using hub proxy (automatic cleanup)
const imageHub = conduitHubs.createImageGenerationHub();
imageHub.onTaskProgress((taskId, progress, message) => {
    updateProgressBar(taskId, progress);
});
```

### Custom Events

The service emits custom events for connection lifecycle:

```javascript
// Connection closed
window.addEventListener('conduit:webhook:connectionClosed', (event) => {
    console.log('Connection closed:', event.detail.error);
});

// Reconnecting
window.addEventListener('conduit:webhook:reconnecting', (event) => {
    showReconnectingIndicator();
});

// Reconnected
window.addEventListener('conduit:webhook:reconnected', (event) => {
    hideReconnectingIndicator();
    console.log('Reconnected with ID:', event.detail.connectionId);
});

// Connection failed
window.addEventListener('conduit:webhook:connectionFailed', (event) => {
    showErrorMessage('Connection failed: ' + event.detail.reason);
});
```

## Message Queuing

Messages are automatically queued when disconnected:

```javascript
// Enable message queuing (default: true)
await signalR.connectToHub('webhooks', virtualKey, {
    enableMessageQueuing: true
});

// Messages sent while disconnected are queued
await signalR.send('webhooks', 'SubscribeToWebhooks', ['https://example.com/hook']);

// When reconnected, queued messages are automatically sent
```

## Performance Monitoring

Track performance metrics:

```javascript
// Enable debug mode to see timing logs
signalR.setDebugMode(true);

// Get metrics for a hub
const metrics = signalR.getMetrics('video-generation');
console.log('Connection metrics:', metrics);

// Example metrics output:
{
    connectionTime: {
        count: 5,
        total: 2500,
        min: 200,
        max: 800,
        average: 500,
        last: 300
    },
    'invoke.SubscribeToTask': {
        count: 10,
        total: 1500,
        min: 100,
        max: 300,
        average: 150,
        last: 120
    }
}
```

## Blazor Integration

### JavaScript Interop

```csharp
@inject IJSRuntime JS

private IJSObjectReference? _signalRService;
private IJSObjectReference? _spendHub;

protected override async Task OnInitializedAsync()
{
    // Get SignalR service instance
    _signalRService = await JS.InvokeAsync<IJSObjectReference>(
        "ConduitSignalRService.getInstance");
    
    // Set virtual key
    await _signalRService.InvokeVoidAsync("setVirtualKey", VirtualKey);
    
    // Create hub proxy
    _spendHub = await JS.InvokeAsync<IJSObjectReference>(
        "conduitHubs.createSpendNotificationsHub");
    
    // Connect to hub
    await _spendHub.InvokeVoidAsync("connect", VirtualKey);
    
    // Register event handler
    await _spendHub.InvokeVoidAsync("onSpendUpdate", 
        DotNetObjectReference.Create(this));
}

[JSInvokable]
public void HandleSpendUpdate(SpendUpdateNotification notification)
{
    // Handle spend update in C#
    CurrentSpend = notification.TotalSpend;
    StateHasChanged();
}

public async ValueTask DisposeAsync()
{
    if (_spendHub != null)
    {
        await _spendHub.InvokeVoidAsync("disconnect");
        await _spendHub.DisposeAsync();
    }
    
    if (_signalRService != null)
    {
        await _signalRService.DisposeAsync();
    }
}
```

### Blazor Component Example

```razor
@page "/realtime-spend"
@implements IAsyncDisposable
@inject IJSRuntime JS

<h3>Real-Time Spend Tracking</h3>

<div class="spend-display">
    <p>Current Spend: $@CurrentSpend.ToString("F2")</p>
    <p>Budget Remaining: $@BudgetRemaining.ToString("F2")</p>
    
    @if (LatestAlert != null)
    {
        <div class="alert alert-@GetAlertClass(LatestAlert.Severity)">
            @LatestAlert.Message
        </div>
    }
</div>

@code {
    private decimal CurrentSpend = 0;
    private decimal BudgetRemaining = 1000;
    private BudgetAlert? LatestAlert;
    
    private IJSObjectReference? _spendHub;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Create and connect to spend hub
            _spendHub = await JS.InvokeAsync<IJSObjectReference>(
                "conduitHubs.createSpendNotificationsHub");
                
            await _spendHub.InvokeVoidAsync("connect", VirtualKey);
            
            // Set up event handlers with callbacks to this component
            var dotNetRef = DotNetObjectReference.Create(this);
            await _spendHub.InvokeVoidAsync("onSpendUpdate", dotNetRef);
            await _spendHub.InvokeVoidAsync("onBudgetAlert", dotNetRef);
        }
    }
    
    [JSInvokable]
    public void HandleSpendUpdate(SpendUpdateDto update)
    {
        CurrentSpend = update.TotalSpend;
        BudgetRemaining = update.RemainingBudget ?? 0;
        InvokeAsync(StateHasChanged);
    }
    
    [JSInvokable]
    public void HandleBudgetAlert(BudgetAlertDto alert)
    {
        LatestAlert = new BudgetAlert
        {
            Message = alert.Message,
            Severity = alert.Severity
        };
        InvokeAsync(StateHasChanged);
    }
    
    private string GetAlertClass(string severity) => severity switch
    {
        "critical" => "danger",
        "warning" => "warning",
        _ => "info"
    };
    
    public async ValueTask DisposeAsync()
    {
        if (_spendHub != null)
        {
            await _spendHub.InvokeVoidAsync("disconnect");
            await _spendHub.DisposeAsync();
        }
    }
}
```

## Available Hubs

### Spend Notifications Hub
- **Hub Name**: `spend-notifications`
- **Events**: SpendUpdate, BudgetAlert, SpendSummary, UnusualSpendingDetected
- **Purpose**: Real-time spend tracking and budget monitoring

### Video Generation Hub
- **Hub Name**: `video-generation`
- **Events**: TaskStarted, TaskProgress, TaskCompleted, TaskFailed, TaskCancelled, TaskTimedOut
- **Methods**: SubscribeToTask, UnsubscribeFromTask
- **Purpose**: Track video generation progress

### Image Generation Hub
- **Hub Name**: `image-generation`
- **Events**: TaskStarted, TaskProgress, TaskCompleted, TaskFailed, TaskCancelled
- **Methods**: SubscribeToTask, UnsubscribeFromTask
- **Purpose**: Track image generation progress

### Webhook Delivery Hub
- **Hub Name**: `webhooks`
- **Events**: DeliveryAttempted, DeliverySucceeded, DeliveryFailed, RetryScheduled, DeliveryStatisticsUpdated, CircuitBreakerStateChanged
- **Methods**: SubscribeToWebhooks, UnsubscribeFromWebhooks, RequestStatistics
- **Purpose**: Monitor webhook delivery status

### Navigation State Hub
- **Hub Name**: `navigation-state`
- **Events**: NavigationStateUpdated, ModelMappingUpdated, ProviderHealthUpdated
- **Purpose**: Real-time navigation and provider status updates

### Task Hub (Unified)
- **Hub Name**: `tasks`
- **Events**: TaskStarted, TaskProgress, TaskCompleted, TaskFailed, TaskCancelled, TaskTimedOut
- **Methods**: SubscribeToTask, UnsubscribeFromTask, SubscribeToTaskType, UnsubscribeFromTaskType
- **Purpose**: Unified hub for all async operations

## Troubleshooting

### Enable Debug Mode

```javascript
// Enable detailed logging
signalR.setDebugMode(true);

// Check browser console for detailed logs
```

### Connection Issues

```javascript
// Check connection state
if (!signalR.isConnected('spend-notifications')) {
    console.log('Not connected. State:', signalR.getConnectionState('spend-notifications'));
}

// Get all active connections
const activeHubs = signalR.getActiveConnections();
console.log('Active hubs:', activeHubs);

// Force reconnect
await signalR.disconnectFromHub('spend-notifications');
await signalR.connectToHub('spend-notifications');
```

### Performance Issues

```javascript
// Check metrics
const metrics = signalR.getMetrics('video-generation');
if (metrics.connectionTime?.average > 5000) {
    console.warn('Slow connection times detected');
}

// Disable message queuing if not needed
await signalR.connectToHub('webhooks', virtualKey, {
    enableMessageQueuing: false
});
```

## Best Practices

1. **Use Hub Proxies**: Prefer typed hub proxies for better IntelliSense and type safety
2. **Handle Disconnections**: Always listen for connection state changes in critical features
3. **Clean Up**: Disconnect from hubs when components unmount
4. **Error Handling**: Wrap hub method calls in try-catch blocks
5. **Virtual Key Management**: Update virtual key when it changes
6. **Performance**: Monitor metrics in production to identify issues
7. **Message Queuing**: Disable for hubs that don't need offline support

## Example: Complete Implementation

```javascript
class SpendTracker {
    constructor() {
        this.signalR = window.conduitSignalR;
        this.spendHub = null;
        this.isConnected = false;
    }
    
    async initialize(virtualKey) {
        try {
            // Enable debug mode in development
            if (window.location.hostname === 'localhost') {
                this.signalR.setDebugMode(true);
            }
            
            // Create hub proxy
            this.spendHub = window.conduitHubs.createSpendNotificationsHub();
            
            // Set up connection event handlers
            this._setupConnectionHandlers();
            
            // Connect to hub
            await this.spendHub.connect(virtualKey, {
                maxReconnectAttempts: 15,
                baseReconnectDelay: 3000
            });
            
            // Set up hub event handlers
            this._setupHubHandlers();
            
            this.isConnected = true;
            console.log('Spend tracker initialized');
            
        } catch (error) {
            console.error('Failed to initialize spend tracker:', error);
            throw error;
        }
    }
    
    _setupConnectionHandlers() {
        // Listen for connection state changes
        window.addEventListener('conduit:spend-notifications:stateChanged', (event) => {
            const { previousState, currentState } = event.detail;
            console.log(`Connection state: ${previousState} -> ${currentState}`);
            
            if (currentState === 'connected' && previousState === 'reconnecting') {
                this._onReconnected();
            } else if (currentState === 'failed') {
                this._onConnectionFailed();
            }
        });
    }
    
    _setupHubHandlers() {
        // Spend updates
        this.spendHub.onSpendUpdate((notification) => {
            this._updateSpendDisplay(notification);
        });
        
        // Budget alerts
        this.spendHub.onBudgetAlert((alert) => {
            this._showBudgetAlert(alert);
        });
        
        // Daily summaries
        this.spendHub.onSpendSummary((summary) => {
            this._updateDailySummary(summary);
        });
        
        // Unusual spending
        this.spendHub.onUnusualSpending((notification) => {
            this._handleUnusualSpending(notification);
        });
    }
    
    _updateSpendDisplay(notification) {
        document.getElementById('current-spend').textContent = 
            `$${notification.totalSpend.toFixed(2)}`;
        
        if (notification.budgetPercentage) {
            const progressBar = document.getElementById('budget-progress');
            progressBar.style.width = `${notification.budgetPercentage}%`;
            progressBar.className = this._getProgressClass(notification.budgetPercentage);
        }
    }
    
    _showBudgetAlert(alert) {
        const alertContainer = document.getElementById('alerts');
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${this._getAlertClass(alert.severity)}`;
        alertDiv.textContent = alert.message;
        alertContainer.appendChild(alertDiv);
        
        // Auto-dismiss after 10 seconds
        setTimeout(() => alertDiv.remove(), 10000);
    }
    
    _updateDailySummary(summary) {
        console.log('Daily summary received:', summary);
        // Update UI with daily summary
    }
    
    _handleUnusualSpending(notification) {
        console.warn('Unusual spending detected:', notification);
        // Show warning to user
    }
    
    _onReconnected() {
        console.log('Reconnected to spend notifications');
        // Refresh data after reconnection
    }
    
    _onConnectionFailed() {
        console.error('Failed to connect to spend notifications');
        // Show offline indicator
    }
    
    _getProgressClass(percentage) {
        if (percentage >= 100) return 'progress-bar-danger';
        if (percentage >= 90) return 'progress-bar-warning';
        if (percentage >= 80) return 'progress-bar-info';
        return 'progress-bar-success';
    }
    
    _getAlertClass(severity) {
        return {
            'critical': 'danger',
            'warning': 'warning',
            'info': 'info'
        }[severity] || 'info';
    }
    
    async dispose() {
        if (this.spendHub) {
            await this.spendHub.disconnect();
        }
    }
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', async () => {
    const tracker = new SpendTracker();
    await tracker.initialize('your-virtual-key');
    
    // Clean up on page unload
    window.addEventListener('beforeunload', () => {
        tracker.dispose();
    });
});
```