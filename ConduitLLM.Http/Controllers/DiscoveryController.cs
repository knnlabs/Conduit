using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for discovering model capabilities and provider features.
    /// </summary>
    [ApiController]
    [Route("v1/discovery")]
    [Authorize]
    public class DiscoveryController : ControllerBase
    {
        private readonly IProviderDiscoveryService _discoveryService;
        private readonly ILogger<DiscoveryController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscoveryController"/> class.
        /// </summary>
        public DiscoveryController(
            IProviderDiscoveryService discoveryService,
            ILogger<DiscoveryController> logger)
        {
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all discovered models and their capabilities.
        /// </summary>
        /// <returns>Dictionary of models with their capabilities.</returns>
        [HttpGet("models")]
        public async Task<IActionResult> GetModels()
        {
            try
            {
                var models = await _discoveryService.DiscoverModelsAsync();
                
                var response = new
                {
                    data = models.Select(m => new
                    {
                        id = m.Key,
                        provider = m.Value.Provider,
                        display_name = m.Value.DisplayName,
                        capabilities = new
                        {
                            chat = m.Value.Capabilities.Chat,
                            chat_stream = m.Value.Capabilities.ChatStream,
                            embeddings = m.Value.Capabilities.Embeddings,
                            image_generation = m.Value.Capabilities.ImageGeneration,
                            vision = m.Value.Capabilities.Vision,
                            video_generation = m.Value.Capabilities.VideoGeneration,
                            video_understanding = m.Value.Capabilities.VideoUnderstanding,
                            function_calling = m.Value.Capabilities.FunctionCalling,
                            tool_use = m.Value.Capabilities.ToolUse,
                            json_mode = m.Value.Capabilities.JsonMode,
                            max_tokens = m.Value.Capabilities.MaxTokens,
                            max_output_tokens = m.Value.Capabilities.MaxOutputTokens,
                            supported_image_sizes = m.Value.Capabilities.SupportedImageSizes,
                            supported_video_resolutions = m.Value.Capabilities.SupportedVideoResolutions,
                            max_video_duration_seconds = m.Value.Capabilities.MaxVideoDurationSeconds
                        },
                        metadata = m.Value.Metadata,
                        last_verified = m.Value.LastVerified
                    }).ToList(),
                    count = models.Count
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering models");
                return StatusCode(500, new { error = new { message = "An error occurred while discovering models", type = "server_error" } });
            }
        }

        /// <summary>
        /// Gets models for a specific provider.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <returns>Dictionary of provider models with their capabilities.</returns>
        [HttpGet("providers/{provider}/models")]
        public async Task<IActionResult> GetProviderModels(string provider)
        {
            try
            {
                var models = await _discoveryService.DiscoverProviderModelsAsync(provider);
                
                var response = new
                {
                    provider = provider,
                    data = models.Select(m => new
                    {
                        id = m.Key,
                        display_name = m.Value.DisplayName,
                        capabilities = new
                        {
                            chat = m.Value.Capabilities.Chat,
                            chat_stream = m.Value.Capabilities.ChatStream,
                            embeddings = m.Value.Capabilities.Embeddings,
                            image_generation = m.Value.Capabilities.ImageGeneration,
                            vision = m.Value.Capabilities.Vision,
                            video_generation = m.Value.Capabilities.VideoGeneration,
                            video_understanding = m.Value.Capabilities.VideoUnderstanding,
                            function_calling = m.Value.Capabilities.FunctionCalling,
                            tool_use = m.Value.Capabilities.ToolUse,
                            json_mode = m.Value.Capabilities.JsonMode,
                            max_tokens = m.Value.Capabilities.MaxTokens,
                            max_output_tokens = m.Value.Capabilities.MaxOutputTokens
                        },
                        metadata = m.Value.Metadata,
                        last_verified = m.Value.LastVerified
                    }).ToList(),
                    count = models.Count
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering models for provider {Provider}", provider);
                return StatusCode(500, new { error = new { message = $"An error occurred while discovering models for provider {provider}", type = "server_error" } });
            }
        }

        /// <summary>
        /// Tests if a model supports a specific capability.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <param name="capability">The capability to test.</param>
        /// <returns>True if the model supports the capability.</returns>
        [HttpGet("models/{model}/capabilities/{capability}")]
        public async Task<IActionResult> TestModelCapability(string model, string capability)
        {
            try
            {
                if (!Enum.TryParse<ModelCapability>(capability, true, out var capabilityEnum))
                {
                    return BadRequest(new { error = new { message = $"Invalid capability: {capability}", type = "invalid_request_error" } });
                }

                var supported = await _discoveryService.TestModelCapabilityAsync(model, capabilityEnum);
                
                return Ok(new
                {
                    model = model,
                    capability = capability,
                    supported = supported
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing capability {Capability} for model {Model}", capability, model);
                return StatusCode(500, new { error = new { message = "An error occurred while testing model capability", type = "server_error" } });
            }
        }

        /// <summary>
        /// Refreshes the capability cache for all providers.
        /// </summary>
        /// <returns>No content on success.</returns>
        [HttpPost("refresh")]
        [Authorize(Policy = "MasterKey")] // Require admin access
        public async Task<IActionResult> RefreshCapabilities()
        {
            try
            {
                await _discoveryService.RefreshCapabilitiesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing capabilities");
                return StatusCode(500, new { error = new { message = "An error occurred while refreshing capabilities", type = "server_error" } });
            }
        }
    }
}