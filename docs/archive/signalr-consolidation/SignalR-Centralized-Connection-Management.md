# SignalR Centralized Connection Management

## Overview

This document describes the centralized SignalR connection management architecture implemented to eliminate duplicate connection tracking and provide a single source of truth for all SignalR connections in the Conduit WebUI.

## Problem Statement

The previous implementation had multiple independent SignalR connection management systems:
- 6+ distinct connection management implementations
- Massive code duplication across components
- Inconsistent state management and naming conventions
- Resource waste from multiple connections to the same hub
- Maintenance complexity requiring changes in multiple locations

## Solution Architecture

### 1. Centralized JavaScript Service

**File**: `/wwwroot/js/conduit-signalr-service.js`

The existing `ConduitSignalRService` is now the single source of truth for all SignalR connections:

```javascript
// Singleton pattern ensures one instance
window.conduitSignalR = window.ConduitSignalRService.getInstance();

// Features:
- Multi-hub support with connection pooling
- Unified connection state management
- Event-driven architecture
- Performance metrics tracking
- Automatic reconnection with exponential backoff
- Message queuing during disconnections
```

### 2. Pure UI Components

#### ConnectionStatusIndicatorV2

**File**: `/Components/ConnectionStatusIndicatorV2.razor`

A pure UI component that:
- Listens to centralized connection state events
- Displays connection status without managing connections
- Shows real-time metrics from centralized service
- Provides consistent UI across all hubs

```csharp
// Listen to centralized events
await JS.InvokeVoidAsync("window.addEventListener", 
    $"conduit:{HubName}:stateChanged", 
    _dotNetRef);

// Get metrics from centralized service
var metrics = await _signalRService.InvokeAsync<JsonElement>("getMetrics", HubName);
```

#### SpendNotificationListenerV2

**File**: `/Components/SpendNotificationListenerV2.razor`

A lightweight wrapper that:
- Uses centralized service for all connection management
- Focuses only on handling spend-specific events
- Delegates connection state to centralized service
- Re-registers handlers automatically on reconnection

```csharp
// Use centralized service to connect
var connection = await _signalRService.InvokeAsync<IJSObjectReference>(
    "connectToHub", HUB_NAME, VirtualKey);

// Listen for state changes from centralized service
await JS.InvokeVoidAsync("window.addEventListener", 
    $"conduit:{HUB_NAME}:stateChanged", 
    _dotNetRef);
```

### 3. C# Connection Manager

**File**: `/Services/SignalRConnectionManager.cs`

A C# service that provides:
- Typed interface to JavaScript SignalR service
- Connection state tracking and events
- Metrics collection and reporting
- Thread-safe connection management
- Simplified API for Blazor components

```csharp
public class SignalRConnectionManager
{
    // Connect to hub with single method
    public async Task<HubConnectionInfo> ConnectToHubAsync(
        string hubName, 
        string? authKey = null, 
        HubConnectionOptions? options = null)
    
    // Get connection state
    public async Task<ConnectionState> GetConnectionStateAsync(string hubName)
    
    // Get performance metrics
    public async Task<HubMetrics?> GetHubMetricsAsync(string hubName)
}
```

## Event-Driven Architecture

### Global Connection Events

The centralized service emits standardized events for all connection state changes:

```javascript
// Event format
window.dispatchEvent(new CustomEvent('conduit:{hubName}:stateChanged', {
    detail: {
        hubName: 'spend-notifications',
        currentState: 'connected',
        previousState: 'connecting',
        timestamp: Date.now()
    }
}));
```

### Event Types

1. **stateChanged**: Connection state transitions
2. **reconnecting**: Reconnection attempts with retry info
3. **reconnected**: Successful reconnection
4. **connectionClosed**: Connection closure with error details
5. **connectionFailed**: Terminal connection failure

## Benefits

### 1. Single Source of Truth
- One connection per hub regardless of consuming components
- Consistent state across entire application
- Centralized configuration and policies

### 2. Resource Efficiency
- Connection pooling prevents duplicate connections
- Shared reconnection logic reduces overhead
- Unified message queuing during disconnections

### 3. Improved Maintainability
- Changes only needed in centralized service
- Consistent error handling and logging
- Simplified debugging with central monitoring

### 4. Better User Experience
- Consistent connection indicators across UI
- Unified reconnection behavior
- Accurate connection state reporting

## Migration Guide

### For Existing Components

1. **Replace connection management code**:
```csharp
// Old
private HubConnection? _connection;
await _connection.StartAsync();

// New
@inject SignalRConnectionManager SignalR
await SignalR.ConnectToHubAsync("hub-name");
```

2. **Use centralized state**:
```csharp
// Old
private ConnectionState _state = ConnectionState.Disconnected;

// New
var state = await SignalR.GetConnectionStateAsync("hub-name");
```

3. **Listen to global events**:
```javascript
// Old - component-specific handling
connection.onclose(() => { /* handle */ });

// New - global event listener
window.addEventListener('conduit:hub-name:stateChanged', (e) => {
    // Handle state change
});
```

### For New Components

1. **Use ConnectionStatusIndicatorV2** for status display
2. **Inject SignalRConnectionManager** for connection management
3. **Create thin wrappers** for hub-specific functionality
4. **Avoid direct SignalR connection management**

## Best Practices

### 1. Component Design
- Keep components focused on their primary purpose
- Delegate all connection management to centralized service
- Use event-driven patterns for state updates

### 2. Error Handling
- Let centralized service handle connection errors
- Focus component error handling on business logic
- Use consistent error reporting patterns

### 3. Performance
- Reuse existing connections via centralized service
- Avoid polling for connection state
- Use event listeners for real-time updates

### 4. Testing
- Mock SignalRConnectionManager for unit tests
- Test components independently of connection logic
- Use integration tests for end-to-end scenarios

## Example Usage

### Simple Connection Status

```razor
<!-- Use the pure UI component -->
<ConnectionStatusIndicatorV2 HubName="spend-notifications" />
```

### Hub-Specific Listener

```razor
<!-- Use lightweight wrapper -->
<SpendNotificationListenerV2 
    VirtualKey="@virtualKey"
    OnSpendUpdate="HandleSpendUpdate"
    OnBudgetAlert="HandleBudgetAlert" />
```

### Direct Connection Management

```csharp
@inject SignalRConnectionManager SignalR

// Connect to hub
var connection = await SignalR.ConnectToHubAsync("my-hub", virtualKey);

// Invoke hub method
var result = await SignalR.InvokeAsync<string>("my-hub", "GetData", param1);

// Listen for events
SignalR.ConnectionStateChanged += (sender, args) => {
    if (args.HubName == "my-hub") {
        // Handle state change
    }
};
```

## Monitoring and Debugging

### Browser Console

```javascript
// Get connection states
conduitSignalR.getActiveConnections()

// Check specific hub
conduitSignalR.getConnectionState('spend-notifications')

// View metrics
conduitSignalR.getMetrics('spend-notifications')

// Enable debug mode
conduitSignalR.setDebugMode(true)
```

### Component Diagnostics

```csharp
// Get all connections
var connections = SignalR.GetAllConnections();

// Check hub metrics
var metrics = await SignalR.GetHubMetricsAsync("hub-name");
```

## Future Enhancements

1. **Connection Health Dashboard**: Visual representation of all hub connections
2. **Automatic Failover**: Switch between SignalR and polling based on connection health
3. **Connection Policies**: Hub-specific reconnection and timeout policies
4. **Metrics Dashboard**: Real-time performance monitoring across all hubs
5. **Connection Prioritization**: Ensure critical hubs reconnect first