using System;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Models;
using ConduitLLM.Http.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles model listing requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1")]
    [Authorize(Policy = "VirtualKeyAuthentication")]
    [Tags("Models")]
    public class ModelsController : ControllerBase
    {
        private readonly ILLMRouter _router;
        private readonly ILogger<ModelsController> _logger;
        private readonly IModelMetadataService _metadataService;

        public ModelsController(
            ILLMRouter router,
            ILogger<ModelsController> logger,
            IModelMetadataService metadataService)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }

        /// <summary>
        /// Lists available models.
        /// </summary>
        /// <returns>A list of available models.</returns>
        [HttpGet("models")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status500InternalServerError)]
        public IActionResult ListModels()
        {
            try
            {
                _logger.LogInformation("Getting available models");

                // Get model names from the router
                var modelNames = _router.GetAvailableModels();

                // Convert to OpenAI format
                var basicModelData = modelNames.Select(m => new
                {
                    id = m,
                    @object = "model"
                }).ToList();

                // Create the response envelope
                var response = new
                {
                    data = basicModelData,
                    @object = "list"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving models list");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "server_error",
                        Code = "internal_error"
                    }
                });
            }
        }

        /// <summary>
        /// Gets metadata for a specific model.
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        /// <returns>Model metadata including capabilities and constraints.</returns>
        [HttpGet("models/{modelId}/metadata")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelMetadata(string modelId)
        {
            try
            {
                _logger.LogInformation("Getting metadata for model {ModelId}", modelId);

                var metadata = await _metadataService.GetModelMetadataAsync(modelId);
                
                if (metadata == null)
                {
                    return NotFound(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = $"No metadata found for model '{modelId}'",
                            Type = "invalid_request_error",
                            Code = "model_not_found"
                        }
                    });
                }

                var response = new
                {
                    modelId = modelId,
                    metadata = metadata
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metadata for model {ModelId}", modelId);
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "server_error",
                        Code = "internal_error"
                    }
                });
            }
        }
    }
}