using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Rerank;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.UsageTracking;


namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Handles document reranking requests (Cohere-compatible <c>/rerank</c>).
    /// </summary>
    public class RerankEndpoints : GatewayEndpointHandlerBase
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly ILogger<RerankEndpoints> _logger;

        public RerankEndpoints(
            ILLMClientFactory clientFactory,
            IModelProviderMappingService modelMappingService,
            ILogger<RerankEndpoints> logger,
            IEventPublisher eventPublisher,
            IHttpContextAccessor httpContextAccessor)
            : base(eventPublisher, httpContextAccessor, logger)
        {
            _clientFactory = clientFactory;
            _modelMappingService = modelMappingService;
            _logger = logger;
        }

        /// <summary>
        /// Scores and orders documents by relevance to a query.
        /// </summary>
        public async Task<IResult> CreateRerank(
            RerankRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrEmpty(request.Model))
                return OpenAIError(400, "A model is required.", "invalid_request");
            if (string.IsNullOrEmpty(request.Query))
                return OpenAIError(400, "A query is required.", "invalid_request");
            if (request.Documents == null || request.Documents.Count == 0)
                return OpenAIError(400, "At least one document is required.", "invalid_request");

            var alias = request.Model;
            var virtualKeyId = HttpContext.Items.TryGetValue("VirtualKeyId", out var keyValue) && keyValue is int keyId
                ? keyId
                : (int?)null;
            var accounting = HttpContext.GetOrCreateRequestAccountingContext();
            accounting.SetOperation(RequestOperation.Rerank, virtualKeyId, alias);
            var mapping = await _modelMappingService.GetMappingByModelAliasAsync(alias);
            var supported = mapping?.ModelProviderTypeAssociation?.Model?.SupportsRerank ?? false;
            if (mapping == null || !supported)
                return OpenAIError(400, $"Model {alias} does not support reranking.", "unsupported_model");

            HttpContext.Items["ProviderId"] = mapping.ProviderId;
            HttpContext.Items["ProviderType"] = mapping.Provider?.ProviderType;
            if (mapping.ModelProviderTypeAssociation?.ModelCostId != null)
                HttpContext.Items[HttpContextKeys.ModelCostId] = mapping.ModelProviderTypeAssociation.ModelCostId;

            var client = await _clientFactory.GetClientAsync(alias, cancellationToken);
            var reranker = client.FindInChain<IRerankClient>();
            if (reranker == null)
                return OpenAIError(400, $"Model {alias} does not support reranking.", "unsupported_model");

            request.Model = mapping.ProviderModelId; // swap alias -> provider model id before dispatch

            var result = await reranker.CreateRerankAsync(request, cancellationToken: cancellationToken);
            // Echo the caller's alias; the middleware bills from usage.search_units + the stamped ModelCostId.
            result.Model = alias;
            if (result.Usage is not null)
                accounting.RecordProviderUsage(result.Usage, alias, UsageEvidenceSource.Provider);

            return Ok(result);
        }
    }
}
