using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Rerank;
using CoreModels = ConduitLLM.Core.Models;
using CoreUtils = ConduitLLM.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.OpenAICompatible
{
    /// <summary>
    /// OpenAICompatibleClient partial adding document reranking against a Cohere-compatible
    /// <c>/rerank</c> endpoint (the de-facto standard across Cohere/Jina/Voyage/TEI and OpenRouter).
    /// Whether a given model serves it is gated at the controller by the DB capability flag.
    /// </summary>
    public abstract partial class OpenAICompatibleClient : IRerankClient
    {
        /// <summary>
        /// Gets the rerank endpoint URL. Override if a provider uses a non-standard path.
        /// </summary>
        protected virtual string GetRerankEndpoint()
        {
            return $"{BaseUrl}/rerank";
        }

        /// <inheritdoc />
        public async Task<RerankResponse> CreateRerankAsync(
            RerankRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient(apiKey);

                var body = new Dictionary<string, object?>
                {
                    ["model"] = string.IsNullOrEmpty(request.Model) ? ProviderModelId : request.Model,
                    ["query"] = request.Query,
                    ["documents"] = request.Documents
                };
                if (request.TopN.HasValue)
                    body["top_n"] = request.TopN.Value;
                if (request.ReturnDocuments.HasValue)
                    body["return_documents"] = request.ReturnDocuments.Value;
                if (request.ExtensionData != null)
                {
                    foreach (var kvp in request.ExtensionData)
                        if (!body.ContainsKey(kvp.Key))
                            body[kvp.Key] = kvp.Value;
                }

                var response = await CoreUtils.HttpClientHelper.SendJsonRequestAsync<Dictionary<string, object?>, RerankResponse>(
                    client,
                    HttpMethod.Post,
                    GetRerankEndpoint(),
                    body,
                    CreateStandardHeaders(apiKey),
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);

                response.Usage ??= new CoreModels.Usage();
                // Capture any provider-reported cost for authoritative billing.
                ExtractProviderUsageFromExtensionData(response.Usage);
                // Synthesize search units when the provider doesn't report them (Cohere convention:
                // 1 search unit = 1 query + up to 100 documents).
                response.Usage.SearchUnits ??= Math.Max(1, (int)Math.Ceiling(request.Documents.Count / 100.0));
                return response;
            }, "CreateRerank", cancellationToken);
        }
    }
}
