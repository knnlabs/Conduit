# Epic: Media Generation System Improvements

## Overview
Improve the media generation system by addressing memory efficiency, error handling, and automation issues discovered during system analysis.

## Background
Current implementation loads entire media files into memory before processing, which can cause OOM errors for large files. Additionally, video generation UI lacks complete error handling, and S3 bucket CORS requires manual configuration.

## Goals
1. Implement streaming-based file processing to eliminate memory bottlenecks
2. Add comprehensive error handling for all video generation states
3. Automate CORS configuration for S3-compatible storage
4. (Stretch) Add media optimization features

## Success Criteria
- Large files (>100MB) can be processed without loading entirely into memory
- Video generation UI handles all possible task states gracefully
- S3 buckets are automatically configured with proper CORS rules
- Memory usage remains constant regardless of file size

## Technical Approach

### 1. Stream-Based File Processing

#### 1.1 Core Storage Service Updates
**File**: `ConduitLLM.Core/Services/S3MediaStorageService.cs`

**Current Issue**:
```csharp
// Current: Entire file in memory
var videoBytes = Convert.FromBase64String(video.B64Json);
using var videoStream = new MemoryStream(videoBytes);
```

**Tasks**:
- [ ] Modify `StoreAsync` to use streaming with `TransferUtility` for files >5MB
- [ ] Implement chunked base64 decoding using `CryptoStream` with `FromBase64Transform`
- [ ] Add `IProgress<long>` parameter for upload progress tracking
- [ ] Use `PutObjectRequest.InputStream` directly without buffering
- [ ] Implement automatic multipart upload for files >100MB

**Acceptance Criteria**:
- Memory usage stays under 50MB for 1GB file uploads
- Progress callbacks fire at least every 1MB uploaded
- Uploads can be cancelled mid-stream

**Edge Cases**:
- Network interruption during streaming
- Base64 data with invalid characters
- S3 service throttling during multipart upload
- Concurrent uploads to same storage key

#### 1.2 Image Controller Streaming
**File**: `ConduitLLM.Http/Controllers/ImagesController.cs`

**Tasks**:
- [ ] Replace `HttpClient.GetAsync` with `HttpClient.GetStreamAsync`
- [ ] Implement streaming pipeline: HTTP → Transform → S3
- [ ] Add request timeout handling for slow image downloads
- [ ] Implement retry logic with exponential backoff

**Acceptance Criteria**:
- External images stream directly to S3 without full buffering
- Memory usage remains constant for any image size
- Failed downloads are retried up to 3 times

**Edge Cases**:
- Content-Length header missing or incorrect
- Server closes connection mid-download
- Redirect chains (301/302) with large images
- Content-Type changes during redirect

#### 1.3 Video Orchestrator Streaming
**File**: `ConduitLLM.Core/Services/VideoGenerationOrchestrator.cs`

**Tasks**:
- [ ] Implement streaming download for external video URLs
- [ ] Use multipart upload for videos >5MB
- [ ] Add progress reporting to task status
- [ ] Implement chunked processing for base64 videos

**Acceptance Criteria**:
- 500MB videos can be processed with <100MB memory usage
- Task progress updates show download/upload percentage
- Multipart uploads resume on failure

**Complications**:
- MiniMax API may not provide Content-Length
- Need to handle videos with unknown duration
- Progress calculation when size is unknown

### 2. Video Generation UI Error Handling

#### 2.1 Hook Improvements
**File**: `ConduitLLM.WebUI/src/app/videos/hooks/useVideoGeneration.ts`

**Current Issue**:
```typescript
// Missing TimedOut handling
else if (taskStatus.status === 'Failed' || taskStatus.status === 'Cancelled') {
  // No TimedOut case
}
```

**Tasks**:
- [ ] Add `TimedOut` status handling with specific error message
- [ ] Implement retry mechanism with exponential backoff
- [ ] Add `retryCount` to task state
- [ ] Implement max retry limit (3 attempts)
- [ ] Add different polling intervals based on task age

**Acceptance Criteria**:
- All possible task states have UI representation
- Failed tasks can be retried with single click
- Retry attempts are tracked and limited
- Clear error messages for each failure type

**Edge Cases**:
- Task status endpoint returns 404
- Task status changes from Completed to Failed
- Polling continues after component unmount
- Multiple rapid retry clicks

#### 2.2 UI Components
**File**: `ConduitLLM.WebUI/src/app/videos/components/VideoQueue.tsx`

**Tasks**:
- [ ] Add retry button for failed/timed out tasks
- [ ] Show time elapsed and estimated remaining
- [ ] Add bulk retry for multiple failed tasks
- [ ] Implement task priority queue

**Open Questions**:
- Should retry use same parameters or allow editing?
- How long to keep failed tasks in history?
- Should we implement task queue limits?

### 3. Automated CORS Configuration

#### 3.1 CORS Detection and Setup
**File**: `ConduitLLM.Core/Services/S3MediaStorageService.cs`

**Tasks**:
- [ ] Add `ConfigureBucketCorsAsync` method
- [ ] Detect if bucket has CORS rules on startup
- [ ] Apply standard CORS configuration if missing
- [ ] Add configuration option to disable auto-CORS
- [ ] Log warnings if CORS setup fails (permissions)

**CORS Configuration**:
```xml
<CORSConfiguration>
  <CORSRule>
    <AllowedMethod>GET</AllowedMethod>
    <AllowedMethod>HEAD</AllowedMethod>
    <AllowedOrigin>*</AllowedOrigin>
    <AllowedHeader>*</AllowedHeader>
    <ExposeHeader>ETag</ExposeHeader>
    <ExposeHeader>Content-Length</ExposeHeader>
    <ExposeHeader>Content-Type</ExposeHeader>
    <MaxAgeSeconds>3600</MaxAgeSeconds>
  </CORSRule>
</CORSConfiguration>
```

**Acceptance Criteria**:
- CORS is configured automatically on first run
- Existing CORS rules are not overwritten
- Failed CORS setup doesn't prevent service startup
- Configuration can be disabled via settings

**Complications**:
- IAM permissions may not include `s3:PutBucketCors`
- Some S3-compatible services don't support CORS API
- Cloudflare R2 has different CORS API

### 4. (Stretch) Media Optimization

#### 4.1 Image Optimization
**Tasks**:
- [ ] Add WebP conversion option for PNG/JPEG
- [ ] Generate thumbnails (256x256) for gallery
- [ ] Implement progressive JPEG encoding
- [ ] Add image metadata stripping option

#### 4.2 Video Thumbnails
**Tasks**:
- [ ] Extract first frame using FFMpegCore
- [ ] Generate multiple thumbnails for timeline
- [ ] Store thumbnails with predictable naming
- [ ] Return thumbnail URLs in API response

**Open Questions**:
- Should optimization be opt-in or opt-out?
- What quality settings for WebP conversion?
- How many timeline thumbnails for videos?

## Testing Requirements

### Unit Tests
- Stream processing with various file sizes
- Base64 decoding with invalid data
- Multipart upload failure scenarios
- CORS configuration with different permissions
- Task retry logic with max attempts

### Integration Tests
- Large file upload end-to-end (use test file generator)
- Video generation with network interruptions
- CORS verification after bucket setup
- Memory usage monitoring during uploads

### Performance Tests
- Memory usage with 1GB file upload
- Concurrent upload performance
- Streaming vs buffered performance comparison

## Dependencies
- AWS SDK already supports streaming
- May need `System.IO.Pipelines` for efficient streaming
- FFMpegCore for video thumbnail extraction (optional)
- ImageSharp for image optimization (optional)

## Risks and Mitigations

### Risk: Breaking Changes
**Mitigation**: Add feature flags for gradual rollout

### Risk: S3 API Limits
**Mitigation**: Implement adaptive rate limiting

### Risk: Memory Leaks in Streaming
**Mitigation**: Comprehensive dispose patterns and using statements

### Risk: Browser Compatibility
**Mitigation**: Test CORS with all major browsers

## Open Questions for Product

1. **File Size Limits**: Should we enforce max file sizes? Current implementation has no limits.

2. **Retry Policy**: How many automatic retries for failed generations? How long to wait between retries?

3. **Progress UI**: Should progress be shown in-page or as a notification?

4. **Storage Costs**: Large files increase storage costs. Should we implement automatic cleanup?

5. **Optimization Trade-offs**: Is processing time for optimization worth the storage savings?

## Implementation Notes

### Memory Efficiency Patterns
```csharp
// Good: Streaming
await using var sourceStream = await httpClient.GetStreamAsync(url);
await s3Client.PutObjectAsync(new PutObjectRequest 
{
    InputStream = sourceStream,
    // ...
});

// Bad: Buffering
var bytes = await httpClient.GetByteArrayAsync(url);
await s3Client.PutObjectAsync(new PutObjectRequest 
{
    InputStream = new MemoryStream(bytes),
    // ...
});
```

### Base64 Streaming Pattern
```csharp
// Stream base64 decoding
using var base64Stream = new CryptoStream(
    inputStream, 
    new FromBase64Transform(), 
    CryptoStreamMode.Read);
```

### Progress Tracking Pattern
```csharp
public async Task<MediaStorageResult> StoreAsync(
    Stream content, 
    MediaMetadata metadata,
    IProgress<long>? progress = null)
{
    var progressStream = new ProgressStream(content, progress);
    // Use progressStream for upload
}
```

## Definition of Done

- [ ] All unit tests pass
- [ ] Integration tests verify streaming behavior
- [ ] Memory profiler shows constant memory usage
- [ ] Documentation updated with new parameters
- [ ] Performance benchmarks documented
- [ ] Feature flags configured for gradual rollout
- [ ] Monitoring alerts configured for memory usage
- [ ] CORS verification tool created
- [ ] Migration guide for existing deployments