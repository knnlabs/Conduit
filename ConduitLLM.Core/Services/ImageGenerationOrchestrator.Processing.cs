using System.Diagnostics;

using ConduitLLM.Core.Events;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Image generation orchestrator - Processing functionality
    /// </summary>
    public partial class ImageGenerationOrchestrator
    {
        private async Task<ConduitLLM.Core.Events.ImageData> ProcessSingleImageAsync(
            ConduitLLM.Core.Models.ImageData imageData,
            int index,
            ImageGenerationRequested request,
            ModelInfo modelInfo,
            SemaphoreSlim semaphore,
            CancellationToken cancellationToken,
            Action onProgress,
            Action<long, long> onTimingUpdate)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                string? finalUrl = imageData.Url;
                
                if (!string.IsNullOrEmpty(imageData.B64Json))
                {
                    // Store base64 image using streaming to avoid loading entire content into memory
                    var metadata = new Dictionary<string, string>
                    {
                        ["prompt"] = request.Request.Prompt,
                        ["model"] = modelInfo.ModelId,
                        ["provider"] = modelInfo.ProviderName
                    };
                    
                    var mediaMetadata = new MediaMetadata
                    {
                        ContentType = "image/png",
                        FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{index}.png",
                        MediaType = MediaType.Image,
                        CustomMetadata = metadata
                    };
                    
                    // Use streaming to decode base64
                    using var base64Stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(imageData.B64Json));
                    using var decodedStream = new System.Security.Cryptography.CryptoStream(
                        base64Stream, 
                        new System.Security.Cryptography.FromBase64Transform(), 
                        System.Security.Cryptography.CryptoStreamMode.Read);
                    
                    var storageResult = await _storageService.StoreAsync(decodedStream, mediaMetadata);
                    finalUrl = storageResult.Url;
                    
                    // Publish MediaGenerationCompleted event for lifecycle tracking
                    await _publishEndpoint.Publish(new MediaGenerationCompleted
                    {
                        MediaType = MediaType.Image,
                        VirtualKeyId = request.VirtualKeyId,
                        MediaUrl = storageResult.Url,
                        StorageKey = storageResult.StorageKey,
                        FileSizeBytes = storageResult.SizeBytes,
                        ContentType = mediaMetadata.ContentType,
                        GeneratedByModel = modelInfo.ModelId,
                        GenerationPrompt = request.Request.Prompt,
                        GeneratedAt = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object>
                        {
                            ["provider"] = modelInfo.ProviderName,
                            ["model"] = modelInfo.ModelId,
                            ["index"] = index,
                            ["format"] = "b64_json"
                        },
                        CorrelationId = request.CorrelationId?.ToString() ?? string.Empty
                    });
                }
                else if (!string.IsNullOrEmpty(imageData.Url) && 
                        (imageData.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                         imageData.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    var (url, downloadMs, storageMs) = await DownloadAndStoreImageAsync(
                        imageData.Url,
                        index,
                        request,
                        modelInfo,
                        cancellationToken);
                    finalUrl = url;
                    onTimingUpdate(downloadMs, storageMs);
                }
                
                // Report progress
                onProgress();
                
                return new ConduitLLM.Core.Events.ImageData
                {
                    Url = finalUrl,
                    B64Json = request.Request.ResponseFormat == "b64_json" ? imageData.B64Json : null,
                    RevisedPrompt = null,
                    Metadata = new Dictionary<string, object>
                    {
                        ["provider"] = modelInfo.ProviderName,
                        ["model"] = modelInfo.ModelId,
                        ["index"] = index
                    }
                };
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<(string url, long downloadMs, long storageMs)> DownloadAndStoreImageAsync(
            string imageUrl,
            int index,
            ImageGenerationRequested request,
            ModelInfo modelInfo,
            CancellationToken cancellationToken)
        {
            var downloadStopwatch = Stopwatch.StartNew();
            var storageStopwatch = new Stopwatch();
            
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = GetProviderTimeout(modelInfo.ProviderType);
                
                // Use streaming for better memory efficiency
                using var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                downloadStopwatch.Stop();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to download image from {Url}: {StatusCode}", 
                        imageUrl, response.StatusCode);
                    return (imageUrl, downloadStopwatch.ElapsedMilliseconds, 0); // Return original URL as fallback
                }
                
                // Determine content type and extension
                var contentType = "image/png";
                var extension = "png";
                
                if (response.Content.Headers.ContentType != null)
                {
                    contentType = response.Content.Headers.ContentType.MediaType ?? contentType;
                    extension = contentType.Split('/').LastOrDefault() ?? "png";
                    if (extension == "jpeg") extension = "jpg";
                }
                else if (imageUrl.Contains(".jpeg", StringComparison.OrdinalIgnoreCase) || 
                         imageUrl.Contains(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "image/jpeg";
                    extension = "jpg";
                }
                
                var metadata = new Dictionary<string, string>
                {
                    ["prompt"] = request.Request.Prompt,
                    ["model"] = modelInfo.ModelId,
                    ["provider"] = modelInfo.ProviderName,
                    ["originalUrl"] = imageUrl
                };
                
                var mediaMetadata = new MediaMetadata
                {
                    ContentType = contentType,
                    FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{index}.{extension}",
                    MediaType = MediaType.Image,
                    CustomMetadata = metadata
                };
                
                // Add CreatedBy if we have virtual key info
                if (request.VirtualKeyId > 0)
                {
                    mediaMetadata.CreatedBy = request.VirtualKeyId.ToString();
                }
                
                // Stream directly to storage
                using var imageStream = await response.Content.ReadAsStreamAsync();
                storageStopwatch.Start();
                var storageResult = await _storageService.StoreAsync(imageStream, mediaMetadata);
                storageStopwatch.Stop();
                
                _logger.LogInformation("Downloaded and stored image from {OriginalUrl} to {StorageUrl} (Download: {DownloadMs}ms, Storage: {StorageMs}ms)", 
                    imageUrl, storageResult.Url, downloadStopwatch.ElapsedMilliseconds, storageStopwatch.ElapsedMilliseconds);
                
                // Get file size for the event
                var contentLength = response.Content.Headers.ContentLength ?? 0;
                
                // Publish MediaGenerationCompleted event for lifecycle tracking
                await _publishEndpoint.Publish(new MediaGenerationCompleted
                {
                    MediaType = MediaType.Image,
                    VirtualKeyId = request.VirtualKeyId,
                    MediaUrl = storageResult.Url,
                    StorageKey = storageResult.StorageKey,
                    FileSizeBytes = contentLength,
                    ContentType = mediaMetadata.ContentType,
                    GeneratedByModel = modelInfo.ModelId,
                    GenerationPrompt = request.Request.Prompt,
                    GeneratedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["provider"] = modelInfo.ProviderName,
                        ["model"] = modelInfo.ModelId,
                        ["index"] = index
                    },
                    CorrelationId = request.CorrelationId
                });
                
                return (storageResult.Url, downloadStopwatch.ElapsedMilliseconds, storageStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download and store image from URL: {Url}", imageUrl);
                return (imageUrl, downloadStopwatch.ElapsedMilliseconds, storageStopwatch.ElapsedMilliseconds); // Return original URL as fallback
            }
        }
    }
}