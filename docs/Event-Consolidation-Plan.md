# Event Consolidation Plan

## Current State Analysis

### Provider Health Events
- **MassTransit Domain Event**: `ProviderHealthChanged` - The source of truth
- **Duplicate SignalR Events**:
  - `ProviderHealthUpdate` (Admin API)
  - `ProviderHealthChanged` (Core API - SystemNotificationService)
  - `OnProviderHealthChanged` (Core API - NavigationStateNotificationService)

### System Announcement Events
- **No MassTransit Event** - Direct SignalR only
- **Duplicate SignalR Events**:
  - `SystemAnnouncement` (Admin API)
  - `SystemAnnouncement` (Core API - SystemNotificationService)

### Model Capabilities Events
- **MassTransit Domain Event**: `ModelCapabilitiesDiscovered` - The source of truth
- **Duplicate SignalR Events**:
  - `ModelCapabilityUpdate` (Admin API)
  - `OnModelCapabilitiesDiscovered` (Core API - NavigationStateNotificationService)

## Consolidation Strategy

### Phase 1: Provider Health Events ✅
1. Keep `ProviderHealthChanged` MassTransit event as the source ✓
2. Keep `OnProviderHealthChanged` SignalR event in NavigationStateNotificationService ✓
3. Remove direct publishing from SystemNotificationService ✓
4. Have AdminNotificationService consume the MassTransit event and republish for admin clients ✓

### Phase 2: System Announcement Events
1. Create a MassTransit `SystemAnnouncementRequested` event
2. Have both Admin and Core APIs consume this event
3. Remove direct SignalR publishing from services

### Phase 3: Model Capabilities Events
1. Keep `ModelCapabilitiesDiscovered` MassTransit event as the source
2. Keep `OnModelCapabilitiesDiscovered` SignalR event in NavigationStateNotificationService
3. Have AdminNotificationService consume the MassTransit event and republish for admin clients

## Service Responsibility Matrix

| Service | Responsibility | Publishes | Consumes |
|---------|---------------|-----------|----------|
| ProviderHealthMonitoringService | Monitor health | ProviderHealthChanged (MassTransit) | - |
| NavigationStateNotificationService | UI state updates | OnProviderHealthChanged (SignalR) | ProviderHealthChanged |
| AdminNotificationService | Admin notifications | ProviderHealthUpdate (SignalR) | ProviderHealthChanged |
| SystemNotificationService | System-wide alerts | RateLimitWarning, ServiceDegraded | - |

## Implementation Order
1. Remove duplicate provider health SignalR publishing
2. Add MassTransit consumers where needed
3. Consolidate system announcements
4. Clean up model capabilities events