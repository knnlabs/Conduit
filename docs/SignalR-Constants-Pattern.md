# SignalR Constants Pattern

## Overview

To prevent runtime failures from mismatched SignalR hub names, method names, and event names, Conduit uses a centralized constants pattern. All SignalR-related strings are defined in `ConduitLLM.Core.Constants.SignalRConstants`.

## Benefits

1. **Compile-time Safety**: Typos and mismatches are caught at compile time
2. **Single Source of Truth**: All SignalR identifiers in one place
3. **Refactoring Safety**: Renaming hubs or methods updates all references
4. **IntelliSense Support**: IDE autocomplete for all SignalR identifiers
5. **Documentation**: Self-documenting code with XML comments

## Constants Structure

### Hub Endpoints
```csharp
SignalRConstants.Hubs.NavigationState     // "/hubs/navigation-state"
SignalRConstants.Hubs.ImageGeneration     // "/hubs/image-generation"
SignalRConstants.Hubs.VideoGeneration     // "/hubs/video-generation"
SignalRConstants.Hubs.SystemNotification  // "/hubs/notifications"
SignalRConstants.Hubs.Task               // "/hubs/tasks"
```

### Hub Methods (Server-side)
```csharp
SignalRConstants.HubMethods.SubscribeToTask      // "SubscribeToTask"
SignalRConstants.HubMethods.UnsubscribeFromTask  // "UnsubscribeFromTask"
SignalRConstants.HubMethods.Subscribe            // "Subscribe"
SignalRConstants.HubMethods.Unsubscribe          // "Unsubscribe"
```

### Client Methods (Events)
```csharp
// Generic task events
SignalRConstants.ClientMethods.TaskProgress
SignalRConstants.ClientMethods.TaskCompleted
SignalRConstants.ClientMethods.TaskFailed
SignalRConstants.ClientMethods.TaskStarted

// Legacy specific events (for backward compatibility)
SignalRConstants.ClientMethods.VideoGenerationStarted
SignalRConstants.ClientMethods.VideoGenerationProgress
SignalRConstants.ClientMethods.VideoGenerationCompleted
SignalRConstants.ClientMethods.VideoGenerationFailed

SignalRConstants.ClientMethods.ImageGenerationStarted
SignalRConstants.ClientMethods.ImageGenerationProgress
SignalRConstants.ClientMethods.ImageGenerationCompleted
SignalRConstants.ClientMethods.ImageGenerationFailed
SignalRConstants.ClientMethods.ImageGenerationCancelled
```

### Group Names
```csharp
SignalRConstants.Groups.ImageTask(taskId)  // Returns "image-{taskId}"
SignalRConstants.Groups.VideoTask(taskId)  // Returns "video-{taskId}"
SignalRConstants.Groups.Task(taskId)       // Returns "task-{taskId}"
SignalRConstants.Groups.AuthenticatedUsers // "authenticated-users"
SignalRConstants.Groups.AdminUsers         // "admin-users"
```

## Usage Examples

### Hub Implementation
```csharp
// In VideoGenerationHub.cs
await Groups.AddToGroupAsync(Context.ConnectionId, 
    SignalRConstants.Groups.VideoTask(taskId));
```

### Notification Service
```csharp
// In VideoGenerationNotificationService.cs
await _hubContext.Clients
    .Group(SignalRConstants.Groups.VideoTask(taskId))
    .SendAsync(SignalRConstants.ClientMethods.VideoGenerationProgress, data);
```

### WebUI Client
```csharp
// In ServerSideSignalRService.cs
await connection.InvokeAsync(
    SignalRConstants.HubMethods.SubscribeToTask, taskId);
```

## Migration Guide

When updating existing code:

1. Add using statement:
   ```csharp
   using ConduitLLM.Core.Constants;
   ```

2. Replace hardcoded strings:
   ```csharp
   // Before
   await Groups.AddToGroupAsync(Context.ConnectionId, $"video-{taskId}");
   
   // After
   await Groups.AddToGroupAsync(Context.ConnectionId, 
       SignalRConstants.Groups.VideoTask(taskId));
   ```

3. Update hub URL references:
   ```csharp
   // Before
   var hubUrl = "/hubs/video-generation";
   
   // After
   var hubUrl = SignalRConstants.Hubs.VideoGeneration;
   ```

## Adding New Constants

When adding new SignalR functionality:

1. Add constants to `SignalRConstants.cs`
2. Use XML documentation
3. Follow existing naming patterns
4. Update both Core and WebUI constants if needed
5. Update this documentation

## Testing

The constants pattern makes testing easier:
- Mock hub connections using the same constants
- Verify correct group names in unit tests
- Ensure event names match between sender and receiver