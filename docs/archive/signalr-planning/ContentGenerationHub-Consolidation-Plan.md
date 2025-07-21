# ContentGenerationHub Consolidation Plan

## Overview
Consolidating ImageGenerationHub and VideoGenerationHub into a unified ContentGenerationHub to reduce duplication and improve maintainability.

## Migration Strategy

### Phase 1: Create New Infrastructure (Completed)
1. ✅ Created ContentGenerationHub.cs - Unified hub for both image and video
2. ✅ Created IContentGenerationNotificationService.cs - Unified interface
3. ✅ Created ContentGenerationNotificationService.cs - Unified implementation

### Phase 2: Update Services (Pending)
1. Update DI registrations to use new services
2. Keep old services temporarily for backward compatibility
3. Mark old services as deprecated

### Phase 3: Update Consumers (Pending)
1. Update image generation consumers to use new service
2. Update video generation consumers to use new service
3. Update any other services that depend on the old notification services

### Phase 4: Update Frontend (Pending)
1. Update frontend to connect to ContentGenerationHub
2. Update event handlers to handle both old and new event formats
3. Test thoroughly

### Phase 5: Remove Legacy Code (Pending)
1. Remove ImageGenerationHub.cs
2. Remove VideoGenerationHub.cs
3. Remove old notification services and interfaces
4. Clean up DI registrations

## Benefits
1. **Single hub to maintain** - Reduces code duplication
2. **Unified event handling** - Consistent event format for both content types
3. **Cross-content notifications** - Can easily add features that work across content types
4. **Better group management** - Unified groups for monitoring all content

## Backward Compatibility
The new ContentGenerationNotificationService sends events to both:
- Legacy groups (image-{taskId}, video-{taskId})
- New unified groups (content-{taskId})

This ensures existing clients continue to work during migration.

## Frontend Migration Guide
```javascript
// Old approach (separate hubs)
const imageHub = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/image-generation")
    .build();

const videoHub = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/video-generation")
    .build();

// New approach (unified hub)
const contentHub = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/content-generation")
    .build();

// Subscribe to specific content type
await contentHub.invoke("SubscribeToTask", taskId, "image");
await contentHub.invoke("SubscribeToTask", taskId, "video");

// Handle events
contentHub.on("ImageGenerationStarted", (data) => { /* ... */ });
contentHub.on("VideoGenerationProgress", (data) => { /* ... */ });

// Or use unified events
contentHub.on("ContentGenerationStarted", (data) => {
    if (data.contentType === "image") {
        // Handle image
    } else if (data.contentType === "video") {
        // Handle video
    }
});
```