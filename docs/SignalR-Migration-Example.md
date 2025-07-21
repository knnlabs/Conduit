# SignalR Migration Example: VirtualKeysDashboard

This example shows how to migrate the VirtualKeysDashboard component from using independent SignalR connection management to the centralized approach.

## Before Migration

```razor
@page "/virtual-keys"
@using ConduitLLM.WebUI.Components

<div class="dashboard">
    <!-- Two components tracking the same connection independently -->
    <SpendNotificationListener 
        VirtualKey="@masterVirtualKey" 
        OnConnectionStateChanged="HandleConnectionStateChanged"
        OnSpendUpdate="HandleSpendUpdate"
        OnBudgetAlert="HandleBudgetAlert" />
    
    <ConnectionStatusIndicator 
        HubName="spend-notifications" 
        OnConnectionStateChanged="HandleConnectionStatusChanged" />
    
    <!-- Rest of dashboard content -->
</div>

@code {
    private string masterVirtualKey = "";
    private ConnectionState spendConnectionState;
    private HubConnectionState indicatorConnectionState;
    
    // Duplicate connection state handlers
    private void HandleConnectionStateChanged(ConnectionState state)
    {
        spendConnectionState = state;
        // Handle state change
    }
    
    private void HandleConnectionStatusChanged(HubConnectionState state)
    {
        indicatorConnectionState = state;
        // Handle same state change differently
    }
}
```

### Problems with This Approach

1. **Two components managing the same hub connection**
2. **Duplicate state tracking** (ConnectionState vs HubConnectionState)
3. **Inconsistent state handling**
4. **Potential for multiple connections to same hub**
5. **Complex state synchronization**

## After Migration

```razor
@page "/virtual-keys"
@using ConduitLLM.WebUI.Components
@inject SignalRConnectionManager SignalR

<div class="dashboard">
    <!-- Single connection status indicator using centralized state -->
    <ConnectionStatusIndicatorV2 
        HubName="spend-notifications" 
        OnConnectionStateChanged="HandleConnectionStateChanged" />
    
    <!-- Lightweight listener focusing only on spend events -->
    <SpendNotificationListenerV2 
        VirtualKey="@masterVirtualKey"
        OnSpendUpdate="HandleSpendUpdate"
        OnBudgetAlert="HandleBudgetAlert" />
    
    <!-- Rest of dashboard content -->
</div>

@code {
    private string masterVirtualKey = "";
    private string connectionState = "disconnected";
    
    protected override async Task OnInitializedAsync()
    {
        // Set authentication
        await SignalR.SetVirtualKeyAsync(masterVirtualKey);
        
        // Listen for connection state changes
        SignalR.ConnectionStateChanged += OnGlobalConnectionStateChanged;
        
        // Connection is managed by SpendNotificationListenerV2
        // No need to manually connect here
    }
    
    // Single, consistent state handler
    private void HandleConnectionStateChanged(string state)
    {
        connectionState = state;
        StateHasChanged();
    }
    
    // Global connection state handler for all hubs
    private void OnGlobalConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.HubName == "spend-notifications")
        {
            connectionState = e.CurrentState.ToString().ToLower();
            InvokeAsync(StateHasChanged);
        }
    }
    
    // Spend event handlers remain the same
    private async Task HandleSpendUpdate(SpendUpdateNotification notification)
    {
        // Handle spend update
    }
    
    private async Task HandleBudgetAlert(BudgetAlertNotification notification)
    {
        // Handle budget alert
    }
    
    public void Dispose()
    {
        SignalR.ConnectionStateChanged -= OnGlobalConnectionStateChanged;
    }
}
```

## Key Improvements

### 1. Simplified State Management
- Single connection state type (string)
- One source of truth for connection state
- Consistent state handling across components

### 2. Component Responsibilities
- **ConnectionStatusIndicatorV2**: Pure UI, displays centralized state
- **SpendNotificationListenerV2**: Handles spend events only
- **VirtualKeysDashboard**: Orchestrates components, no connection management

### 3. Resource Efficiency
- Single connection to spend-notifications hub
- No duplicate state tracking
- Centralized reconnection handling

### 4. Better Error Handling
```csharp
// Centralized error information
private void OnGlobalConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
{
    if (e.CurrentState == ConnectionState.Failed)
    {
        // Show error to user
        ShowError($"Connection failed: {e.Error}");
    }
}
```

### 5. Enhanced Monitoring
```csharp
// Get connection metrics
private async Task ShowConnectionMetrics()
{
    var metrics = await SignalR.GetHubMetricsAsync("spend-notifications");
    if (metrics != null)
    {
        Console.WriteLine($"Latency: {metrics.AverageLatency}ms");
        Console.WriteLine($"Messages: {metrics.MessageCount}");
    }
}
```

## Migration Checklist

- [ ] Replace old components with V2 versions
- [ ] Remove duplicate connection state tracking
- [ ] Inject SignalRConnectionManager service
- [ ] Update state change handlers to use centralized events
- [ ] Remove manual connection management code
- [ ] Test connection state transitions
- [ ] Verify event handlers still work correctly
- [ ] Check for memory leaks (dispose event handlers)

## Common Patterns

### Pattern 1: Status + Listener
```razor
<!-- Show connection status -->
<ConnectionStatusIndicatorV2 HubName="@hubName" />

<!-- Handle hub-specific events -->
<HubSpecificListenerV2 
    AuthKey="@authKey"
    OnEvent="HandleEvent" />
```

### Pattern 2: Manual Connection Management
```csharp
@inject SignalRConnectionManager SignalR

// Connect manually
await SignalR.ConnectToHubAsync("custom-hub", authKey);

// Register handlers
await SignalR.OnAsync("custom-hub", "CustomEvent", HandleCustomEvent);

// Disconnect when done
await SignalR.DisconnectFromHubAsync("custom-hub");
```

### Pattern 3: Global State Monitoring
```csharp
// Monitor all connections
SignalR.ConnectionStateChanged += (s, e) => {
    Logger.LogInformation("Hub {Hub} changed from {From} to {To}", 
        e.HubName, e.PreviousState, e.CurrentState);
};
```

## Testing the Migration

1. **Connection State**: Verify single connection per hub
2. **Event Delivery**: Ensure all events still fire correctly
3. **Reconnection**: Test automatic reconnection works
4. **Performance**: Check for improved resource usage
5. **Error Handling**: Verify errors are properly displayed