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

                // Use discovery service to check if model supports image generation
                var modelName = request.Model ?? "dall-e-3";
                var supportsImageGen = await _discoveryService.TestModelCapabilityAsync(
                    modelName, 
                    ModelCapability.ImageGeneration);
                
                if (!supportsImageGen)
                {
                    return BadRequest(new { error = new { message = $"Model {modelName} does not support image generation", type = "invalid_request_error" } });
                }

                // Get provider mapping for the model
                var mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelName);
                if (mapping == null)
                {
                    return BadRequest(new { error = new { message = $"No provider mapping found for model {modelName}", type = "invalid_request_error" } });
                }

                // Create client for the model
                var client = _clientFactory.GetClient(modelName);
                
                // Update request with the provider's model ID
                request.Model = mapping.ProviderModelId;
                
                // Generate images
                var response = await client.CreateImageAsync(request);

                // Store generated images if they're base64
                if (request.ResponseFormat == "b64_json" || response.Data.Any(d => !string.IsNullOrEmpty(d.B64Json)))
                {
                    for (int i = 0; i < response.Data.Count; i++)
                    {
                        var imageData = response.Data[i];
                        if (!string.IsNullOrEmpty(imageData.B64Json))
                        {
                            // Convert base64 to bytes
                            var imageBytes = Convert.FromBase64String(imageData.B64Json);
                            
                            // Store in media storage
                            var metadata = new MediaMetadata
                            {
                                ContentType = "image/png", // Default to PNG for generated images
                                FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{i}.png",
                                MediaType = MediaType.Image,
                                CustomMetadata = new()
                                {
                                    ["prompt"] = request.Prompt,
                                    ["model"] = request.Model ?? "unknown",
                                    ["provider"] = mapping.ProviderName
                                }
                            };

                            if (request.User != null)
                            {
                                metadata.CreatedBy = request.User;
                            }

                            var storageResult = await _storageService.StoreAsync(
                                new MemoryStream(imageBytes), 
                                metadata);

                            // Update response with URL
                            imageData.Url = storageResult.Url;
                            
                            // Clear base64 data if user requested URL format
                            if (request.ResponseFormat == "url")
                            {
                                imageData.B64Json = null;
                            }
                        }
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