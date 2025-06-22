# SignalR Navigation State Implementation

## Overview
This document describes the implementation of real-time navigation state updates in Conduit WebUI, replacing the 30-second polling mechanism with SignalR-based event-driven updates.

## Architecture

### Event Flow
1. Admin API publishes domain events when configuration changes occur
2. Core API event consumers receive these events
3. SignalR hub pushes real-time updates to connected WebUI clients
4. WebUI navigation automatically updates without polling

### Key Components

#### Core API (ConduitLLM.Http)
- **NavigationStateHub**: SignalR hub for real-time updates at `/hubs/navigation-state`
- **NavigationStateNotificationService**: Service for pushing updates through SignalR
- **Event Consumers**: Three consumers that handle domain events and push updates
  - ModelMappingChangedNotificationConsumer
  - ProviderHealthChangedNotificationConsumer
  - ModelCapabilitiesDiscoveredNotificationConsumer

#### WebUI (ConduitLLM.WebUI)
- **SignalRNavigationStateService**: Replaces the polling-based NavigationStateService
- **Features**:
  - Automatic SignalR connection management
  - Reconnection with exponential backoff
  - Graceful fallback to 30-second polling if WebSocket fails
  - Maintains same interface as original service

## Configuration

### Environment Variables
- `CONDUIT_API_BASE_URL`: Core API base URL (defaults to `http://localhost:5000`)
- Standard SignalR configuration applies

### SignalR Groups
- `navigation-updates`: All navigation state updates
- `model-{modelId}`: Model-specific updates

## Benefits
1. **Real-time Updates**: Configuration changes are reflected immediately
2. **Reduced Load**: Eliminates unnecessary API polling every 30 seconds
3. **Better UX**: Users see navigation state changes instantly
4. **Scalable**: Uses event-driven architecture that scales with multiple instances
5. **Resilient**: Automatic reconnection and fallback to polling

## Event Types Handled
- **ModelMappingChanged**: Updates when model mappings are created/updated/deleted
- **ProviderHealthChanged**: Updates when provider health status changes
- **ModelCapabilitiesDiscovered**: Updates when new model capabilities are discovered
- **ModelAvailabilityChanged**: Updates when specific model availability changes

## Implementation Notes
1. The WebUI NavMenu component already subscribes to NavigationStateChanged events
2. SignalR connection is established on service initialization
3. Fallback polling activates after 5 failed reconnection attempts
4. All navigation prerequisite checks remain unchanged

## Future Enhancements
1. Add ProviderHealthChanged event publishing (currently defined but not published)
2. Add more granular update types for specific navigation sections
3. Implement user-specific navigation state updates
4. Add SignalR connection status indicator in UI