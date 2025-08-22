using System.Diagnostics;

using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class VideoGenerationOrchestrator
    {
        /// <summary>
        /// Process the video response after generation completes.
        /// </summary>
        private async Task ProcessVideoResponseAsync(
            VideoGenerationRequested request,
            VideoGenerationResponse response,
            ModelInfo modelInfo,
            ConduitLLM.Configuration.Entities.VirtualKey virtualKeyInfo,
            Stopwatch stopwatch,
            string originalModelAlias,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get the task metadata to reconstruct the request
                var taskStatus = await _taskService.GetTaskStatusAsync(request.RequestId);
                if (taskStatus == null)
                {
                    throw new InvalidOperationException($"Task {request.RequestId} not found");
                }

                // Reconstruct the video generation request from metadata
                VideoGenerationRequest videoRequest;
                try
                {
                    // Serialize the metadata object to JSON string first, then deserialize to JsonElement
                    var metadataJsonString = System.Text.Json.JsonSerializer.Serialize(taskStatus.Metadata);
                    var metadataJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(metadataJsonString);
                    
                    // Handle wrapped format from legacy async task service
                    System.Text.Json.JsonElement workingMetadata;
                    if (metadataJson.TryGetProperty("originalMetadata", out var originalMetadataElement))
                    {
                        workingMetadata = originalMetadataElement;
                    }
                    else
                    {
                        workingMetadata = metadataJson;
                    }
                    
                    // Extract the Request from metadata
                    if (workingMetadata.TryGetProperty("Request", out var requestElement))
                    {
                        videoRequest = System.Text.Json.JsonSerializer.Deserialize<VideoGenerationRequest>(requestElement.GetRawText()) ??
                            throw new InvalidOperationException("Failed to deserialize request from metadata");
                    }
                    else
                    {
                        // Fallback to constructing from event parameters
                        videoRequest = new VideoGenerationRequest
                        {
                            Model = request.Model,
                            Prompt = request.Prompt,
                            Duration = request.Parameters?.Duration,
                            Size = request.Parameters?.Size,
                            Fps = request.Parameters?.Fps,
                            ResponseFormat = "url"
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract request from task metadata, using fallback");
                    // Fallback to constructing from event parameters
                    videoRequest = new VideoGenerationRequest
                    {
                        Model = request.Model,
                        Prompt = request.Prompt,
                        Duration = request.Parameters?.Duration ?? 6,
                        Size = request.Parameters?.Size ?? "1280x720",
                        Fps = request.Parameters?.Fps ?? 30,
                        ResponseFormat = "url"
                    };
                }

                // Store video in media storage
                var videoUrl = response.Data?.FirstOrDefault()?.Url;
                if (response.Data != null)
                {
                    foreach (var video in response.Data)
                    {
                        // Handle base64 data
                        if (!string.IsNullOrEmpty(video.B64Json))
                        {
                            // Use streaming to decode base64 without loading entire content into memory
                            using var base64Stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(video.B64Json));
                            using var decodedStream = new System.Security.Cryptography.CryptoStream(
                                base64Stream, 
                                new System.Security.Cryptography.FromBase64Transform(), 
                                System.Security.Cryptography.CryptoStreamMode.Read);
                            
                            var videoMediaMetadata = new VideoMediaMetadata
                            {
                                MediaType = MediaType.Video,
                                ContentType = video.Metadata?.MimeType ?? "video/mp4",
                                FileSizeBytes = 0, // Will be set by storage service
                                FileName = $"video_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4",
                                Width = video.Metadata?.Width ?? 1280,
                                Height = video.Metadata?.Height ?? 720,
                                Duration = video.Metadata?.Duration ?? videoRequest.Duration ?? 6,
                                FrameRate = video.Metadata?.Fps ?? 30,
                                Codec = video.Metadata?.Codec ?? "h264",
                                Bitrate = video.Metadata?.Bitrate,
                                GeneratedByModel = request.Model,
                                GenerationPrompt = request.Prompt,
                                Resolution = videoRequest.Size ?? "1280x720"
                            };
                            
                            // Create a progress callback
                            Action<long>? progressCallback = bytesProcessed =>
                            {
                                _logger.LogDebug("Video upload progress: {BytesProcessed} bytes", bytesProcessed);
                            };
                            
                            var storageResult = await _storageService.StoreVideoAsync(decodedStream, videoMediaMetadata, progressCallback);
                            video.Url = storageResult.Url;
                            video.B64Json = null; // Clear base64 data after storing
                            videoUrl = storageResult.Url;
                            
                            // Publish MediaGenerationCompleted event for lifecycle tracking
                            await _publishEndpoint.Publish(new MediaGenerationCompleted
                            {
                                MediaType = MediaType.Video,
                                VirtualKeyId = virtualKeyInfo.Id,
                                MediaUrl = storageResult.Url,
                                StorageKey = storageResult.StorageKey,
                                FileSizeBytes = videoMediaMetadata.FileSizeBytes,
                                ContentType = videoMediaMetadata.ContentType,
                                GeneratedByModel = request.Model,
                                GenerationPrompt = request.Prompt,
                                GeneratedAt = DateTime.UtcNow,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["width"] = videoMediaMetadata.Width,
                                    ["height"] = videoMediaMetadata.Height,
                                    ["duration"] = videoMediaMetadata.Duration,
                                    ["frameRate"] = videoMediaMetadata.FrameRate,
                                    ["resolution"] = videoMediaMetadata.Resolution
                                },
                                CorrelationId = request.CorrelationId
                            });
                        }
                        // Handle external URLs from any provider (Replicate, MiniMax, etc.)
                        else if (!string.IsNullOrEmpty(video.Url) && 
                                (video.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                 video.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogInformation("Downloading and storing video from external URL: {Url}", video.Url);
                            
                            try
                            {
                                // Stream the video directly from the external URL to storage
                                using var httpClient = _httpClientFactory.CreateClient("VideoDownload");
                                
                                // Set timeout based on provider - some providers generate videos faster than others
                                // Videos are typically larger than images, so we need longer timeouts
                                httpClient.Timeout = TimeSpan.FromMinutes(15); // Default to 15 minutes for video downloads
                                
                                // Use ResponseHeadersRead for streaming
                                using var videoResponse = await httpClient.GetAsync(
                                    video.Url, 
                                    HttpCompletionOption.ResponseHeadersRead,
                                    cancellationToken);
                                videoResponse.EnsureSuccessStatusCode();
                                
                                var contentLength = videoResponse.Content.Headers.ContentLength ?? 0;
                                using var videoStream = await videoResponse.Content.ReadAsStreamAsync();
                                
                                var videoMediaMetadata = new VideoMediaMetadata
                                {
                                    MediaType = MediaType.Video,
                                    ContentType = video.Metadata?.MimeType ?? videoResponse.Content.Headers.ContentType?.MediaType ?? "video/mp4",
                                    FileSizeBytes = contentLength,
                                    FileName = $"video_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4",
                                    Width = video.Metadata?.Width ?? 1280,
                                    Height = video.Metadata?.Height ?? 720,
                                    Duration = video.Metadata?.Duration ?? videoRequest.Duration ?? 6,
                                    FrameRate = video.Metadata?.Fps ?? 30,
                                    Codec = video.Metadata?.Codec ?? "h264",
                                    Bitrate = video.Metadata?.Bitrate,
                                    GeneratedByModel = request.Model,
                                    GenerationPrompt = request.Prompt,
                                    Resolution = videoRequest.Size ?? "1280x720"
                                };
                                
                                // Create progress callback that updates task status
                                Action<long>? progressCallback = async (bytesProcessed) =>
                                {
                                    try
                                    {
                                        var percentage = contentLength > 0 
                                            ? (int)((bytesProcessed * 100) / contentLength) 
                                            : -1;
                                        
                                        var progressMessage = contentLength > 0
                                            ? $"Uploading video: {bytesProcessed / 1024 / 1024}MB of {contentLength / 1024 / 1024}MB ({percentage}%)"
                                            : $"Uploading video: {bytesProcessed / 1024 / 1024}MB";
                                        
                                        await _taskService.UpdateTaskStatusAsync(
                                            request.RequestId, 
                                            TaskState.Processing,
                                            progress: percentage
                                        );
                                        
                                        _logger.LogDebug("Video upload progress: {BytesProcessed} bytes ({Percentage}%)", 
                                            bytesProcessed, percentage);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to update task progress");
                                    }
                                };
                                
                                var originalUrl = video.Url; // Save original URL for logging
                                var storageResult = await _storageService.StoreVideoAsync(videoStream, videoMediaMetadata, progressCallback);
                                video.Url = storageResult.Url;
                                videoUrl = storageResult.Url;
                                
                                _logger.LogInformation("Successfully downloaded video from {ProviderUrl} and stored in CDN: {CdnUrl} (Size: {SizeMB}MB)", 
                                    originalUrl, storageResult.Url, contentLength / 1024 / 1024);
                                
                                // Publish MediaGenerationCompleted event for lifecycle tracking
                                await _publishEndpoint.Publish(new MediaGenerationCompleted
                                {
                                    MediaType = MediaType.Video,
                                    VirtualKeyId = virtualKeyInfo.Id,
                                    MediaUrl = storageResult.Url,
                                    StorageKey = storageResult.StorageKey,
                                    FileSizeBytes = videoMediaMetadata.FileSizeBytes,
                                    ContentType = videoMediaMetadata.ContentType,
                                    GeneratedByModel = request.Model,
                                    GenerationPrompt = request.Prompt,
                                    GeneratedAt = DateTime.UtcNow,
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["width"] = videoMediaMetadata.Width,
                                        ["height"] = videoMediaMetadata.Height,
                                        ["duration"] = videoMediaMetadata.Duration,
                                        ["frameRate"] = videoMediaMetadata.FrameRate,
                                        ["resolution"] = videoMediaMetadata.Resolution
                                    },
                                    CorrelationId = request.CorrelationId
                                });
                            }
                            catch (TaskCanceledException)
                            {
                                _logger.LogInformation("Video download timed out or cancelled for URL: {Url}", video.Url);
                                throw; // Re-throw to handle cancellation properly
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.LogInformation("Video download cancelled for URL: {Url}", video.Url);
                                throw; // Re-throw to handle cancellation properly
                            }
                            catch (HttpRequestException ex)
                            {
                                _logger.LogError(ex, "HTTP error downloading video from URL: {Url}. Video will use provider URL directly.", video.Url);
                                // Keep the original URL if download fails - user can still access video from provider
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to download and store video from external URL: {Url}. Video will use provider URL directly.", video.Url);
                                // Keep the original URL if download fails - user can still access video from provider
                            }
                        }
                    }
                }

                // Calculate cost
                var cost = await CalculateVideoCostAsync(request, modelInfo);
                
                // Update spend
                await _virtualKeyService.UpdateSpendAsync(virtualKeyInfo.Id, cost);
                
                // Restore the original model alias in the response
                if (response.Model != null)
                {
                    response.Model = originalModelAlias;
                }

                // Update task status to completed
                var result = new 
                {
                    videoUrl = videoUrl,
                    usage = response.Usage,
                    model = response.Model ?? originalModelAlias,
                    created = response.Created,
                    duration = stopwatch.Elapsed.TotalSeconds
                };
                
                await _taskService.UpdateTaskStatusAsync(
                    request.RequestId, 
                    TaskState.Completed,
                    result: result);
                
                // Publish VideoGenerationCompleted event
                await _publishEndpoint.Publish(new VideoGenerationCompleted
                {
                    RequestId = request.RequestId,
                    VideoUrl = videoUrl ?? string.Empty,
                    CompletedAt = DateTime.UtcNow,
                    GenerationDuration = stopwatch.Elapsed,
                    CorrelationId = request.CorrelationId
                });
                
                // Send webhook notification if configured
                if (!string.IsNullOrEmpty(request.WebhookUrl))
                {
                    var webhookPayload = new VideoCompletionWebhookPayload
                    {
                        TaskId = request.RequestId,
                        Status = "completed",
                        VideoUrl = videoUrl,
                        GenerationDurationSeconds = stopwatch.Elapsed.TotalSeconds,
                        Model = request.Model,
                        Prompt = request.Prompt
                    };

                    // Publish webhook delivery event for scalable processing
                    await _publishEndpoint.Publish(new WebhookDeliveryRequested
                    {
                        TaskId = request.RequestId,
                        TaskType = "video",
                        WebhookUrl = request.WebhookUrl,
                        EventType = WebhookEventType.TaskCompleted,
                        PayloadJson = ConduitLLM.Core.Helpers.WebhookPayloadHelper.SerializePayload(webhookPayload),
                        Headers = request.WebhookHeaders,
                        CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()
                    });
                    
                    _logger.LogDebug("Published webhook delivery event for completed video task {RequestId}", request.RequestId);
                }
                
                // Cancel progress tracking since task is complete
                await _publishEndpoint.Publish(new VideoProgressTrackingCancelled
                {
                    RequestId = request.RequestId,
                    VirtualKeyId = request.VirtualKeyId,
                    Reason = "Video generation completed successfully",
                    CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()
                });
                
                _logger.LogInformation("Successfully completed video generation task {RequestId} in {Duration}ms",
                    request.RequestId, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process video response for task {RequestId}", request.RequestId);
                await HandleVideoGenerationFailureAsync(request, ex.Message, stopwatch);
            }
        }

        /// <summary>
        /// Handle video generation failure.
        /// </summary>
        private async Task HandleVideoGenerationFailureAsync(
            VideoGenerationRequested request,
            string errorMessage,
            Stopwatch stopwatch)
        {
            _logger.LogError("Video generation failed for task {RequestId}: {Error}", request.RequestId, errorMessage);
            
            // Get current task status to check retry count
            var taskStatus = await _taskService.GetTaskStatusAsync(request.RequestId);
            var retryCount = taskStatus?.RetryCount ?? 0;
            var maxRetries = _retryConfiguration.EnableRetries ? 
                (taskStatus?.MaxRetries ?? _retryConfiguration.MaxRetries) : 0;
            var isRetryable = _retryConfiguration.EnableRetries && 
                IsRetryableError(new Exception(errorMessage)) && retryCount < maxRetries;
            
            // Calculate next retry time with exponential backoff
            DateTime? nextRetryAt = null;
            if (isRetryable)
            {
                var delaySeconds = _retryConfiguration.CalculateRetryDelay(retryCount);
                nextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                
                // Update task status to pending for retry
                await _taskService.UpdateTaskStatusAsync(
                    request.RequestId, 
                    TaskState.Pending, 
                    error: $"Retry {retryCount + 1}/{maxRetries} scheduled: {errorMessage}");
                
                _logger.LogInformation("Scheduling retry {RetryCount}/{MaxRetries} for task {RequestId} at {NextRetryAt}", 
                    retryCount + 1, maxRetries, request.RequestId, nextRetryAt);
            }
            else
            {
                // Update task status to failed (no more retries)
                await _taskService.UpdateTaskStatusAsync(
                    request.RequestId, 
                    TaskState.Failed, 
                    error: errorMessage);
            }
            
            // Publish VideoGenerationFailed event with retry information
            await _publishEndpoint.Publish(new VideoGenerationFailed
            {
                RequestId = request.RequestId,
                Error = errorMessage,
                IsRetryable = isRetryable,
                RetryCount = retryCount,
                MaxRetries = maxRetries,
                NextRetryAt = nextRetryAt,
                FailedAt = DateTime.UtcNow,
                CorrelationId = request.CorrelationId
            });
            
            // Cancel progress tracking since task has failed
            await _publishEndpoint.Publish(new VideoProgressTrackingCancelled
            {
                RequestId = request.RequestId,
                VirtualKeyId = request.VirtualKeyId,
                Reason = $"Video generation failed: {errorMessage}",
                CorrelationId = request.CorrelationId
            });

            // Send webhook notification if configured
            if (!string.IsNullOrEmpty(request.WebhookUrl))
            {
                var webhookPayload = new VideoCompletionWebhookPayload
                {
                    TaskId = request.RequestId,
                    Status = isRetryable ? "retrying" : "failed",
                    Error = errorMessage,
                    Model = request.Model,
                    Prompt = request.Prompt
                };

                // Publish webhook delivery event for scalable processing
                await _publishEndpoint.Publish(new WebhookDeliveryRequested
                {
                    TaskId = request.RequestId,
                    TaskType = "video",
                    WebhookUrl = request.WebhookUrl,
                    EventType = WebhookEventType.TaskFailed,
                    PayloadJson = ConduitLLM.Core.Helpers.WebhookPayloadHelper.SerializePayload(webhookPayload),
                    Headers = request.WebhookHeaders,
                    CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()
                });
                
                _logger.LogDebug("Published webhook delivery event for failed video task {RequestId}", request.RequestId);
            }
        }

        /// <summary>
        /// Handle video generation cancellation.
        /// </summary>
        private async Task HandleVideoGenerationCancellationAsync(
            VideoGenerationRequested request,
            Stopwatch stopwatch)
        {
            _logger.LogInformation("Video generation cancelled for task {RequestId} after {ElapsedMs}ms", 
                request.RequestId, stopwatch.ElapsedMilliseconds);
            
            // Update task status to cancelled
            await _taskService.UpdateTaskStatusAsync(
                request.RequestId, 
                TaskState.Cancelled, 
                error: "Video generation was cancelled by user request");
            
            // Publish VideoGenerationCancelled event
            await _publishEndpoint.Publish(new VideoGenerationCancelled
            {
                RequestId = request.RequestId,
                CancelledAt = DateTime.UtcNow,
                CorrelationId = request.CorrelationId
            });
            
            // Cancel progress tracking since task is cancelled
            await _publishEndpoint.Publish(new VideoProgressTrackingCancelled
            {
                RequestId = request.RequestId,
                VirtualKeyId = request.VirtualKeyId,
                Reason = "Video generation cancelled by user",
                CorrelationId = request.CorrelationId
            });

            // Send webhook notification if configured
            if (!string.IsNullOrEmpty(request.WebhookUrl))
            {
                var webhookPayload = new VideoCompletionWebhookPayload
                {
                    TaskId = request.RequestId,
                    Status = "cancelled",
                    Error = "Video generation was cancelled by user request",
                    Model = request.Model,
                    Prompt = request.Prompt
                };

                // Publish webhook delivery event for scalable processing
                await _publishEndpoint.Publish(new WebhookDeliveryRequested
                {
                    TaskId = request.RequestId,
                    TaskType = "video",
                    WebhookUrl = request.WebhookUrl,
                    EventType = WebhookEventType.TaskCancelled,
                    PayloadJson = ConduitLLM.Core.Helpers.WebhookPayloadHelper.SerializePayload(webhookPayload),
                    Headers = request.WebhookHeaders,
                    CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()
                });
                
                _logger.LogDebug("Published webhook delivery event for cancelled video task {RequestId}", request.RequestId);
            }
        }
    }
}