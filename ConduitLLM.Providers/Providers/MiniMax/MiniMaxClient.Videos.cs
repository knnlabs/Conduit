using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Exceptions;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.MiniMax
{
    /// <summary>
    /// MiniMaxClient partial class containing video generation functionality.
    /// </summary>
    public partial class MiniMaxClient
    {
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
            
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey!;
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
    }
}