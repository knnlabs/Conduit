# Cross-Hub Forwarding Fix Plan

## Current Issue
The TaskNotificationService is sending events to multiple hubs:
1. TaskHub (the unified hub)
2. ImageGenerationHub (legacy hub)
3. VideoGenerationHub (legacy hub)

This creates:
- Duplicate events for clients
- Unclear service boundaries
- Maintenance complexity

## Fix Strategy

### Option 1: Remove Cross-Hub Forwarding (Recommended)
- Keep events in their respective hubs only
- TaskHub handles generic task events
- ImageGenerationHub handles image-specific events
- VideoGenerationHub handles video-specific events

### Option 2: Use TaskHub as Primary
- Make TaskHub the primary hub for all task events
- Keep specific hubs for specialized events only
- Remove duplicate forwarding

## Implementation Plan

1. **Remove SendToLegacyHub methods from TaskNotificationService**
   - Delete SendToLegacyHub and SendToLegacyHubForTask methods
   - Remove calls to these methods from all notification methods

2. **Update Task Notification Methods**
   - Keep only TaskHub notifications in TaskNotificationService
   - Remove ImageGenerationHub and VideoGenerationHub contexts

3. **Ensure Specific Services Handle Their Events**
   - ImageGenerationNotificationService handles all image events
   - VideoGenerationNotificationService handles all video events
   - No cross-forwarding between hubs

4. **Update Frontend to Subscribe Correctly**
   - For generic task tracking: Subscribe to TaskHub
   - For image-specific events: Subscribe to ImageGenerationHub
   - For video-specific events: Subscribe to VideoGenerationHub

## Benefits
- Clear separation of concerns
- No duplicate events
- Better performance (no redundant sending)
- Easier to maintain and debug