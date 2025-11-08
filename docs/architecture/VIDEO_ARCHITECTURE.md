# Video Support Architecture for Conduit

## Overview

This document outlines the architecture for adding comprehensive video support to Conduit, including video generation, video understanding (vision), and streaming capabilities.

## Core Components

### 1. Video Models and DTOs

#### Video Generation Models
```csharp
// Request for video generation
public class VideoGenerationRequest
{
    public string Prompt { get; set; }
    public string? Model { get; set; }
    public int? Duration { get; set; } // In seconds
    public string? Size { get; set; } // e.g., "1920x1080", "1280x720"
    public int? Fps { get; set; } // Frames per second
    public string? Style { get; set; }
    public string? ResponseFormat { get; set; } // "url" or "b64_json"
}

// Response from video generation
public class VideoGenerationResponse
{
    public long Created { get; set; }
    public List<VideoData> Data { get; set; }
}

public class VideoData
{
    public string? Url { get; set; }
    public string? B64Json { get; set; }
    public VideoMetadata? Metadata { get; set; }
}

public class VideoMetadata
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Duration { get; set; } // In seconds
    public int Fps { get; set; }
    public string? Codec { get; set; }
    public long FileSizeBytes { get; set; }
}
```

#### Video Understanding Models
```csharp
// Video content part for multimodal messages
public class VideoUrlContentPart
{
    public string Type => "video_url";
    public VideoUrl VideoUrl { get; set; }
}

public class VideoUrl
{
    public string Url { get; set; }
    public string? Detail { get; set; } // "low", "high", "auto"
    public int? MaxFrames { get; set; } // Max frames to analyze
    public double? SampleRate { get; set; } // Sample every N seconds
}
```

### 2. Video Storage and Streaming

#### IVideoStorageService
```csharp
public interface IVideoStorageService : IMediaStorageService
{
    // Additional video-specific methods
    Task<string> GenerateStreamingUrlAsync(string storageKey, StreamingOptions options);
    Task<VideoMetadata> GetVideoMetadataAsync(string storageKey);
    Task<string> CreateThumbnailAsync(string videoStorageKey, int timeSeconds);
}

public class StreamingOptions
{
    public string? Protocol { get; set; } // "hls", "dash", "mp4"
    public int? MaxBitrate { get; set; }
    public TimeSpan? UrlExpiration { get; set; }
}
```

### 3. Video Processing Pipeline

#### IVideoProcessor
```csharp
public interface IVideoProcessor
{
    Task<VideoMetadata> ExtractMetadataAsync(Stream videoStream);
    Task<Stream> TranscodeAsync(Stream videoStream, VideoTranscodeOptions options);
    Task<List<string>> ExtractFramesAsync(Stream videoStream, FrameExtractionOptions options);
    Task<Stream> GenerateThumbnailAsync(Stream videoStream, int timeSeconds);
}

public class VideoTranscodeOptions
{
    public string OutputFormat { get; set; }
    public string? Codec { get; set; }
    public int? Bitrate { get; set; }
    public string? Size { get; set; }
}

public class FrameExtractionOptions
{
    public double SampleRate { get; set; } // Extract frame every N seconds
    public int? MaxFrames { get; set; }
    public string OutputFormat { get; set; } // "jpeg", "png"
}
```

### 4. Provider Integration

#### Video-Capable Providers
- **MiniMax**: Video generation support
- **OpenAI**: GPT-4 Vision (future video support)
- **Google Gemini**: Video understanding
- **Anthropic Claude**: Video understanding (future)
- **Replicate**: Various video generation models

#### Provider Capabilities Extension
```csharp
public class ModelCapabilities
{
    // Existing...
    public bool VideoGeneration { get; set; }
    public bool VideoUnderstanding { get; set; }
    public VideoCapabilityDetails? VideoDetails { get; set; }
}

public class VideoCapabilityDetails
{
    public int? MaxDurationSeconds { get; set; }
    public List<string> SupportedSizes { get; set; }
    public List<int> SupportedFps { get; set; }
    public List<string> SupportedCodecs { get; set; }
    public long? MaxFileSizeBytes { get; set; }
}
```

### 5. HTTP Endpoints

#### Video Generation Endpoint
```
POST /v1/videos/generations
Authorization: Bearer {api_key}

{
  "prompt": "A serene ocean sunset with waves",
  "model": "minimax-video-01",
  "duration": 5,
  "size": "1280x720",
  "fps": 30
}
```

#### Video Upload Endpoint
```
POST /v1/videos/upload
Authorization: Bearer {api_key}
Content-Type: multipart/form-data

[Binary video data]
```

#### Video Streaming Endpoint
```
GET /v1/videos/{video_id}/stream
Authorization: Bearer {api_key}
Accept: application/vnd.apple.mpegurl (for HLS)
```

### 6. Async Task Integration

Video generation is a long-running operation that requires async task handling:

```csharp
public class VideoGenerationController : ControllerBase
{
    [HttpPost("generations")]
    public async Task<IActionResult> GenerateVideo(VideoGenerationRequest request)
    {
        // 1. Create async task
        var taskId = await _taskService.CreateTaskAsync("video_generation", request);
        
        // 2. Queue video generation job
        await _videoGenerationService.QueueGenerationAsync(taskId, request);
        
        // 3. Return task ID for polling
        return Accepted(new { task_id = taskId });
    }
}
```

### 7. WebUI Integration

#### Video Generation UI
- Similar to image generation page
- Progress tracking with async task polling
- Video preview player
- Download and share options

#### Chat Interface Video Support
- Video upload capability
- Video preview in chat messages
- Inline video player for responses

### 8. Video Utilities

#### Core Video Utilities
```csharp
public static class VideoUtils
{
    public static bool IsVideoMimeType(string mimeType);
    public static string GetVideoExtension(string mimeType);
    public static bool ValidateVideoFile(Stream stream, long maxSize);
    public static TimeSpan ParseDuration(string duration);
}
```

## Implementation Phases

### Phase 1: Core Infrastructure
1. ✅ Async task service (completed)
2. ✅ Media storage service (completed)
3. Video models and DTOs
4. Video utilities

### Phase 2: Video Generation
1. MiniMax video generation integration
2. Video generation HTTP endpoint
3. Async task handling for generation
4. Basic video storage

### Phase 3: Video Understanding
1. Video content parts for messages
2. Frame extraction utilities
3. Provider integrations (Gemini, GPT-4V)
4. Video upload endpoint

### Phase 4: Advanced Features
1. Video streaming (HLS/DASH)
2. Video transcoding
3. Thumbnail generation
4. WebUI video player

### Phase 5: Production Ready
1. CDN integration
2. Video compression
3. Bandwidth optimization
4. Analytics and monitoring

## Technical Considerations

### Storage Requirements
- Videos require significantly more storage than images
- Consider lifecycle policies (auto-delete after N days)
- Implement storage quotas per user/API key

### Performance
- Video processing is CPU/GPU intensive
- Use background jobs for transcoding
- Implement request queuing for generation
- Cache processed videos and thumbnails

### Security
- Validate video content before processing
- Implement virus scanning for uploads
- Rate limit video generation endpoints
- Watermark generated videos if needed

### Scalability
- Use external video processing services (e.g., AWS MediaConvert)
- Implement distributed task processing
- Use CDN for video delivery
- Consider edge caching for popular videos

## Configuration

```json
{
  "ConduitLLM": {
    "Video": {
      "MaxUploadSizeBytes": 104857600,  // 100MB
      "MaxGenerationDurationSeconds": 30,
      "SupportedFormats": ["mp4", "webm", "mov"],
      "StorageProvider": "S3",
      "ProcessingProvider": "Local",  // or "AWS", "Azure"
      "EnableStreaming": true,
      "EnableTranscoding": false
    }
  }
}
```

## API Compatibility

The video endpoints follow patterns similar to OpenAI's image generation API:
- `/v1/videos/generations` - Generate videos
- `/v1/videos/uploads` - Upload videos
- `/v1/videos/{id}` - Get video info
- `/v1/videos/{id}/stream` - Stream video

This ensures consistency and familiarity for developers already using the image generation features.