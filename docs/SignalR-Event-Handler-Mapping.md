# SignalR Event Handler Mapping Issue

## Problem
The AdminNotificationListener.razor component is registering event handlers by passing a DotNetObjectReference to JavaScript, but the JavaScript doesn't know which .NET method to invoke when events are received.

## Current Pattern
```javascript
// AdminNotificationListener.razor
await _navigationHub.InvokeVoidAsync("onProviderHealthChanged", _dotNetRef);
```

This passes the entire .NET object reference, but JavaScript needs to know to call `HandleProviderHealthChanged` method.

## Solution Options

### Option 1: Create JavaScript wrapper functions
Modify the hub proxy to accept a DotNetObjectReference and method name:

```javascript
// In hub proxy
onProviderHealthChanged(dotNetRef, methodName = "HandleProviderHealthChanged") {
    this.on('ProviderHealthUpdate', async (data) => {
        await dotNetRef.invokeMethodAsync(methodName, data);
    });
}
```

### Option 2: Use a JavaScript adapter pattern
Create an adapter that maps event names to method names:

```javascript
// Event to method mapping
const eventMethodMap = {
    'ProviderHealthUpdate': 'HandleProviderHealthChanged',
    'ModelCapabilityUpdate': 'HandleModelDiscovered',
    'VirtualKeyUpdate': 'HandleVirtualKeyUpdate',
    'HighSpendAlert': 'HandleHighSpendAlert',
    'SecurityAlert': 'HandleSecurityAlert',
    'SystemAnnouncement': 'HandleSystemAlert'
};
```

### Option 3: Pass method names from Blazor
Have Blazor components specify the method name:

```csharp
// In Blazor component
await _navigationHub.InvokeVoidAsync("registerHandler", 
    "onProviderHealthChanged", 
    _dotNetRef, 
    "HandleProviderHealthChanged");
```

## Recommended Solution
Option 1 is the cleanest - modify the JavaScript hub proxy methods to properly handle DotNetObjectReference and invoke the correct method.