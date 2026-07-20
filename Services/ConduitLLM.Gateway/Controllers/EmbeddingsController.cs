using System.Diagnostics;
using ConduitLLM.Core;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Metrics;
using GatewayOpsMetrics = ConduitLLM.Gateway.Services.GatewayOperationsMetricsService;

using ConduitLLM.Configuration.Messaging;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Gateway.Authorization;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Filters;
using ConduitLLM.Gateway.UsageTracking;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// Handles embedding generation requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1")]
    [Authorize(AuthenticationSchemes = "VirtualKey")]
    [RequireBalance]
    [Tags("Embeddings")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class EmbeddingsController : GatewayControllerBase
    {
        private readonly Conduit _conduit;
        private readonly ConduitLLM.Configuration.Interfaces.IModelProviderMappingService _modelMappingService;

        public EmbeddingsController(
            Conduit conduit,
            ILogger<EmbeddingsController> logger,
            ConduitLLM.Configuration.Interfaces.IModelProviderMappingService modelMappingService,
            IEventBus eventBus) : base(eventBus, logger)
        {
            _conduit = conduit ?? throw new ArgumentNullException(nameof(conduit));
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

            using var activity = GatewayRequestMetrics.StartEmbeddingsActivity(request.Model);
            var sw = Stopwatch.StartNew();
            var virtualKeyId = HttpContext.Items.TryGetValue("VirtualKeyId", out var keyValue) && keyValue is int keyId
                ? keyId
                : (int?)null;
            var accounting = HttpContext.GetOrCreateRequestAccountingContext();
            accounting.SetOperation(RequestOperation.Embedding, virtualKeyId, request.Model);

            Logger.LogInformation("Processing embeddings request for model: {Model}", LoggingSanitizer.S(request.Model));

            // Get provider info for usage tracking
            try
            {
                var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                if (modelMapping != null)
                {
                    HttpContext.Items["ProviderId"] = modelMapping.ProviderId;
                    HttpContext.Items["ProviderType"] = modelMapping.Provider?.ProviderType;

                    if (modelMapping.ModelProviderTypeAssociation?.ModelCostId != null)
                    {
                        HttpContext.Items[HttpContextKeys.ModelCostId] =
                            modelMapping.ModelProviderTypeAssociation.ModelCostId;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to get provider info for model {Model}", request.Model);
            }

            // Get the client for the specified model and create embeddings
            var client = await _conduit.GetClientAsync(request.Model, cancellationToken);
            var result = await client.CreateEmbeddingAsync(request, cancellationToken: cancellationToken);
            accounting.RecordProviderUsage(result.Usage, request.Model, UsageEvidenceSource.Provider);
            GatewayOpsMetrics.RecordLlmOperation("embedding", request.Model, "success", sw.Elapsed.TotalSeconds);
            return Ok(result);
        }
    }
}
