using ConduitLLM.Core.Models;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Images controller - Synchronous image generation
    /// </summary>
    public partial class ImagesController
    {
        /// <summary>
        /// Creates one or more images given a prompt.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <returns>Generated images.</returns>
        [HttpPost("generations")]
        public async Task<IActionResult> CreateImage([FromBody] ConduitLLM.Core.Models.ImageGenerationRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Prompt))
                {
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Prompt is required",
                            Type = "invalid_request_error",
                            Code = "missing_parameter",
                            Param = "prompt"
                        }
                    });
                }

                // Model parameter is required
                if (string.IsNullOrWhiteSpace(request.Model))
                {
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Model is required",
                            Type = "invalid_request_error",
                            Code = "missing_parameter",
                            Param = "model"
                        }
                    });
                }
                
                var modelName = request.Model;
                
                // First check model mappings for image generation capability
                var mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelName);
                bool supportsImageGen = false;
                
                if (mapping != null)
                {
                    // Check if the mapping indicates image generation support
                    supportsImageGen = mapping.SupportsImageGeneration;
                    
                    _logger.LogInformation("Model {Model} mapping found, supports image generation: {Supports}", 
                        modelName, supportsImageGen);
                    
                    // Store provider info for usage tracking
                    HttpContext.Items["ProviderId"] = mapping.ProviderId;
                    HttpContext.Items["ProviderType"] = mapping.Provider?.ProviderType;
                }
                else
                {
                    // Model must be mapped to be used
                    _logger.LogWarning("No mapping found for model {Model}. Model must be configured in model mappings.", modelName);
                    supportsImageGen = false;
                }
                
                if (!supportsImageGen)
                {
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = $"Model {modelName} does not support image generation",
                            Type = "invalid_request_error",
                            Code = "unsupported_model",
                            Param = "model"
                        }
                    });
                }

                // If we don't have a mapping, try to create a client anyway (for direct model names)
                if (mapping == null)
                {
                    _logger.LogWarning("No provider mapping found for model {Model}, attempting direct client creation", modelName);
                }

                // Create client for the model
                var client = _clientFactory.GetClient(modelName);
                
                // Update request with the provider's model ID if we have a mapping
                if (mapping != null)
                {
                    request.Model = mapping.ProviderModelId;
                }
                
                // Generate images
                var response = await client.CreateImageAsync(request);

                // Store generated images if they're base64 or external URLs
                for (int i = 0; i < response.Data.Count; i++)
                {
                    var imageData = response.Data[i];
                    Stream? imageStream = null;
                    string contentType = "image/png";
                    string extension = "png";
                    
                    _logger.LogInformation("Processing image {Index}: URL={Url}, HasB64={HasB64}", 
                        i, imageData.Url ?? "null", !string.IsNullOrEmpty(imageData.B64Json));
                    
                    try
                    {
                        if (!string.IsNullOrEmpty(imageData.B64Json))
                        {
                            // Decode base64 to binary
                            try
                            {
                                var imageBytes = Convert.FromBase64String(imageData.B64Json);
                                imageStream = new MemoryStream(imageBytes);
                                _logger.LogInformation("Decoded base64 image, size: {Size} bytes", imageBytes.Length);
                            }
                            catch (FormatException ex)
                            {
                                _logger.LogError(ex, "Failed to decode base64 image data");
                                continue;
                            }
                        }
                        else if (!string.IsNullOrEmpty(imageData.Url) && 
                                (imageData.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                 imageData.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Stream external image directly to storage without buffering
                            using var httpClient = _httpClientFactory.CreateClient("ImageDownload");
                            httpClient.Timeout = TimeSpan.FromSeconds(60); // Increased timeout for streaming
                            
                            try
                            {
                                // Use GetAsync with HttpCompletionOption.ResponseHeadersRead for streaming
                                using var imageResponse = await httpClient.GetAsync(imageData.Url, 
                                    System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                                
                                if (imageResponse.IsSuccessStatusCode)
                                {
                                    // Try to determine content type from response
                                    if (imageResponse.Content.Headers.ContentType != null)
                                    {
                                        contentType = imageResponse.Content.Headers.ContentType.MediaType ?? contentType;
                                        extension = contentType.Split('/').LastOrDefault() ?? "png";
                                        if (extension == "jpeg") extension = "jpg";
                                    }
                                    else if (imageData.Url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase) || 
                                             imageData.Url.Contains(".jpg", StringComparison.OrdinalIgnoreCase))
                                    {
                                        contentType = "image/jpeg";
                                        extension = "jpg";
                                    }
                                    
                                    // Copy the stream to memory to avoid disposal issues
                                    var responseStream = await imageResponse.Content.ReadAsStreamAsync();
                                    var memoryStream = new MemoryStream();
                                    await responseStream.CopyToAsync(memoryStream);
                                    memoryStream.Position = 0;
                                    imageStream = memoryStream;
                                    
                                    _logger.LogInformation("Downloaded image data: {Bytes} bytes", memoryStream.Length);
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to download image from {Url}: {StatusCode}", 
                                        imageData.Url, imageResponse.StatusCode);
                                    continue;
                                }
                            }
                            catch (TaskCanceledException ex)
                            {
                                _logger.LogWarning(ex, "Timeout downloading image from {Url}", imageData.Url);
                                continue;
                            }
                            catch (System.Net.Http.HttpRequestException ex)
                            {
                                _logger.LogWarning(ex, "HTTP error downloading image from {Url}", imageData.Url);
                                continue;
                            }
                        }
                        else if (!string.IsNullOrEmpty(imageData.Url))
                        {
                            // Log non-HTTP URLs that we're not downloading
                            _logger.LogWarning("Image URL is not an HTTP/HTTPS URL, will not download: {Url}", imageData.Url);
                        }
                        else
                        {
                            _logger.LogWarning("Image data has neither URL nor base64 content");
                        }
                        
                        if (imageStream != null)
                        {
                            // Store in media storage directly with streaming
                            var metadata = new MediaMetadata
                            {
                                ContentType = contentType,
                                FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{i}.{extension}",
                                MediaType = MediaType.Image,
                                CustomMetadata = new()
                                {
                                    ["prompt"] = request.Prompt,
                                    ["model"] = request.Model ?? "unknown",
                                    ["provider"] = mapping?.ProviderId.ToString() ?? "unknown",
                                    ["originalUrl"] = imageData.Url ?? ""
                                }
                            };

                            if (request.User != null)
                            {
                                metadata.CreatedBy = request.User;
                            }

                            // Create progress reporter for large image downloads
                            var progress = new Progress<long>(bytesProcessed =>
                            {
                                _logger.LogDebug("Image storage progress: {BytesProcessed} bytes processed", bytesProcessed);
                            });
                            
                            var storageResult = await _storageService.StoreAsync(imageStream, metadata, progress);

                            // Track media ownership for lifecycle management
                            try
                            {
                                // Get virtual key ID from HttpContext
                                var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                                if (!string.IsNullOrEmpty(virtualKeyIdClaim) && int.TryParse(virtualKeyIdClaim, out var virtualKeyId))
                                {
                                    var mediaMetadata = new Core.Interfaces.MediaLifecycleMetadata
                                    {
                                        ContentType = contentType,
                                        SizeBytes = storageResult.SizeBytes,
                                        Provider = mapping?.Provider?.ProviderType.ToString() ?? "unknown",
                                        Model = request.Model ?? "unknown",
                                        Prompt = request.Prompt,
                                        StorageUrl = storageResult.Url,
                                        PublicUrl = storageResult.Url
                                    };

                                    await _mediaLifecycleService.TrackMediaAsync(
                                        virtualKeyId,
                                        storageResult.StorageKey,
                                        "image",
                                        mediaMetadata);
                                    
                                    _logger.LogInformation("Tracked media {StorageKey} for virtual key {VirtualKeyId}", 
                                        storageResult.StorageKey, virtualKeyId);
                                }
                                else
                                {
                                    _logger.LogWarning("Could not determine virtual key ID for media tracking");
                                }
                            }
                            catch (Exception trackEx)
                            {
                                // Don't fail the request if tracking fails
                                _logger.LogError(trackEx, "Failed to track media ownership, but continuing with response");
                            }
                            
                            // Update response with our proxied URL
                            _logger.LogInformation("Setting image URL: {Url}", storageResult.Url);
                            imageData.Url = storageResult.Url;
                            
                            // Handle response format
                            if (request.ResponseFormat == "b64_json")
                            {
                                // Read from storage to convert to base64
                                var storedStream = await _storageService.GetStreamAsync(storageResult.StorageKey);
                                if (storedStream != null)
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        await storedStream.CopyToAsync(ms);
                                        imageData.B64Json = Convert.ToBase64String(ms.ToArray());
                                    }
                                    storedStream.Dispose();
                                }
                                imageData.Url = null; // Clear URL when returning base64
                            }
                            else if (request.ResponseFormat == "url")
                            {
                                // Clear any base64 data when URL format is requested
                                imageData.B64Json = null;
                            }
                        }
                    }
                    finally
                    {
                        imageStream?.Dispose();
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating images");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "An error occurred while generating images",
                        Type = "server_error",
                        Code = "internal_error"
                    }
                });
            }
        }
    }
}