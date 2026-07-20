using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Rerank;
using ConduitLLM.Gateway.Authorization;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.UsageTracking;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// Handles document reranking requests (Cohere-compatible <c>/rerank</c>).
    /// </summary>
    [ApiController]
    [Route("v1")]
    [Authorize(AuthenticationSchemes = "VirtualKey")]
    [RequireBalance]
    [Tags("Rerank")]
    public class RerankController : GatewayControllerBase
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly ILogger<RerankController> _logger;

        public RerankController(
            ILLMClientFactory clientFactory,
            IModelProviderMappingService modelMappingService,
            ILogger<RerankController> logger,
            IEventBus eventBus)
            : base(eventBus, logger)
        {
            _clientFactory = clientFactory;
            _modelMappingService = modelMappingService;
            _logger = logger;
        }

        /// <summary>
        /// Scores and orders documents by relevance to a query.
        /// </summary>
        [HttpPost("rerank")]
        public async Task<IActionResult> CreateRerank(
            [FromBody] RerankRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrEmpty(request.Model))
                return OpenAIError(400, "A model is required.", "invalid_request_error", "invalid_request");
            if (string.IsNullOrEmpty(request.Query))
                return OpenAIError(400, "A query is required.", "invalid_request_error", "invalid_request");
            if (request.Documents == null || request.Documents.Count == 0)
                return OpenAIError(400, "At least one document is required.", "invalid_request_error", "invalid_request");

            var alias = request.Model;
            var virtualKeyId = HttpContext.Items.TryGetValue("VirtualKeyId", out var keyValue) && keyValue is int keyId
                ? keyId
                : (int?)null;
            var accounting = HttpContext.GetOrCreateRequestAccountingContext();
            accounting.SetOperation(RequestOperation.Rerank, virtualKeyId, alias);
            var mapping = await _modelMappingService.GetMappingByModelAliasAsync(alias);
            var supported = mapping?.ModelProviderTypeAssociation?.Model?.SupportsRerank ?? false;
            if (mapping == null || !supported)
                return OpenAIError(400, $"Model {alias} does not support reranking.", "invalid_request_error", "unsupported_model");

            HttpContext.Items["ProviderId"] = mapping.ProviderId;
            HttpContext.Items["ProviderType"] = mapping.Provider?.ProviderType;
            if (mapping.ModelProviderTypeAssociation?.ModelCostId != null)
                HttpContext.Items[HttpContextKeys.ModelCostId] = mapping.ModelProviderTypeAssociation.ModelCostId;

            var client = await _clientFactory.GetClientAsync(alias, cancellationToken);
            var reranker = client.FindInChain<IRerankClient>();
            if (reranker == null)
                return OpenAIError(400, $"Model {alias} does not support reranking.", "invalid_request_error", "unsupported_model");

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
