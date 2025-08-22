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
        /// Performs actual video generation using the appropriate provider.
        /// </summary>
        private async Task GenerateVideoAsync(
            VideoGenerationRequested request,
            ModelInfo modelInfo,
            ConduitLLM.Configuration.Entities.VirtualKey virtualKeyInfo,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get the task metadata to reconstruct the request
                var taskStatus = await _taskService.GetTaskStatusAsync(request.RequestId);
                if (taskStatus == null)
                {
                    throw new InvalidOperationException($"Task {request.RequestId} not found");
                }

                // Declare originalModelAlias for proper scope
                string originalModelAlias = request.Model;

                // Extract the request from metadata
                VideoGenerationRequest? videoRequest;
                try
                {
                    if (taskStatus.Metadata is TaskMetadata taskMetadata)
                    {
                        // We already have the virtualKeyInfo from the database lookup
                        // No need to extract the virtual key string from metadata
                        _logger.LogDebug("Using VirtualKeyId {VirtualKeyId} from database for video generation", virtualKeyInfo.Id);
                        
                        // Extract request from extension data
                        videoRequest = null;
                        if (taskMetadata.ExtensionData != null)
                        {
                            // First try direct Request property
                            if (taskMetadata.ExtensionData.TryGetValue("Request", out var requestObj))
                            {
                                if (requestObj is VideoGenerationRequest req)
                                {
                                    videoRequest = req;
                                }
                                else if (requestObj is System.Text.Json.JsonElement jsonReq)
                                {
                                    videoRequest = System.Text.Json.JsonSerializer.Deserialize<VideoGenerationRequest>(jsonReq.GetRawText());
                                }
                            }
                            // Then try wrapped format with originalMetadata
                            else if (taskMetadata.ExtensionData.TryGetValue("originalMetadata", out var originalMetadataObj) &&
                                     originalMetadataObj is IDictionary<string, object> originalMetadata &&
                                     originalMetadata.TryGetValue("Request", out var wrappedRequestObj))
                            {
                                if (wrappedRequestObj is VideoGenerationRequest wrappedReq)
                                {
                                    videoRequest = wrappedReq;
                                }
                                else if (wrappedRequestObj is System.Text.Json.JsonElement wrappedJsonReq)
                                {
                                    videoRequest = System.Text.Json.JsonSerializer.Deserialize<VideoGenerationRequest>(wrappedJsonReq.GetRawText());
                                }
                            }
                        }
                        
                        // Fallback to constructing from event parameters
                        if (videoRequest == null)
                        {
                            videoRequest = new VideoGenerationRequest
                            {
                                Model = request.Model,
                                Prompt = request.Prompt,
                                Duration = request.Parameters?.Duration,
                                Size = request.Parameters?.Size,
                                Fps = request.Parameters?.Fps,
                                N = 1
                            };
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Invalid task metadata type: {taskStatus.Metadata?.GetType().FullName ?? "null"}");
                    }
                }
                catch (Exception ex) when (!(ex is InvalidOperationException))
                {
                    _logger.LogError(ex, "Failed to extract data from task metadata");
                    throw new InvalidOperationException("Invalid task metadata format", ex);
                }

                // Update the original model alias to the video request model
                originalModelAlias = videoRequest.Model;
                
                // Update request to use the provider's model ID (already retrieved in modelInfo)
                videoRequest.Model = modelInfo.ModelId;

                // Get the appropriate client for the model using the alias
                var client = _clientFactory.GetClient(originalModelAlias);
                if (client == null)
                {
                    throw new NotSupportedException($"No provider available for model {originalModelAlias}");
                }

                // Check if the client supports video generation using reflection
                VideoGenerationResponse response;
                var clientType = client.GetType();
                
                // Handle decorators by getting inner client
                object clientToCheck = client;
                if (clientType.FullName?.Contains("Decorator") == true || clientType.FullName?.Contains("PerformanceTracking") == true)
                {
                    var innerClientField = clientType.GetField("_innerClient", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (innerClientField != null)
                    {
                        var innerClient = innerClientField.GetValue(client);
                        if (innerClient != null)
                        {
                            clientToCheck = innerClient;
                            clientType = innerClient.GetType();
                        }
                    }
                }
                
                // Find CreateVideoAsync method
                var createVideoMethod = clientType.GetMethods()
                    .FirstOrDefault(m => m.Name == "CreateVideoAsync" && m.GetParameters().Length == 3);
                
                if (createVideoMethod != null)
                {
                    // Create a cancellation token source for this specific task
                    var taskCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    
                    // Register the task for cancellation
                    _taskRegistry.RegisterTask(request.RequestId, taskCts);
                    _logger.LogDebug("Registered task {RequestId} for cancellation", request.RequestId);
                    
                    try
                    {
                        // Start progress tracking via event-driven architecture
                        // NOTE: Disabled time-based tracking in favor of real progress from MiniMax
                        // await StartProgressTrackingAsync(request);

                        // Set up progress callback if the client is MiniMax
                        if (clientType.Name == "MiniMaxClient")
                        {
                            // Use reflection to set the progress callback
                            var setCallbackMethod = clientType.GetMethod("SetVideoProgressCallback");
                            if (setCallbackMethod != null)
                            {
                                Func<string, string, int, Task> progressCallback = async (taskId, status, progressPercentage) =>
                                {
                                    _logger.LogInformation("Video generation progress for {TaskId}: {Status} at {Progress}%", 
                                        taskId, status, progressPercentage);
                                    
                                    // Update task progress
                                    await _taskService.UpdateTaskStatusAsync(
                                        request.RequestId,
                                        TaskState.Processing,
                                        progress: progressPercentage);
                                    
                                    // Publish progress event
                                    await _publishEndpoint.Publish(new VideoGenerationProgress
                                    {
                                        RequestId = request.RequestId,
                                        ProgressPercentage = progressPercentage,
                                        Status = status,
                                        Message = $"Video generation {status.ToLowerInvariant()}",
                                        CorrelationId = request.CorrelationId
                                    });
                                };
                                
                                setCallbackMethod.Invoke(clientToCheck, new object[] { progressCallback });
                                _logger.LogDebug("Set video progress callback for MiniMax client");
                            }
                        }

                        _logger.LogInformation("Processing video generation for task {RequestId} with cancellation support", request.RequestId);
                        
                        // Invoke video generation with the virtual key and cancellation token
                        // The client is already configured with the correct API key from the factory
                        var task = createVideoMethod.Invoke(clientToCheck, new object?[] { videoRequest, null, taskCts.Token }) as Task<VideoGenerationResponse>;
                        if (task != null)
                        {
                            response = await task;
                            
                            // Process the response when it completes
                            await ProcessVideoResponseAsync(request, response, modelInfo, virtualKeyInfo, stopwatch, originalModelAlias, taskCts.Token);
                        }
                        else
                        {
                            throw new InvalidOperationException($"CreateVideoAsync method on {clientType.Name} did not return expected Task<VideoGenerationResponse>");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Video generation for task {RequestId} was cancelled", request.RequestId);
                        await HandleVideoGenerationCancellationAsync(request, stopwatch);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Video generation failed for task {RequestId}", request.RequestId);
                        await HandleVideoGenerationFailureAsync(request, ex.Message, stopwatch);
                    }
                    finally
                    {
                        // Unregister the task when complete
                        _taskRegistry.UnregisterTask(request.RequestId);
                    }
                    
                    _logger.LogInformation("Video generation task {RequestId} completed processing", request.RequestId);
                    return;
                }
                else
                {
                    throw new NotSupportedException($"Provider for model {request.Model} does not support video generation");
                }
            }
            catch (Exception ex)
            {
                await HandleVideoGenerationFailureAsync(request, ex.Message, stopwatch);
            }
        }

        /// <summary>
        /// Starts progress tracking for a video generation task using event-driven architecture.
        /// </summary>
        private async Task StartProgressTrackingAsync(VideoGenerationRequested request)
        {
            // Publish initial progress check request
            var progressCheck = new VideoProgressCheckRequested
            {
                RequestId = request.RequestId,
                VirtualKeyId = request.VirtualKeyId,
                ScheduledAt = DateTime.UtcNow.AddSeconds(5), // First check in 5 seconds
                IntervalIndex = 0,
                TotalIntervals = 5, // 10%, 30%, 50%, 70%, 90%
                StartTime = DateTime.UtcNow,
                CorrelationId = request.CorrelationId
            };
            
            await _publishEndpoint.Publish(progressCheck);
            _logger.LogDebug("Initiated progress tracking for video generation {RequestId}", request.RequestId);
        }

        /// <summary>
        /// Gets model information from mappings or discovery service.
        /// </summary>
        private async Task<ModelInfo?> GetModelInfoAsync(string modelAlias, string virtualKeyHash)
        {
            // First try to get from model mappings
            var mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelAlias);
            
            if (mapping != null)
            {
                return new ModelInfo
                {
                    ModelId = mapping.ProviderModelId,
                    Provider = mapping.ProviderId.ToString(),
                    ModelAlias = mapping.ModelAlias
                };
            }
            
            // No fallback - model must be in ModelProviderMapping
            return null;
        }

        /// <summary>
        /// Calculates the cost for video generation.
        /// </summary>
        private async Task<decimal> CalculateVideoCostAsync(VideoGenerationRequested request, ModelInfo modelInfo)
        {
            // Create usage object for video generation
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = request.Parameters?.Duration ?? 5,
                VideoResolution = request.Parameters?.Size ?? "1280x720"
            };

            // Use the cost calculation service
            return await _costService.CalculateCostAsync(modelInfo.ModelId, usage);
        }

        private decimal GetResolutionMultiplier(string? resolution)
        {
            return resolution switch
            {
                "1920x1080" or "1080x1920" => 2.0m,
                "1280x720" or "720x1280" => 1.5m,
                "720x480" or "480x720" => 1.2m,
                _ => 1.0m
            };
        }

        private bool IsRetryableError(Exception ex)
        {
            // Check the exception type
            var isRetryableType = ex switch
            {
                TimeoutException => true,
                HttpRequestException => true,
                TaskCanceledException => true,
                System.IO.IOException => true,
                System.Net.Sockets.SocketException => true,
                _ => false
            };

            // Check for specific error messages that indicate transient failures
            if (!isRetryableType && ex.Message != null)
            {
                var lowerMessage = ex.Message.ToLowerInvariant();
                isRetryableType = lowerMessage.Contains("timeout") ||
                                  lowerMessage.Contains("timed out") ||
                                  lowerMessage.Contains("connection") ||
                                  lowerMessage.Contains("network") ||
                                  lowerMessage.Contains("temporarily unavailable") ||
                                  lowerMessage.Contains("service unavailable") ||
                                  lowerMessage.Contains("too many requests") ||
                                  lowerMessage.Contains("rate limit");
            }

            return isRetryableType;
        }
    }
}