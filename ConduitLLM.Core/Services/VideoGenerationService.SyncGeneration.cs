using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Partial class containing synchronous video generation functionality.
    /// </summary>
    public partial class VideoGenerationService
    {
        /// <inheritdoc/>
        public async Task<VideoGenerationResponse> GenerateVideoAsync(
            VideoGenerationRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting synchronous video generation for model {Model}", request.Model);

            // Validate the request
            if (!await ValidateRequestAsync(request, cancellationToken))
            {
                throw new ArgumentException("Invalid video generation request");
            }

            // Validate virtual key
            var virtualKeyInfo = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey, request.Model);
            if (virtualKeyInfo == null || !virtualKeyInfo.IsEnabled)
            {
                throw new UnauthorizedAccessException("Invalid or disabled virtual key");
            }

            // Get model mapping to resolve alias to provider model
            var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
            if (modelMapping == null)
            {
                throw new NotSupportedException($"Model {request.Model} is not configured. Please add it to model mappings.");
            }

            // Check if the model supports video generation
            var supportsVideo = await _capabilityService.SupportsVideoGenerationAsync(request.Model);
            if (!supportsVideo)
            {
                throw new NotSupportedException($"Model {request.Model} does not support video generation");
            }

            // Get the appropriate client for the model
            var client = _clientFactory.GetClient(request.Model);
            if (client == null)
            {
                throw new NotSupportedException($"No provider available for model {request.Model}");
            }

            // Store the original model alias for response
            var originalModelAlias = request.Model;
            
            // Update request to use the provider's model ID
            request.Model = modelMapping.ProviderModelId;

            // Publish VideoGenerationRequested event
            var requestId = Guid.NewGuid().ToString();
            await PublishEventAsync(
                new VideoGenerationRequested
                {
                    RequestId = requestId,
                    Model = request.Model,
                    Prompt = request.Prompt,
                    VirtualKeyId = virtualKeyInfo.Id.ToString(),
                    RequestedAt = DateTime.UtcNow,
                    CorrelationId = requestId
                },
                "video generation request",
                new { Model = request.Model, VirtualKeyId = virtualKeyInfo.Id });

            try
            {
                // Check if the client supports video generation using reflection
                // This avoids circular dependencies while allowing providers to implement video generation
                VideoGenerationResponse response;
                
                var clientType = client.GetType();
                _logger.LogInformation("Client type for model {Model}: {ClientType}", request.Model, clientType.FullName);
                
                // Check if this is a decorator and try to get the inner client
                object clientToCheck = client;
                if (clientType.FullName?.Contains("Decorator") == true || clientType.FullName?.Contains("PerformanceTracking") == true)
                {
                    // Try to get the inner client via reflection
                    var innerClientField = clientType.GetField("_innerClient", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (innerClientField != null)
                    {
                        var innerClient = innerClientField.GetValue(client);
                        if (innerClient != null)
                        {
                            clientToCheck = innerClient;
                            clientType = innerClient.GetType();
                            _logger.LogInformation("Unwrapped decorator to inner client type: {InnerClientType}", clientType.FullName);
                        }
                    }
                }
                
                // Try to find the method with both nullable and non-nullable string parameter
                var createVideoMethod = clientType.GetMethod("CreateVideoAsync", 
                    new[] { typeof(VideoGenerationRequest), typeof(string), typeof(CancellationToken) })
                    ?? clientType.GetMethod("CreateVideoAsync", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null,
                        new[] { typeof(VideoGenerationRequest), typeof(string), typeof(CancellationToken) },
                        null);
                
                // If not found, try finding any method with the name and check parameters manually
                if (createVideoMethod == null)
                {
                    var methods = clientType.GetMethods()
                        .Where(m => m.Name == "CreateVideoAsync" && m.GetParameters().Length == 3)
                        .ToArray();
                    
                    _logger.LogInformation("Found {Count} CreateVideoAsync methods with 3 parameters", methods.Length);
                    foreach (var method in methods)
                    {
                        var parameters = method.GetParameters();
                        _logger.LogInformation("Method: {Method}, Parameters: {P1} {P2} {P3}", 
                            method.Name,
                            parameters[0].ParameterType.Name,
                            parameters[1].ParameterType.Name,
                            parameters[2].ParameterType.Name);
                    }
                    
                    if (methods.Length > 0)
                    {
                        createVideoMethod = methods[0];
                    }
                }
                
                if (createVideoMethod != null)
                {
                    // The client is already configured with the correct API key
                    var task = createVideoMethod.Invoke(clientToCheck, new object?[] { request, null, cancellationToken }) as Task<VideoGenerationResponse>;
                    if (task != null)
                    {
                        response = await task;
                    }
                    else
                    {
                        throw new InvalidOperationException($"CreateVideoAsync method on {clientType.Name} did not return expected Task<VideoGenerationResponse>");
                    }
                }
                else
                {
                    _logger.LogError("CreateVideoAsync method not found on client type {ClientType} for model {Model}", 
                        clientType.FullName, request.Model);
                    
                    // Log all public methods to help debug
                    var allMethods = clientType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        .Where(m => m.Name.Contains("Video"))
                        .Select(m => m.Name)
                        .Distinct()
                        .ToList();
                    
                    _logger.LogError("Available video-related methods on {ClientType}: {Methods}", 
                        clientType.FullName, string.Join(", ", allMethods));
                    
                    throw new NotSupportedException($"Provider for model {request.Model} does not support video generation");
                }
                
                // Store video in media storage
                if (response.Data != null)
                {
                    foreach (var video in response.Data)
                    {
                        if (!string.IsNullOrEmpty(video.B64Json))
                        {
                            // Use streaming to decode base64 without loading entire content into memory
                            using var base64Stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(video.B64Json));
                            using var decodedStream = new System.Security.Cryptography.CryptoStream(
                                base64Stream, 
                                new System.Security.Cryptography.FromBase64Transform(), 
                                System.Security.Cryptography.CryptoStreamMode.Read);
                            
                            // Create video metadata for storage
                            var videoMediaMetadata = new VideoMediaMetadata
                            {
                                MediaType = MediaType.Video,
                                ContentType = video.Metadata?.MimeType ?? "video/mp4",
                                FileSizeBytes = 0, // Will be set by storage service
                                FileName = $"video_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4",
                                Width = video.Metadata?.Width ?? 1280,
                                Height = video.Metadata?.Height ?? 720,
                                Duration = video.Metadata?.Duration ?? request.Duration ?? 6,
                                FrameRate = video.Metadata?.Fps ?? 30,
                                Codec = video.Metadata?.Codec ?? "h264",
                                Bitrate = video.Metadata?.Bitrate,
                                GeneratedByModel = request.Model,
                                GenerationPrompt = request.Prompt,
                                Resolution = request.Size ?? "1280x720"
                            };
                            
                            var storageResult = await _mediaStorage.StoreVideoAsync(decodedStream, videoMediaMetadata);
                            video.Url = storageResult.Url;
                            video.B64Json = null; // Clear base64 data after storing
                            
                            // Publish MediaGenerationCompleted event for lifecycle tracking
                            await PublishEventAsync(new MediaGenerationCompleted
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
                                CorrelationId = requestId
                            }, "media generation completed", new { MediaType = "Video", Model = request.Model });
                        }
                    }
                }

                // Update spend
                var cost = await EstimateCostAsync(request, cancellationToken);
                await _virtualKeyService.UpdateSpendAsync(virtualKeyInfo.Id, cost);

                // Publish VideoGenerationCompleted event
                await PublishEventAsync(new VideoGenerationCompleted
                {
                    RequestId = requestId,
                    VideoUrl = response.Data?.FirstOrDefault()?.Url ?? string.Empty,
                    CompletedAt = DateTime.UtcNow,
                    CorrelationId = requestId
                }, "video generation completed", new { Model = originalModelAlias });

                // Restore the original model alias in the response
                if (response.Model != null)
                {
                    response.Model = originalModelAlias;
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Video generation failed for model {Model}", request.Model);

                // Publish VideoGenerationFailed event
                await PublishEventAsync(
                    new VideoGenerationFailed
                    {
                        RequestId = requestId,
                        Error = ex.Message,
                        FailedAt = DateTime.UtcNow,
                        CorrelationId = requestId
                    },
                    "video generation failure",
                    new { Model = request.Model, Error = ex.Message });

                throw;
            }
        }
    }
}