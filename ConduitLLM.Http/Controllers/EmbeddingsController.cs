using System;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Adapters;
using ConduitLLM.Http.Models;

using MassTransit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles embedding generation requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1")]
    [Authorize(Policy = "RequireVirtualKey")]
    [Tags("Embeddings")]
    public class EmbeddingsController : EventPublishingControllerBase
    {
        private readonly ILLMRouter _router;
        private readonly ILogger<EmbeddingsController> _logger;
        private readonly ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService _modelMappingService;

        public EmbeddingsController(
            ILLMRouter router,
            ILogger<EmbeddingsController> logger,
            ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService modelMappingService,
            IPublishEndpoint publishEndpoint) : base(publishEndpoint, logger)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
        }

        /// <summary>
        /// Creates embeddings for the given input.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An embedding response.</returns>
        [HttpPost("embeddings")]
        [ProducesResponseType(typeof(EmbeddingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateEmbedding(
            [FromBody] EmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Invalid request body.",
                        Type = "invalid_request_error",
                        Code = "invalid_request"
                    }
                });
            }

            try
            {
                _logger.LogInformation("Processing embeddings request for model: {Model}", request.Model);
                
                // Get provider info for usage tracking
                try
                {
                    var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                    if (modelMapping != null)
                    {
                        HttpContext.Items["ProviderId"] = modelMapping.ProviderId;
                        HttpContext.Items["ProviderType"] = modelMapping.Provider?.ProviderType;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get provider info for model {Model}", request.Model);
                }
                
                var response = await _router.CreateEmbeddingAsync(request, cancellationToken: cancellationToken);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing embeddings request for model: {Model}", request.Model);
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