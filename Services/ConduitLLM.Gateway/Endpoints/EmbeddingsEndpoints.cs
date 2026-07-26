using System.Diagnostics;
using ConduitLLM.Core;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Metrics;
using GatewayOpsMetrics = ConduitLLM.Gateway.Services.GatewayOperationsMetricsService;

using ConduitLLM.Configuration.Messaging;

using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.UsageTracking;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Handles embedding generation requests following OpenAI's API format.
    /// </summary>
    public class EmbeddingsEndpoints : GatewayEndpointHandlerBase
    {
        private readonly Conduit _conduit;
        private readonly ConduitLLM.Configuration.Interfaces.IModelProviderMappingService _modelMappingService;

        public EmbeddingsEndpoints(
            Conduit conduit,
            ILogger<EmbeddingsEndpoints> logger,
            ConduitLLM.Configuration.Interfaces.IModelProviderMappingService modelMappingService,
            IEventBus eventBus,
            IHttpContextAccessor httpContextAccessor) : base(eventBus, httpContextAccessor, logger)
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
        public async Task<IResult> CreateEmbedding(
            EmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return OpenAIError(400, "Invalid request body.", "invalid_request");
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
