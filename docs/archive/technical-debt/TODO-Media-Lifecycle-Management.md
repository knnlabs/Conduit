# TODO: Media Lifecycle Management

## Problem Statement
Currently, media files (images/videos) generated through Conduit are stored in CDN/S3 but lack proper ownership tracking and lifecycle management. When virtual keys are deleted, their associated media remains orphaned in storage, leading to:
- Ever-growing storage costs
- Security concerns (content from banned keys persists)
- No ability to track storage usage per virtual key
- Compliance issues with data retention requirements

## Current Gaps
1. **No Database Tracking**: Media ownership only exists in storage metadata
2. **No Cleanup on Deletion**: Virtual key deletion doesn't remove associated media
3. **No Maintenance Tasks**: No automated cleanup of expired/orphaned content
4. **No Access Control**: Media URLs work without ownership validation

## Proposed Implementation

### Phase 1: Database Schema (High Priority)
Create a media tracking table:

```sql
CREATE TABLE MediaRecords (
    Id UUID PRIMARY KEY,
    StorageKey VARCHAR(500) NOT NULL UNIQUE,
    VirtualKeyId UUID NOT NULL,
    MediaType VARCHAR(50) NOT NULL, -- 'image' or 'video'
    ContentType VARCHAR(100),
    SizeBytes BIGINT,
    ContentHash VARCHAR(64),
    Provider VARCHAR(50),
    Model VARCHAR(100),
    Prompt TEXT,
    StorageUrl TEXT,
    PublicUrl TEXT,
    ExpiresAt TIMESTAMP,
    CreatedAt TIMESTAMP NOT NULL,
    LastAccessedAt TIMESTAMP,
    AccessCount INT DEFAULT 0,
    FOREIGN KEY (VirtualKeyId) REFERENCES VirtualKeys(Id) ON DELETE CASCADE
);

CREATE INDEX IX_MediaRecords_VirtualKeyId ON MediaRecords(VirtualKeyId);
CREATE INDEX IX_MediaRecords_ExpiresAt ON MediaRecords(ExpiresAt);
CREATE INDEX IX_MediaRecords_CreatedAt ON MediaRecords(CreatedAt);
```

### Phase 2: Track Media Creation (High Priority)
Update image/video generation to record in database:

```csharp
// In ImagesController after storing to CDN
var mediaRecord = new MediaRecord
{
    Id = Guid.NewGuid(),
    StorageKey = storageResult.StorageKey,
    VirtualKeyId = virtualKey.Id,
    MediaType = "image",
    ContentType = metadata.ContentType,
    SizeBytes = storageResult.SizeBytes,
    ContentHash = storageResult.ContentHash,
    Provider = mapping.ProviderName,
    Model = request.Model,
    Prompt = request.Prompt,
    StorageUrl = storageResult.Url,
    PublicUrl = storageResult.PublicUrl,
    CreatedAt = DateTime.UtcNow
};
await _mediaRepository.CreateAsync(mediaRecord);
```

### Phase 3: Virtual Key Deletion Cleanup (High Priority)
Add media cleanup to virtual key deletion:

```csharp
// In AdminVirtualKeyService.DeleteVirtualKeyAsync
public async Task DeleteVirtualKeyAsync(Guid keyId)
{
    // ... existing deletion logic ...
    
    // Cleanup associated media
    var mediaRecords = await _mediaRepository.GetByVirtualKeyIdAsync(keyId);
    foreach (var media in mediaRecords)
    {
        try
        {
            await _storageService.DeleteAsync(media.StorageKey);
            _logger.LogInformation("Deleted media {StorageKey} for virtual key {KeyId}", 
                media.StorageKey, keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media {StorageKey}", media.StorageKey);
        }
    }
    
    // Database cascade delete will remove MediaRecords
}
```

### Phase 4: Maintenance Tasks (Medium Priority)
Add to VirtualKeyMaintenanceService:

```csharp
// Run daily
public async Task CleanupExpiredMediaAsync()
{
    var expiredMedia = await _mediaRepository.GetExpiredMediaAsync(DateTime.UtcNow);
    foreach (var media in expiredMedia)
    {
        await _storageService.DeleteAsync(media.StorageKey);
        await _mediaRepository.DeleteAsync(media.Id);
    }
}

// Run weekly
public async Task CleanupOrphanedMediaAsync()
{
    // Find media records with non-existent virtual keys
    var orphaned = await _mediaRepository.GetOrphanedMediaAsync();
    foreach (var media in orphaned)
    {
        await _storageService.DeleteAsync(media.StorageKey);
        await _mediaRepository.DeleteAsync(media.Id);
    }
}

// Run monthly
public async Task PruneOldMediaAsync(int daysToKeep = 90)
{
    var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
    var oldMedia = await _mediaRepository.GetMediaOlderThanAsync(cutoffDate);
    
    foreach (var media in oldMedia)
    {
        // Skip if accessed recently
        if (media.LastAccessedAt > DateTime.UtcNow.AddDays(-30))
            continue;
            
        await _storageService.DeleteAsync(media.StorageKey);
        await _mediaRepository.DeleteAsync(media.Id);
    }
}
```

### Phase 5: Access Control (Low Priority)
Add ownership validation to MediaController:

```csharp
[HttpGet("v1/media/{**storageKey}")]
public async Task<IActionResult> GetMedia(string storageKey)
{
    // Option 1: Public access (current behavior)
    // Option 2: Validate ownership
    var mediaRecord = await _mediaRepository.GetByStorageKeyAsync(storageKey);
    if (mediaRecord != null)
    {
        // Update access stats
        await _mediaRepository.UpdateAccessStatsAsync(mediaRecord.Id);
        
        // Optional: Validate ownership
        // var virtualKey = await GetVirtualKeyFromRequest();
        // if (mediaRecord.VirtualKeyId != virtualKey.Id)
        //     return Unauthorized();
    }
    
    // ... serve media ...
}
```

### Phase 6: Admin Tools (Low Priority)
Add admin endpoints for media management:

```csharp
// GET /api/admin/media/stats
// - Total storage used
// - Storage by virtual key
// - Storage by provider
// - Orphaned media count

// DELETE /api/admin/media/cleanup
// - Trigger manual cleanup
// - Prune old media
// - Remove orphaned files

// GET /api/admin/virtual-keys/{id}/media
// - List all media for a virtual key
// - Show storage usage
```

## Implementation Order
1. **Immediate**: Add TODO comments in code
2. **Next Sprint**: Database schema and creation tracking
3. **Following Sprint**: Deletion cleanup and basic maintenance
4. **Future**: Access control and admin tools

## Configuration Options
```yaml
ConduitLLM:
  MediaManagement:
    EnableOwnershipTracking: true
    EnableAutoCleanup: true
    MediaRetentionDays: 90
    OrphanCleanupEnabled: true
    AccessControlEnabled: false  # Start with public access
```

## Monitoring and Alerts
- Alert when storage exceeds threshold
- Alert on cleanup failures
- Track storage growth rate
- Monitor orphaned media count

## Alternative Approaches
1. **S3 Lifecycle Policies**: Use S3/R2 lifecycle rules for automatic expiration
2. **Signed URLs**: Use time-limited signed URLs instead of permanent public URLs
3. **Lazy Deletion**: Mark as deleted in DB, cleanup in batches
4. **Event-Driven**: Use domain events for media lifecycle (MediaCreated, MediaDeleted)

## Notes
- Consider GDPR compliance for media deletion
- Plan for gradual rollout with feature flags
- Consider CDN cache invalidation on deletion
- Document media retention policy for users