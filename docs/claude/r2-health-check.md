# Cloudflare R2 Health Check Example

This document provides examples of implementing health checks for Cloudflare R2 storage.

## Basic R2 Health Check

Add this to your health check endpoint or monitoring service:

```csharp
public class R2HealthCheck : IHealthCheck
{
    private readonly IS3MediaStorageService _storageService;
    private readonly ILogger<R2HealthCheck> _logger;
    
    public R2HealthCheck(IS3MediaStorageService storageService, ILogger<R2HealthCheck> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple connectivity check - list up to 1 object
            var testKey = $"health-check/{Guid.NewGuid()}.txt";
            var testContent = Encoding.UTF8.GetBytes($"Health check at {DateTime.UtcNow:O}");
            
            // Test write
            await _storageService.StoreAsync(
                new MemoryStream(testContent),
                new MediaMetadata
                {
                    ContentType = "text/plain",
                    FileName = "health-check.txt",
                    MediaType = MediaType.Document
                },
                progress: null
            );
            
            // Test read
            var info = await _storageService.GetInfoAsync(testKey);
            if (info == null)
            {
                return HealthCheckResult.Unhealthy("Failed to retrieve test file from R2");
            }
            
            // Test delete
            await _storageService.DeleteAsync(testKey);
            
            return HealthCheckResult.Healthy("R2 storage is operational");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "R2 health check failed");
            return HealthCheckResult.Unhealthy($"R2 storage check failed: {ex.Message}");
        }
    }
}
```

## Registering the Health Check

In your `Program.cs` or `Startup.cs`:

```csharp
services.AddHealthChecks()
    .AddTypeActivatedCheck<R2HealthCheck>(
        "r2-storage",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "storage", "r2" });
```

## Lightweight R2 Connectivity Check

For a lighter-weight check that doesn't create/delete objects:

```csharp
public async Task<bool> CheckR2ConnectivityAsync()
{
    try
    {
        var s3Config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            ForcePathStyle = true
        };
        
        using var client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, s3Config);
        
        // Just check if we can list objects (won't fail if bucket is empty)
        var request = new ListObjectsV2Request
        {
            BucketName = _options.BucketName,
            MaxKeys = 1
        };
        
        var response = await client.ListObjectsV2Async(request);
        
        // Log R2-specific info if detected
        if (_options.IsR2)
        {
            _logger.LogInformation("R2 connectivity check successful. Bucket: {Bucket}", 
                _options.BucketName);
        }
        
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "R2 connectivity check failed");
        return false;
    }
}
```

## Monitoring R2 Performance

For monitoring R2-specific performance metrics:

```csharp
public class R2PerformanceMetrics
{
    public long UploadBytesPerSecond { get; set; }
    public long DownloadBytesPerSecond { get; set; }
    public double AverageUploadLatencyMs { get; set; }
    public double AverageDownloadLatencyMs { get; set; }
    public int MultipartChunkSizeMB { get; set; }
    public bool IsR2Detected { get; set; }
}

public async Task<R2PerformanceMetrics> GetR2MetricsAsync()
{
    // Implementation would track actual upload/download performance
    return new R2PerformanceMetrics
    {
        IsR2Detected = _options.IsR2,
        MultipartChunkSizeMB = (int)(_options.MultipartChunkSizeBytes / (1024 * 1024)),
        // ... other metrics from your monitoring system
    };
}
```

## Best Practices

1. **Frequency**: Run health checks every 30-60 seconds
2. **Timeout**: Set a reasonable timeout (e.g., 10 seconds) for R2 operations
3. **Caching**: Cache health check results for 5-10 seconds to avoid excessive API calls
4. **Alerting**: Set up alerts for repeated failures
5. **Metrics**: Track success rate, latency, and error types for R2 operations

## R2-Specific Considerations

- R2 may have different latency characteristics than S3
- Monitor for rate limiting (though R2 has generous limits)
- Track egress bandwidth usage (free with R2!)
- Consider R2's global distribution when monitoring from different regions