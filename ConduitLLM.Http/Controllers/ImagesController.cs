using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles image generation requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1/images")]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IMediaStorageService _storageService;
        private readonly ILogger<ImagesController> _logger;
        private readonly IProviderDiscoveryService _discoveryService;
        private readonly IModelProviderMappingService _modelMappingService;

        public ImagesController(
            ILLMClientFactory clientFactory,
            IMediaStorageService storageService,
            ILogger<ImagesController> logger,
            IProviderDiscoveryService discoveryService,
            IModelProviderMappingService modelMappingService)
        {
            _clientFactory = clientFactory;
            _storageService = storageService;
            _logger = logger;
            _discoveryService = discoveryService;
            _modelMappingService = modelMappingService;
        }

        /// <summary>
        /// Creates one or more images given a prompt.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <returns>Generated images.</returns>
        [HttpPost("generations")]
        public async Task<IActionResult> CreateImage([FromBody] ImageGenerationRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Prompt))
                {
                    return BadRequest(new { error = new { message = "Prompt is required", type = "invalid_request_error" } });
                }

                var modelName = request.Model ?? "dall-e-3";
                
                // First check model mappings for image generation capability
                var mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelName);
                bool supportsImageGen = false;
                
                if (mapping != null)
                {
                    // Check if the mapping indicates image generation support
                    supportsImageGen = mapping.SupportsImageGeneration;
                    
                    _logger.LogInformation("Model {Model} mapping found, supports image generation: {Supports}", 
                        modelName, supportsImageGen);
                }
                else
                {
                    // Fall back to discovery service if no mapping exists
                    _logger.LogInformation("No mapping found for {Model}, using discovery service", modelName);
                    supportsImageGen = await _discoveryService.TestModelCapabilityAsync(
                        modelName, 
                        ModelCapability.ImageGeneration);
                }
                
                if (!supportsImageGen)
                {
                    return BadRequest(new { error = new { message = $"Model {modelName} does not support image generation", type = "invalid_request_error" } });
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
                    
                    try
                    {
                        if (!string.IsNullOrEmpty(imageData.B64Json))
                        {
                            // Convert base64 to bytes
                            var imageBytes = Convert.FromBase64String(imageData.B64Json);
                            imageStream = new MemoryStream(imageBytes);
                        }
                        else if (!string.IsNullOrEmpty(imageData.Url) && 
                                (imageData.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                 imageData.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Download external image to proxy it through our storage
                            using var httpClient = new System.Net.Http.HttpClient();
                            httpClient.Timeout = TimeSpan.FromSeconds(30);
                            
                            var imageResponse = await httpClient.GetAsync(imageData.Url);
                            if (imageResponse.IsSuccessStatusCode)
                            {
                                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                                imageStream = new MemoryStream(imageBytes);
                                
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
                            }
                            else
                            {
                                _logger.LogWarning("Failed to download image from {Url}: {StatusCode}", 
                                    imageData.Url, imageResponse.StatusCode);
                                continue;
                            }
                        }
                        
                        if (imageStream != null)
                        {
                            // Read the image bytes first if we need base64
                            byte[]? imageBytes = null;
                            if (request.ResponseFormat == "b64_json")
                            {
                                // Read bytes for base64 conversion
                                using (var ms = new MemoryStream())
                                {
                                    await imageStream.CopyToAsync(ms);
                                    imageBytes = ms.ToArray();
                                }
                                // Create new stream for storage
                                imageStream = new MemoryStream(imageBytes);
                            }
                            
                            // Store in media storage
                            var metadata = new MediaMetadata
                            {
                                ContentType = contentType,
                                FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{i}.{extension}",
                                MediaType = MediaType.Image,
                                CustomMetadata = new()
                                {
                                    ["prompt"] = request.Prompt,
                                    ["model"] = request.Model ?? "unknown",
                                    ["provider"] = mapping?.ProviderName ?? "unknown",
                                    ["originalUrl"] = imageData.Url ?? ""
                                }
                            };

                            if (request.User != null)
                            {
                                metadata.CreatedBy = request.User;
                            }

                            var storageResult = await _storageService.StoreAsync(imageStream, metadata);

                            // Update response with our proxied URL
                            imageData.Url = storageResult.Url;
                            
                            // Handle response format
                            if (request.ResponseFormat == "b64_json" && imageBytes != null)
                            {
                                // Use the bytes we already read
                                imageData.B64Json = Convert.ToBase64String(imageBytes);
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
                return StatusCode(500, new { error = new { message = "An error occurred while generating images", type = "server_error" } });
            }
        }

    }
}