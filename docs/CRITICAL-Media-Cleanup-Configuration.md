# CRITICAL: Media Cleanup Configuration for Production

## Issue
Without proper configuration, media files (images/videos) are **NEVER cleaned up** when virtual keys are deleted, leading to:
- **Unbounded S3/storage costs** 
- **Orphaned media files accumulating forever**
- **No way to track storage usage per virtual key**

## Root Cause
The Admin API's `MediaLifecycleService` is only registered when storage provider is configured. Without it, virtual key deletion leaves orphaned media files.

## Solution

### 1. Configure Storage Provider in Admin API
The Admin API needs the **SAME** storage configuration as the Core API:

```bash
# For S3/R2 storage (production)
export CONDUITLLM__STORAGE__PROVIDER=S3
export CONDUITLLM__STORAGE__S3__SERVICEURL=https://your-s3-endpoint.com
export CONDUITLLM__STORAGE__S3__ACCESSKEY=your-access-key
export CONDUITLLM__STORAGE__S3__SECRETKEY=your-secret-key
export CONDUITLLM__STORAGE__S3__BUCKETNAME=conduit-media
export CONDUITLLM__STORAGE__S3__REGION=auto
```

### 2. Enable Auto-Cleanup (Optional but Recommended)
```bash
export CONDUITLLM__MEDIAMANAGEMENT__ENABLEAUTOCLEANUP=true
export CONDUITLLM__MEDIAMANAGEMENT__ORPHANCLEANUPENABLED=true
```

### 3. Verify Configuration
Check Admin API logs on startup for:
```
[AdminVirtualKeyService] Deleting associated media files for virtual key {KeyId}
[MediaLifecycleService] Deleted {Count} media files for virtual key {VirtualKeyId}
```

If you see this instead, media cleanup is NOT working:
```
[AdminVirtualKeyService] Media lifecycle service not available, media files for virtual key {KeyId} will become orphaned
```

## Impact of Not Configuring

Without this configuration:
1. **Every deleted virtual key leaves ALL its media files behind**
2. **Storage costs grow indefinitely**
3. **No automated cleanup mechanism exists**
4. **Manual cleanup becomes necessary**

## Temporary Workarounds

Until properly configured:
1. **S3 Lifecycle Policies**: Auto-delete files older than X days
2. **Manual Cleanup**: Periodically delete orphaned files
3. **Monitor Storage**: Track S3 bucket size and costs

## Testing Media Cleanup

1. Create a virtual key
2. Generate some images/videos with that key
3. Delete the virtual key via Admin API
4. Check logs for cleanup confirmation
5. Verify media files are removed from S3

## Architecture Note

The infrastructure for media lifecycle management is fully implemented:
- `MediaGenerationCompleted` events track all generated media
- `MediaLifecycleService` handles cleanup on virtual key deletion
- `MediaLifecycleHandler` maintains the media-to-virtualkey mapping

The only missing piece is the configuration!