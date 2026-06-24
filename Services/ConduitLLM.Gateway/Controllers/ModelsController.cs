using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Filters;
using ConduitLLM.Gateway.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// Handles model listing requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1")]
    [Authorize(Policy = "VirtualKeyAuthentication")]
    [Tags("Models")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class ModelsController : GatewayControllerBase
    {
        private readonly IModelMetadataService _metadataService;
        private readonly ConduitLLM.Configuration.Interfaces.IModelProviderMappingRepository _modelMappingRepository;

        public ModelsController(
            ILogger<ModelsController> logger,
            IModelMetadataService metadataService,
            ConduitLLM.Configuration.Interfaces.IModelProviderMappingRepository modelMappingRepository)
            : base(logger)
        {
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            _modelMappingRepository = modelMappingRepository ?? throw new ArgumentNullException(nameof(modelMappingRepository));
        }

        /// <summary>
        /// Lists available models.
        /// </summary>
        /// <returns>A list of available models in OpenAI-compatible format.</returns>
        /// <remarks>
        /// This endpoint maintains OpenAI API compatibility and returns all models without pagination.
        /// For large deployments with many models, use the Admin API's paginated endpoints.
        /// </remarks>
        [HttpGet("models")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListModels(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Getting available models");

            // Get model mappings using paginated repository method
            // Use max page size; most deployments have <100 model mappings
            var allMappings = new List<Configuration.Entities.ModelProviderMapping>();
            var pageNumber = 1;
            const int pageSize = 100;

            // Fetch all pages to maintain OpenAI API compatibility (no pagination in response)
            while (true)
            {
                var (mappings, totalCount) = await _modelMappingRepository.GetPaginatedAsync(pageNumber, pageSize, cancellationToken);
                allMappings.AddRange(mappings);

                if (allMappings.Count >= totalCount || mappings.Count == 0)
                    break;

                pageNumber++;
            }

            // Convert to OpenAI format using model aliases
            var basicModelData = allMappings
                .Select(m => m.ModelAlias)
                .Distinct()
                .Select(alias => new
                {
                    id = alias,
                    @object = "model"
                }).ToList();

            Logger.LogDebug("Returning {ModelCount} available models", basicModelData.Count);

            // Create the response envelope
            var response = new
            {
                data = basicModelData,
                @object = "list"
            };

            return Ok(response);
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
            Logger.LogInformation("Getting metadata for model {ModelId}", modelId);

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
    }
}
