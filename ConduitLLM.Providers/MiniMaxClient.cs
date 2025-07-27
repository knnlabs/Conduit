using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.InternalModels;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with MiniMax AI APIs.
    /// </summary>
    public class MiniMaxClient : BaseLLMClient
    {
        private const string DefaultBaseUrl = "https://api.minimax.io";
        private readonly string _baseUrl;
        private Func<string, string, int, Task>? _progressCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="MiniMaxClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials containing API key and endpoint.</param>
        /// <param name="modelId">The default model ID to use.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="defaultModels">The default models configuration.</param>
        public MiniMaxClient(
            ProviderCredentials credentials,
            string modelId,
            ILogger<MiniMaxClient> logger,
            IHttpClientFactory httpClientFactory,
            ProviderDefaultModels? defaultModels = null)
            : base(credentials, modelId, logger, httpClientFactory, "minimax", defaultModels)
        {
            _baseUrl = string.IsNullOrWhiteSpace(credentials.BaseUrl) ? DefaultBaseUrl : credentials.BaseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Sets a callback for video generation progress updates.
        /// </summary>
        /// <param name="progressCallback">Callback that receives taskId, status, and progress percentage.</param>
        public void SetVideoProgressCallback(Func<string, string, int, Task>? progressCallback)
        {
            _progressCallback = progressCallback;
        }

        /// <inheritdoc/>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateChatCompletion");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var httpClient = CreateHttpClient(apiKey);
                
                var miniMaxRequest = new MiniMaxChatCompletionRequest
                {
                    Model = MapModelName(request.Model ?? ProviderModelId),
                    Messages = ConvertMessages(request.Messages),
                    Stream = false,
                    MaxTokens = request.MaxTokens,
                    Temperature = request.Temperature,
                    TopP = request.TopP,
                    Tools = ConvertTools(request.Tools),
                    ToolChoice = ConvertToolChoice(request.ToolChoice),
                    ReplyConstraints = request.ResponseFormat != null ? new ReplyConstraints
                    {
                        GuidanceType = request.ResponseFormat.Type == "json_object" ? "json_schema" : null,
                        JsonSchema = request.ResponseFormat.Type == "json_object" ? new { type = "object" } : null
                    } : null
                };

                var endpoint = $"{_baseUrl}/v1/chat/completions";
                // Log the request for debugging
                var requestJson = JsonSerializer.Serialize(miniMaxRequest);
                Logger.LogInformation("MiniMax request: {Request}", requestJson);

                // Make direct HTTP call to debug
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                
                var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
                var rawContent = await httpResponse.Content.ReadAsStringAsync();
                
                Logger.LogInformation("MiniMax HTTP Status: {Status}", httpResponse.StatusCode);
                Logger.LogInformation("MiniMax raw response: {Response}", rawContent);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new LLMCommunicationException($"MiniMax API returned {httpResponse.StatusCode}: {rawContent}");
                }
                
                // Now deserialize
                MiniMaxChatCompletionResponse response;
                try
                {
                    response = JsonSerializer.Deserialize<MiniMaxChatCompletionResponse>(rawContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    })!;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error deserializing MiniMax response: {Response}", rawContent);
                    throw new LLMCommunicationException("Failed to deserialize MiniMax response", ex);
                }

                // Log the raw response for debugging
                if (response == null)
                {
                    Logger.LogWarning("MiniMax response is null");
                    throw new LLMCommunicationException("MiniMax returned null response");
                }

                var responseJson = JsonSerializer.Serialize(response);
                Logger.LogInformation("MiniMax response: {Response}", responseJson);
                Logger.LogInformation("MiniMax response choices count: {Count}", response.Choices?.Count ?? 0);
                if (response.Choices != null && response.Choices.Count > 0)
                {
                    Logger.LogInformation("First choice message: {Message}", 
                        JsonSerializer.Serialize(response.Choices[0].Message));
                }

                // Check for MiniMax error response
                if (response.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                {
                    Logger.LogError("MiniMax error: {StatusCode} - {StatusMsg}", 
                        baseResp.StatusCode, baseResp.StatusMsg);
                    throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                }

                return ConvertToCoreResponse(response, request.Model ?? ProviderModelId);
            }, "CreateChatCompletion", cancellationToken);
        }

        /// <inheritdoc/>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");

            using var httpClient = CreateHttpClient(apiKey);
            
            var miniMaxRequest = new MiniMaxChatCompletionRequest
            {
                Model = MapModelName(request.Model ?? ProviderModelId),
                Messages = ConvertMessages(request.Messages),
                Stream = true,
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                TopP = request.TopP,
                Tools = ConvertTools(request.Tools),
                ToolChoice = ConvertToolChoice(request.ToolChoice),
                ReplyConstraints = request.ResponseFormat != null ? new ReplyConstraints
                {
                    GuidanceType = request.ResponseFormat.Type == "json_object" ? "json_schema" : null,
                    JsonSchema = request.ResponseFormat.Type == "json_object" ? new { type = "object" } : null
                } : null
            };

            var endpoint = $"{_baseUrl}/v1/chat/completions";
            
            var response = await Core.Utilities.HttpClientHelper.SendStreamingRequestAsync(
                httpClient, HttpMethod.Post, endpoint, miniMaxRequest, null, null, Logger, cancellationToken);

            Logger.LogInformation("MiniMax streaming response status: {StatusCode}", response.StatusCode);

            await foreach (var chunk in Core.Utilities.StreamHelper.ProcessSseStreamAsync<MiniMaxStreamChunk>(
                response, Logger, null, cancellationToken))
            {
                if (chunk != null)
                {
                    Logger.LogDebug("Received MiniMax chunk with ID: {Id}, Choices: {ChoiceCount}", 
                        chunk.Id, chunk.Choices?.Count ?? 0);
                    
                    // Check for MiniMax error response
                    if (chunk.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                    {
                        Logger.LogError("MiniMax streaming error: {StatusCode} - {StatusMsg}", 
                            baseResp.StatusCode, baseResp.StatusMsg);
                        throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                    }
                    
                    yield return ConvertToChunk(chunk, request.Model ?? ProviderModelId);
                }
                else
                {
                    Logger.LogDebug("Received null chunk from MiniMax stream");
                }
            }
            
            Logger.LogInformation("MiniMax streaming completed");
        }

        /// <inheritdoc/>
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var httpClient = CreateHttpClient(apiKey);
                
                var miniMaxRequest = new MiniMaxImageGenerationRequest
                {
                    Model = request.Model ?? "image-01",
                    Prompt = request.Prompt,
                    AspectRatio = MapSizeToAspectRatio(request.Size),
                    ResponseFormat = "url", // Always request URLs, we'll convert if needed
                    N = request.N,
                    PromptOptimizer = true
                };

                // Add subject reference if provided (for future use)
                if (!string.IsNullOrEmpty(request.User))
                {
                    // MiniMax uses this for tracking, not subject reference
                }

                var endpoint = $"{_baseUrl}/v1/image_generation";
                
                // Log the request for debugging
                var requestJson = JsonSerializer.Serialize(miniMaxRequest);
                Logger.LogInformation("MiniMax image request: {Request}", requestJson);
                
                // Make direct HTTP call to debug
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                
                var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
                var rawContent = await httpResponse.Content.ReadAsStringAsync();
                
                Logger.LogInformation("MiniMax HTTP Status: {Status}", httpResponse.StatusCode);
                Logger.LogInformation("MiniMax raw response: {Response}", rawContent);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new LLMCommunicationException($"MiniMax API returned {httpResponse.StatusCode}: {rawContent}");
                }
                
                // Now deserialize with specific options
                MiniMaxImageGenerationResponse response;
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    };
                    response = JsonSerializer.Deserialize<MiniMaxImageGenerationResponse>(rawContent, options)!;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error deserializing MiniMax response: {Response}", rawContent);
                    throw new LLMCommunicationException("Failed to deserialize MiniMax response", ex);
                }
                
                // Log the response for debugging
                var responseJson = JsonSerializer.Serialize(response);
                Logger.LogInformation("MiniMax image response object: {Response}", responseJson);
                
                // Check for MiniMax error response
                if (response.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                {
                    Logger.LogError("MiniMax image generation error: {StatusCode} - {StatusMsg}", 
                        baseResp.StatusCode, baseResp.StatusMsg);
                    throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                }

                // Map MiniMax response to Core response
                var imageData = new List<ImageData>();
                
                // Handle URL response format
                if (response.Data?.ImageUrls != null)
                {
                    foreach (var imageUrl in response.Data.ImageUrls)
                    {
                        // If user requested b64_json, download and convert the image
                        if (request.ResponseFormat == "b64_json")
                        {
                            try
                            {
                                Logger.LogInformation("Downloading image from URL for base64 conversion: {Url}", imageUrl);
                                using var imageResponse = await httpClient.GetAsync(imageUrl, cancellationToken);
                                if (imageResponse.IsSuccessStatusCode)
                                {
                                    var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                                    var base64String = Convert.ToBase64String(imageBytes);
                                    imageData.Add(new ImageData
                                    {
                                        Url = null,
                                        B64Json = base64String
                                    });
                                }
                                else
                                {
                                    Logger.LogWarning("Failed to download image from {Url}: {Status}", imageUrl, imageResponse.StatusCode);
                                    imageData.Add(new ImageData
                                    {
                                        Url = imageUrl,
                                        B64Json = null
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "Error downloading image from {Url}", imageUrl);
                                imageData.Add(new ImageData
                                {
                                    Url = imageUrl,
                                    B64Json = null
                                });
                            }
                        }
                        else
                        {
                            imageData.Add(new ImageData
                            {
                                Url = imageUrl,
                                B64Json = null
                            });
                        }
                    }
                }
                
                // Handle base64 response format
                if (response.Data?.Images != null)
                {
                    foreach (var image in response.Data.Images)
                    {
                        imageData.Add(new ImageData
                        {
                            Url = null,
                            B64Json = image.B64
                        });
                    }
                }
                
                // Handle MiniMax base64 format (image_base64 field)
                if (response.Data?.ImageBase64 != null)
                {
                    foreach (var base64Image in response.Data.ImageBase64)
                    {
                        imageData.Add(new ImageData
                        {
                            Url = null,
                            B64Json = base64Image
                        });
                    }
                }

                return new ImageGenerationResponse
                {
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Data = imageData
                };
            }, "CreateImage", cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("MiniMax provider does not support embeddings.");
        }

        /// <summary>
        /// Creates a video based on the provided request.
        /// </summary>
        /// <param name="request">The video generation request containing the prompt and generation parameters.</param>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The video generation response containing URLs or base64-encoded videos.</returns>
        public async Task<VideoGenerationResponse> CreateVideoAsync(
            VideoGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateVideo");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var httpClient = CreateVideoHttpClient(apiKey);
                
                var miniMaxRequest = new MiniMaxVideoGenerationRequest
                {
                    Model = request.Model ?? "video-01",
                    Prompt = request.Prompt,
                    VideoLength = request.Duration ?? 6, // Default to 6 seconds
                    Resolution = MapSizeToResolution(request.Size)
                };

                var endpoint = $"{_baseUrl}/v1/video_generation";
                
                // Log the request for debugging
                var requestJson = JsonSerializer.Serialize(miniMaxRequest);
                Logger.LogInformation("MiniMax video request: {Request}", requestJson);
                
                // Submit the video generation request
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                
                var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
                var rawContent = await httpResponse.Content.ReadAsStringAsync();
                
                Logger.LogInformation("MiniMax HTTP Status: {Status}", httpResponse.StatusCode);
                Logger.LogInformation("MiniMax raw response: {Response}", rawContent);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new LLMCommunicationException($"MiniMax API returned {httpResponse.StatusCode}: {rawContent}");
                }
                
                // Deserialize initial response
                MiniMaxVideoGenerationResponse response;
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    };
                    response = JsonSerializer.Deserialize<MiniMaxVideoGenerationResponse>(rawContent, options)!;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error deserializing MiniMax video response: {Response}", rawContent);
                    throw new LLMCommunicationException("Failed to deserialize MiniMax video response", ex);
                }
                
                // Check for MiniMax error response
                if (response.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                {
                    Logger.LogError("MiniMax video generation error: {StatusCode} - {StatusMsg}", 
                        baseResp.StatusCode, baseResp.StatusMsg);
                    throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                }

                // If we have a task ID, we need to poll for the result
                if (!string.IsNullOrEmpty(response.TaskId))
                {
                    Logger.LogInformation("MiniMax video generation task created: {TaskId}", response.TaskId);
                    
                    // Get polling timeout configuration from environment or use default
                    var pollingTimeoutMinutes = 10; // Default 10 minutes
                    var envTimeout = Environment.GetEnvironmentVariable("CONDUITLLM__TIMEOUTS__VIDEO_POLLING__SECONDS");
                    if (!string.IsNullOrEmpty(envTimeout) && int.TryParse(envTimeout, out var timeoutSeconds))
                    {
                        pollingTimeoutMinutes = timeoutSeconds / 60;
                    }
                    
                    const int basePollingIntervalMs = 2000; // Start with 2 seconds
                    const int maxPollingIntervalMs = 30000; // Max 30 seconds
                    var maxPollingAttempts = (pollingTimeoutMinutes * 60 * 1000) / basePollingIntervalMs;
                    
                    Logger.LogInformation("Configured video polling timeout: {TimeoutMinutes} minutes, max attempts: {MaxAttempts}", 
                        pollingTimeoutMinutes, maxPollingAttempts);
                    
                    var pollingIntervalMs = basePollingIntervalMs;
                    var consecutiveErrors = 0;
                    const int maxConsecutiveErrors = 3;
                    
                    for (int attempt = 0; attempt < maxPollingAttempts; attempt++)
                    {
                        // Wait before polling (skip first attempt)
                        if (attempt > 0)
                        {
                            await Task.Delay(pollingIntervalMs, cancellationToken);
                            
                            // Implement exponential backoff with jitter
                            pollingIntervalMs = Math.Min(
                                (int)(pollingIntervalMs * 1.5 + Random.Shared.Next(500)), 
                                maxPollingIntervalMs);
                        }
                        
                        // Check status with retry on transient errors
                        var statusEndpoint = $"{_baseUrl}/v1/query/video_generation?task_id={response.TaskId}";
                        var statusRequest = new HttpRequestMessage(HttpMethod.Get, statusEndpoint);
                        
                        HttpResponseMessage statusResponse;
                        string statusContent;
                        
                        try
                        {
                            statusResponse = await httpClient.SendAsync(statusRequest, cancellationToken);
                            statusContent = await statusResponse.Content.ReadAsStringAsync();
                            
                            // Reset consecutive errors on success
                            consecutiveErrors = 0;
                        }
                        catch (HttpRequestException ex)
                        {
                            consecutiveErrors++;
                            Logger.LogWarning(ex, "Network error checking video status (attempt {Attempt}, consecutive errors: {ConsecutiveErrors})", 
                                attempt + 1, consecutiveErrors);
                            
                            if (consecutiveErrors >= maxConsecutiveErrors)
                            {
                                throw new LLMCommunicationException($"Failed to check video status after {maxConsecutiveErrors} consecutive errors", ex);
                            }
                            
                            continue;
                        }
                        catch (TaskCanceledException ex)
                        {
                            Logger.LogWarning(ex, "Timeout checking video status (attempt {Attempt})", attempt + 1);
                            throw new LLMCommunicationException("Video status check timed out", ex);
                        }
                        
                        Logger.LogInformation("MiniMax video status check {Attempt}: {Status}", 
                            attempt + 1, statusContent);
                        
                        if (!statusResponse.IsSuccessStatusCode)
                        {
                            // Handle specific error codes
                            if (statusResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                Logger.LogWarning("Rate limited while checking video status, backing off");
                                pollingIntervalMs = maxPollingIntervalMs; // Max out the interval
                                continue;
                            }
                            else if ((int)statusResponse.StatusCode >= 500)
                            {
                                // Server errors - retry with backoff
                                consecutiveErrors++;
                                Logger.LogWarning("Server error checking video status: {StatusCode} - {Response}", 
                                    statusResponse.StatusCode, statusContent);
                                
                                if (consecutiveErrors >= maxConsecutiveErrors)
                                {
                                    throw new LLMCommunicationException($"Server error persisted after {maxConsecutiveErrors} attempts: {statusResponse.StatusCode}");
                                }
                                continue;
                            }
                            else
                            {
                                // Client errors - don't retry
                                throw new LLMCommunicationException($"Client error checking video status: {statusResponse.StatusCode} - {statusContent}");
                            }
                        }
                        
                        // Parse status response
                        MiniMaxVideoStatusResponse statusResult;
                        try
                        {
                            statusResult = JsonSerializer.Deserialize<MiniMaxVideoStatusResponse>(statusContent, 
                                new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                                })!;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error deserializing status response: {Response}", statusContent);
                            continue;
                        }
                        
                        // Check for MiniMax error response in status
                        if (statusResult.BaseResp is { } statusBaseResp && statusBaseResp.StatusCode != 0)
                        {
                            // Handle specific MiniMax error codes
                            var errorMsg = $"MiniMax error {statusBaseResp.StatusCode}: {statusBaseResp.StatusMsg}";
                            
                            switch (statusBaseResp.StatusCode)
                            {
                                case 1002: // Invalid API key
                                case 1004: // Authentication failed
                                    throw new UnauthorizedAccessException(errorMsg);
                                    
                                case 1008: // Quota exceeded
                                case 1013: // Rate limit exceeded
                                    Logger.LogWarning("MiniMax quota/rate limit error: {Error}", errorMsg);
                                    // Continue polling as the task might still complete
                                    pollingIntervalMs = maxPollingIntervalMs;
                                    continue;
                                    
                                case 2013: // Content policy violation
                                    throw new LLMCommunicationException($"Content policy violation: {statusBaseResp.StatusMsg}");
                                    
                                default:
                                    if (statusBaseResp.StatusCode >= 2000)
                                    {
                                        // Application errors - task has failed
                                        throw new LLMCommunicationException(errorMsg);
                                    }
                                    else
                                    {
                                        // System errors - might be transient
                                        consecutiveErrors++;
                                        Logger.LogWarning("MiniMax system error: {Error}", errorMsg);
                                        
                                        if (consecutiveErrors >= maxConsecutiveErrors)
                                        {
                                            throw new LLMCommunicationException($"MiniMax error persisted: {errorMsg}");
                                        }
                                        continue;
                                    }
                            }
                        }
                        
                        // Check if completed
                        if (statusResult.Status == "Success" && !string.IsNullOrEmpty(statusResult.FileId))
                        {
                            Logger.LogInformation("MiniMax video generation completed: FileId={FileId}", statusResult.FileId);
                            
                            // For MiniMax, we need to fetch the video file using the file_id
                            // The video URL is constructed from the file_id
                            var videoUrl = $"https://api.minimax.io/v1/files/retrieve?file_id={statusResult.FileId}";
                            
                            // Convert to standard response format
                            var videoData = new List<VideoData>
                            {
                                new VideoData
                                {
                                    Url = videoUrl,
                                    Metadata = new VideoMetadata
                                    {
                                        Width = statusResult.VideoWidth,
                                        Height = statusResult.VideoHeight,
                                        Duration = request.Duration ?? 6,
                                        Fps = request.Fps ?? 30,
                                        Format = "mp4",
                                        MimeType = "video/mp4"
                                    }
                                }
                            };
                            
                            // Handle response format conversion if needed
                            if (request.ResponseFormat == "b64_json")
                            {
                                try
                                {
                                    Logger.LogInformation("Downloading video for base64 conversion: {Url}", statusResult.Video?.Url);
                                    using var videoResponse = await httpClient.GetAsync(statusResult.Video?.Url ?? string.Empty, cancellationToken);
                                    if (videoResponse.IsSuccessStatusCode)
                                    {
                                        var videoBytes = await videoResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                                        videoData[0].B64Json = Convert.ToBase64String(videoBytes);
                                        videoData[0].Url = null;
                                        if (videoData[0].Metadata != null)
                                        {
                                            videoData[0].Metadata!.FileSizeBytes = videoBytes.Length;
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning("Failed to download video from {Url}: {Status}", 
                                            statusResult.Video?.Url, videoResponse.StatusCode);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError(ex, "Error downloading video from {Url}", statusResult.Video?.Url);
                                }
                            }
                            
                            var videoDuration = statusResult.Video?.Duration ?? request.Duration ?? 6;
                            var estimatedCost = EstimateVideoGenerationCost((int)videoDuration, request.Size ?? "1280x720");
                            
                            return new VideoGenerationResponse
                            {
                                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                Data = videoData,
                                Model = request.Model ?? "video-01",
                                Usage = new VideoGenerationUsage
                                {
                                    VideosGenerated = 1,
                                    TotalDurationSeconds = videoDuration,
                                    EstimatedCost = estimatedCost
                                }
                            };
                        }
                        else if (statusResult.Status == "Failed")
                        {
                            throw new LLMCommunicationException($"MiniMax video generation failed: {statusResult.BaseResp?.StatusMsg ?? "Unknown error"}");
                        }
                        else if (statusResult.Status == "Processing" || statusResult.Status == "Pending" || 
                                 statusResult.Status == "Preparing" || statusResult.Status == "Queueing")
                        {
                            Logger.LogDebug("MiniMax video generation still in progress: {Status}", statusResult.Status);
                            
                            // Report progress via callback if available
                            if (_progressCallback != null)
                            {
                                // Map status to progress percentage
                                var progressPercentage = statusResult.Status switch
                                {
                                    "Preparing" => 10,
                                    "Queueing" => 20,
                                    "Pending" => 30,
                                    "Processing" => CalculateProcessingProgress(attempt, maxPollingAttempts),
                                    _ => 0
                                };
                                
                                try
                                {
                                    await _progressCallback(response.TaskId, statusResult.Status, progressPercentage);
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogWarning(ex, "Error calling progress callback for task {TaskId}", response.TaskId);
                                }
                            }
                            
                            // Continue polling
                        }
                        else
                        {
                            Logger.LogWarning("Unknown MiniMax video status: {Status}", statusResult.Status);
                            // Continue polling for unknown statuses
                        }
                    }
                    
                    throw new LLMCommunicationException("MiniMax video generation timed out after 10 minutes");
                }
                
                // Should not reach here for video generation as it's always async
                throw new LLMCommunicationException("MiniMax video generation did not return a task ID");
            }, "CreateVideo", cancellationToken);
        }

        /// <summary>
        /// Calculates progress percentage for processing status based on polling attempts.
        /// </summary>
        private static int CalculateProcessingProgress(int currentAttempt, int maxAttempts)
        {
            // Processing starts at 30% and goes up to 90%
            const int minProgress = 30;
            const int maxProgress = 90;
            const int startSlowdownAttempt = 10; // Start slowing down after 10 attempts
            
            if (currentAttempt < startSlowdownAttempt)
            {
                // Linear progress for first attempts
                var linearProgress = (double)currentAttempt / startSlowdownAttempt;
                return minProgress + (int)((maxProgress - minProgress) * linearProgress * 0.7); // Use 70% of range
            }
            else
            {
                // Logarithmic progress for later attempts
                var remainingAttempts = currentAttempt - startSlowdownAttempt;
                var remainingMaxAttempts = Math.Max(1, maxAttempts - startSlowdownAttempt);
                var logProgress = Math.Log(remainingAttempts + 1) / Math.Log(remainingMaxAttempts + 1);
                var baseProgress = minProgress + (int)((maxProgress - minProgress) * 0.7);
                return baseProgress + (int)((maxProgress - baseProgress) * logProgress);
            }
        }

        /// <summary>
        /// Estimates the cost for a video generation request.
        /// MiniMax charges per second of video generation with potential resolution-based multipliers.
        /// </summary>
        /// <param name="duration">Duration in seconds</param>
        /// <param name="resolution">Video resolution</param>
        /// <returns>Estimated cost in USD</returns>
        public static decimal EstimateVideoGenerationCost(int duration, string resolution)
        {
            // Base costs per second for MiniMax video generation (example pricing)
            const decimal baseCostPerSecond = 0.15m; // $0.15 per second
            
            // Resolution multipliers
            var resolutionMultipliers = new Dictionary<string, decimal>
            {
                { "720x480", 0.8m },    // SD - 80% of base cost
                { "1280x720", 1.0m },   // HD - base cost
                { "1920x1080", 1.5m },  // Full HD - 150% of base cost
                { "720x1280", 1.0m },   // Portrait HD - base cost
                { "1080x1920", 1.5m }   // Portrait Full HD - 150% of base cost
            };
            
            var multiplier = resolutionMultipliers.GetValueOrDefault(resolution, 1.0m);
            return duration * baseCostPerSecond * multiplier;
        }

        /// <inheritdoc/>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // MiniMax doesn't provide a models endpoint, return static list
            return await Task.FromResult(new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("abab6.5-chat", "minimax", "abab6.5-chat"),
                ExtendedModelInfo.Create("abab6.5s-chat", "minimax", "abab6.5s-chat"),
                ExtendedModelInfo.Create("abab5.5-chat", "minimax", "abab5.5-chat"),
                ExtendedModelInfo.Create("image-01", "minimax", "image-01"),
                ExtendedModelInfo.Create("video-01", "minimax", "video-01")
            });
        }

        /// <inheritdoc/>
        protected override string GetDefaultBaseUrl()
        {
            return DefaultBaseUrl;
        }

        /// <inheritdoc/>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // MiniMax uses a different authentication header
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <summary>
        /// Creates a configured HttpClient specifically for video generation requests.
        /// This client has no timeout policy to support long-running video generation.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <returns>A configured HttpClient instance for video generation.</returns>
        protected virtual HttpClient CreateVideoHttpClient(string? apiKey = null)
        {
            HttpClient client;
            
            // Use the factory if available (for testing), otherwise create new client
            if (HttpClientFactory != null)
            {
                client = HttpClientFactory.CreateClient($"{ProviderName}VideoClient");
            }
            else
            {
                // For video generation, create a new HttpClient without using the factory
                // This ensures no timeout policies are applied by HttpClientFactory in production
                client = new HttpClient();
            }
            
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey!;
            if (string.IsNullOrWhiteSpace(effectiveApiKey))
            {
                throw new ConfigurationException($"API key is missing for provider '{ProviderName}'");
            }

            // Configure headers manually to avoid any base class behavior
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", effectiveApiKey);
            
            // Set a very long timeout for video generation (1 hour)
            client.Timeout = TimeSpan.FromHours(1);
            
            Logger.LogInformation("Created video HTTP client with 1-hour timeout and no Polly policies (bypassing factory: {BypassFactory})", HttpClientFactory == null);
            
            return client;
        }

        private static string MapSizeToAspectRatio(string? size)
        {
            return size switch
            {
                "1792x1024" => "16:9",
                "1024x1792" => "9:16",
                "1024x1024" => "1:1",
                "512x512" => "1:1",
                "2048x2048" => "1:1",
                _ => "1:1" // Default to square
            };
        }

        private static string MapSizeToResolution(string? size)
        {
            return size switch
            {
                "1920x1080" => "1080P",
                "1280x720" => "768P",  // MiniMax uses 768P for HD
                "720x480" => "768P",   // Map SD to 768P
                "720x1280" => "768P",  // Portrait HD
                "1080x1920" => "1080P", // Portrait Full HD
                _ => "768P" // Default to 768P (HD)
            };
        }

        private static int ParseResolutionWidth(string? size)
        {
            if (string.IsNullOrEmpty(size))
                return 1280;
            
            var parts = size.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out var width))
                return width;
                
            return 1280;
        }

        private static int ParseResolutionHeight(string? size)
        {
            if (string.IsNullOrEmpty(size))
                return 720;
            
            var parts = size.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[1], out var height))
                return height;
                
            return 720;
        }

        private string MapModelName(string modelName)
        {
            // Map user-friendly names to MiniMax model IDs
            return modelName switch
            {
                "minimax-chat" => "MiniMax-Text-01",
                "abab6.5-chat" => "MiniMax-Text-01",
                "abab6.5s-chat" => "MiniMax-Text-01",
                "abab5.5-chat" => "MiniMax-Text-01",
                "minimax-image" => "image-01",
                "minimax-video" => "video-01",
                "MiniMax-Text-01" => "MiniMax-Text-01", // Pass through if already mapped
                _ => modelName // Pass through if already a valid model ID
            };
        }

        private List<MiniMaxMessage> ConvertMessages(List<Message> messages)
        {
            var miniMaxMessages = new List<MiniMaxMessage>();
            
            foreach (var message in messages)
            {
                var miniMaxMessage = new MiniMaxMessage
                {
                    Role = message.Role,
                    Content = ConvertMessageContent(message.Content ?? string.Empty)
                };
                
                if (message.Role == "assistant" && message.ToolCalls != null && message.ToolCalls.Count > 0)
                {
                    // MiniMax uses function_call format, convert from tool_calls
                    var firstToolCall = message.ToolCalls[0];
                    if (firstToolCall.Function != null)
                    {
                        miniMaxMessage.FunctionCall = new MiniMaxFunctionCall
                        {
                            Name = firstToolCall.Function.Name,
                            Arguments = firstToolCall.Function.Arguments
                        };
                    }
                }
                
                miniMaxMessages.Add(miniMaxMessage);
            }
            
            return miniMaxMessages;
        }

        private object ConvertMessageContent(object content)
        {
            if (content is string stringContent)
            {
                return stringContent;
            }
            else if (content is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                var miniMaxParts = new List<object>();
                foreach (var element in jsonElement.EnumerateArray())
                {
                    if (element.TryGetProperty("type", out var typeElement))
                    {
                        var type = typeElement.GetString();
                        if (type == "text" && element.TryGetProperty("text", out var textElement))
                        {
                            miniMaxParts.Add(new { type = "text", text = textElement.GetString() });
                        }
                        else if (type == "image_url" && element.TryGetProperty("image_url", out var imageElement) &&
                                 imageElement.TryGetProperty("url", out var urlElement))
                        {
                            miniMaxParts.Add(new { type = "image_url", image_url = new { url = urlElement.GetString() } });
                        }
                    }
                }
                return miniMaxParts;
            }
            else if (content is List<object> contentParts)
            {
                // Handle if content is already a list of objects
                return contentParts;
            }
            
            return content;
        }

        private ChatCompletionResponse ConvertToCoreResponse(MiniMaxChatCompletionResponse miniMaxResponse, string modelId)
        {
            Logger.LogDebug("Converting MiniMax response: Id={Id}, ChoiceCount={ChoiceCount}, BaseResp={BaseResp}", 
                miniMaxResponse.Id, miniMaxResponse.Choices?.Count ?? 0, miniMaxResponse.BaseResp?.StatusCode ?? 0);
            
            var response = new ChatCompletionResponse
            {
                Id = miniMaxResponse.Id ?? Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = miniMaxResponse.Created ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>()
            };

            if (miniMaxResponse.Choices != null)
            {
                foreach (var choice in miniMaxResponse.Choices)
                {
                    Logger.LogDebug("MiniMax choice: Index={Index}, Role={Role}, Content={Content}, FinishReason={FinishReason}", 
                        choice.Index, choice.Message?.Role, choice.Message?.Content?.ToString()?.Substring(0, Math.Min(50, choice.Message?.Content?.ToString()?.Length ?? 0)), choice.FinishReason);
                    
                    response.Choices.Add(new Choice
                    {
                        Index = choice.Index,
                        Message = new Message
                        {
                            Role = choice.Message?.Role ?? "assistant",
                            Content = choice.Message?.Content ?? string.Empty,
                            ToolCalls = ConvertFunctionCallToToolCalls(choice.Message?.FunctionCall)
                        },
                        FinishReason = choice.FinishReason ?? "stop"
                    });
                }
            }

            if (miniMaxResponse.Usage != null)
            {
                response.Usage = new Usage
                {
                    PromptTokens = miniMaxResponse.Usage.PromptTokens,
                    CompletionTokens = miniMaxResponse.Usage.CompletionTokens,
                    TotalTokens = miniMaxResponse.Usage.TotalTokens
                };
            }

            return response;
        }

        private ChatCompletionChunk ConvertToChunk(MiniMaxStreamChunk miniMaxChunk, string modelId)
        {
            Logger.LogDebug("Converting MiniMax chunk: Id={Id}, ChoiceCount={ChoiceCount}", 
                miniMaxChunk.Id, miniMaxChunk.Choices?.Count ?? 0);
            
            var chunk = new ChatCompletionChunk
            {
                Id = miniMaxChunk.Id ?? Guid.NewGuid().ToString(),
                Object = "chat.completion.chunk",
                Created = miniMaxChunk.Created ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<StreamingChoice>()
            };

            if (miniMaxChunk.Choices != null)
            {
                foreach (var choice in miniMaxChunk.Choices)
                {
                    var content = choice.Delta?.Content;
                    var role = choice.Delta?.Role;
                    
                    Logger.LogDebug("MiniMax choice: Index={Index}, Content={Content}, Role={Role}, FinishReason={FinishReason}", 
                        choice.Index, content, role, choice.FinishReason);
                    
                    chunk.Choices.Add(new StreamingChoice
                    {
                        Index = choice.Index,
                        Delta = new DeltaContent
                        {
                            Role = role,
                            Content = content,
                            ToolCalls = ConvertDeltaFunctionCallToToolCalls(choice.Delta?.FunctionCall)
                        },
                        FinishReason = choice.FinishReason
                    });
                }
            }

            // Note: ChatCompletionChunk doesn't have Usage property in standard implementation
            // Usage is typically tracked separately or sent in final chunk

            return chunk;
        }

        private List<MiniMaxTool>? ConvertTools(List<Tool>? tools)
        {
            if (tools == null || tools.Count == 0)
                return null;

            var miniMaxTools = new List<MiniMaxTool>();
            foreach (var tool in tools)
            {
                if (tool.Type == "function" && tool.Function != null)
                {
                    miniMaxTools.Add(new MiniMaxTool
                    {
                        Type = "function",
                        Function = new MiniMaxFunctionDefinition
                        {
                            Name = tool.Function.Name,
                            Description = tool.Function.Description,
                            Parameters = tool.Function.Parameters
                        }
                    });
                }
            }
            return miniMaxTools.Count > 0 ? miniMaxTools : null;
        }

        private object? ConvertToolChoice(ToolChoice? toolChoice)
        {
            if (toolChoice == null)
                return null;

            // Get the serialized value from ToolChoice
            var serializedValue = toolChoice.GetSerializedValue();
            
            // If it's already a string (like "auto", "none"), return it directly
            if (serializedValue is string stringChoice)
            {
                return stringChoice;
            }
            
            // Otherwise, it's a function choice object, return it as-is
            // MiniMax expects the same format as OpenAI
            return serializedValue;
        }

        private List<ToolCall>? ConvertFunctionCallToToolCalls(MiniMaxFunctionCall? functionCall)
        {
            if (functionCall == null)
                return null;

            return new List<ToolCall>
            {
                new ToolCall
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = functionCall.Name,
                        Arguments = functionCall.Arguments
                    }
                }
            };
        }

        private List<ToolCallChunk>? ConvertDeltaFunctionCallToToolCalls(MiniMaxFunctionCall? functionCall)
        {
            if (functionCall == null)
                return null;

            return new List<ToolCallChunk>
            {
                new ToolCallChunk
                {
                    Index = 0,
                    Id = Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new FunctionCallChunk
                    {
                        Name = functionCall.Name,
                        Arguments = functionCall.Arguments
                    }
                }
            };
        }

        #region MiniMax-specific Models

        private class MiniMaxChatCompletionRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string Model { get; set; } = "abab6.5-chat";

            [System.Text.Json.Serialization.JsonPropertyName("messages")]
            public List<MiniMaxMessage> Messages { get; set; } = new();

            [System.Text.Json.Serialization.JsonPropertyName("stream")]
            public bool Stream { get; set; } = false;

            [System.Text.Json.Serialization.JsonPropertyName("max_tokens")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public int? MaxTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("temperature")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public double? Temperature { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("top_p")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public double? TopP { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("tools")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public List<MiniMaxTool>? Tools { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("tool_choice")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public object? ToolChoice { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("reply_constraints")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public ReplyConstraints? ReplyConstraints { get; set; }
        }

        private class MiniMaxMessage
        {
            [System.Text.Json.Serialization.JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("content")]
            public object Content { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("name")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public string? Name { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("audio_content")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public string? AudioContent { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("function_call")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public MiniMaxFunctionCall? FunctionCall { get; set; }
        }

        private class ReplyConstraints
        {
            [System.Text.Json.Serialization.JsonPropertyName("guidance_type")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public string? GuidanceType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("json_schema")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public object? JsonSchema { get; set; }
        }

        private class MiniMaxChatCompletionResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string? Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("created")]
            public long? Created { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string? Model { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("object")]
            public string? Object { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("choices")]
            public List<MiniMaxChoice>? Choices { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("usage")]
            public MiniMaxUsage? Usage { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }

            // MiniMax specific fields
            [System.Text.Json.Serialization.JsonPropertyName("input_sensitive")]
            public bool? InputSensitive { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive")]
            public bool? OutputSensitive { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("input_sensitive_type")]
            public int? InputSensitiveType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive_type")]
            public int? OutputSensitiveType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive_int")]
            public int? OutputSensitiveInt { get; set; }
        }

        private class MiniMaxChoice
        {
            [System.Text.Json.Serialization.JsonPropertyName("index")]
            public int Index { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("message")]
            public MiniMaxMessage? Message { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("finish_reason")]
            public string? FinishReason { get; set; }
        }

        private class MiniMaxStreamChunk
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string? Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("created")]
            public long? Created { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string? Model { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("object")]
            public string? Object { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("choices")]
            public List<MiniMaxStreamChoice>? Choices { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("usage")]
            public MiniMaxUsage? Usage { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }

            // MiniMax specific fields
            [System.Text.Json.Serialization.JsonPropertyName("input_sensitive")]
            public bool? InputSensitive { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive")]
            public bool? OutputSensitive { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("input_sensitive_type")]
            public int? InputSensitiveType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive_type")]
            public int? OutputSensitiveType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("output_sensitive_int")]
            public int? OutputSensitiveInt { get; set; }
        }

        private class MiniMaxStreamChoice
        {
            [System.Text.Json.Serialization.JsonPropertyName("index")]
            public int Index { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("delta")]
            public MiniMaxDelta? Delta { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("finish_reason")]
            public string? FinishReason { get; set; }
        }

        private class MiniMaxDelta
        {
            [System.Text.Json.Serialization.JsonPropertyName("role")]
            public string? Role { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("content")]
            public string? Content { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("function_call")]
            public MiniMaxFunctionCall? FunctionCall { get; set; }

            // MiniMax specific fields that appear in streaming responses
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string? Name { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("audio_content")]
            public object? AudioContent { get; set; }
        }

        private class MiniMaxUsage
        {
            [System.Text.Json.Serialization.JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("total_characters")]
            public int? TotalCharacters { get; set; }
        }

        private class MiniMaxImageGenerationRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string Model { get; set; } = "image-01";

            [System.Text.Json.Serialization.JsonPropertyName("prompt")]
            public string Prompt { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("aspect_ratio")]
            public string AspectRatio { get; set; } = "1:1";

            [System.Text.Json.Serialization.JsonPropertyName("response_format")]
            public string ResponseFormat { get; set; } = "url";

            [System.Text.Json.Serialization.JsonPropertyName("n")]
            public int N { get; set; } = 1;

            [System.Text.Json.Serialization.JsonPropertyName("prompt_optimizer")]
            public bool PromptOptimizer { get; set; } = true;

            [System.Text.Json.Serialization.JsonPropertyName("subject_reference")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public List<object>? SubjectReference { get; set; }
        }

        private class MiniMaxImageGenerationResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string? Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("data")]
            public MiniMaxImageResponseData? Data { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("metadata")]
            public object? Metadata { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }
        }

        private class MiniMaxImageResponseData
        {
            [System.Text.Json.Serialization.JsonPropertyName("image_urls")]
            public List<string>? ImageUrls { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("images")]
            public List<MiniMaxImageData>? Images { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("image_base64")]
            public List<string>? ImageBase64 { get; set; }
        }
        
        private class MiniMaxImageData
        {
            [System.Text.Json.Serialization.JsonPropertyName("b64")]
            public string? B64 { get; set; }
        }


        private class BaseResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("status_code")]
            public int StatusCode { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("status_msg")]
            public string? StatusMsg { get; set; }
        }

        private class MiniMaxTool
        {
            [System.Text.Json.Serialization.JsonPropertyName("type")]
            public string Type { get; set; } = "function";

            [System.Text.Json.Serialization.JsonPropertyName("function")]
            public MiniMaxFunctionDefinition? Function { get; set; }
        }

        private class MiniMaxFunctionDefinition
        {
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("description")]
            public string? Description { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("parameters")]
            public object? Parameters { get; set; }
        }

        private class MiniMaxFunctionCall
        {
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("arguments")]
            public string Arguments { get; set; } = string.Empty;
        }

        private class MiniMaxVideoGenerationRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string Model { get; set; } = "video-01";

            [System.Text.Json.Serialization.JsonPropertyName("prompt")]
            public string Prompt { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("video_length")]
            public int VideoLength { get; set; } = 6;

            [System.Text.Json.Serialization.JsonPropertyName("resolution")]
            public string Resolution { get; set; } = "1280x720";
        }

        private class MiniMaxVideoGenerationResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("task_id")]
            public string? TaskId { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("status")]
            public string? Status { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }
        }

        private class MiniMaxVideoStatusResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("task_id")]
            public string? TaskId { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("status")]
            public string? Status { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("file_id")]
            public string? FileId { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("video_width")]
            public int VideoWidth { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("video_height")]
            public int VideoHeight { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("video")]
            public MiniMaxVideoData? Video { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }
        }

        private class MiniMaxVideoData
        {
            [System.Text.Json.Serialization.JsonPropertyName("url")]
            public string? Url { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("duration")]
            public double? Duration { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("resolution")]
            public string? Resolution { get; set; }
        }

        #endregion
    }
}