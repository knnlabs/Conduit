# Event Handler Naming Conventions

This document outlines the naming conventions for event handlers in Conduit.

## General Convention

Event handlers should be named after the event they consume, with "Handler" suffix:

```
{EventName}Handler : IConsumer<{EventName}>
```

## Examples

### Single Event Handlers

- `VideoGenerationCompletedHandler` : `IConsumer<VideoGenerationCompleted>`
- `SpendUpdatedHandler` : `IConsumer<SpendUpdated>`
- `ModelCapabilitiesDiscoveredHandler` : `IConsumer<ModelCapabilitiesDiscovered>`

### Multi-Event Handlers

When a handler consumes multiple related events, use a descriptive name that indicates the handler's purpose:

- `VirtualKeyCacheInvalidationHandler` : 
  - `IConsumer<VirtualKeyCreated>`
  - `IConsumer<VirtualKeyUpdated>`
  - `IConsumer<VirtualKeyDeleted>`
  - `IConsumer<SpendUpdated>`

- `ProviderCredentialEventHandler` :
  - `IConsumer<ProviderCredentialUpdated>`
  - `IConsumer<ProviderCredentialDeleted>`

## Special Purpose Handlers

Some handlers have names that reflect their specific purpose rather than just the event name:

- `ModelDiscoveryNotificationHandler` - Handles `ModelCapabilitiesDiscovered` for SignalR notifications
- `MediaLifecycleHandler` - Handles `MediaGenerationCompleted` for lifecycle management

## Guidelines

1. **Use Event Name**: For single-event handlers, always use the event name + "Handler"
2. **Be Descriptive**: For multi-event handlers, use a name that describes what the handler does
3. **Avoid Redundancy**: Don't include "Event" in the handler name unless it adds clarity
4. **Purpose-Specific**: If a handler has a specific purpose beyond just handling the event, name it accordingly

## Migration

When renaming handlers:
1. Update the file name
2. Update the class name
3. Update logger generic type parameter
4. Update all references in DI registration