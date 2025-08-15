using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Replicate
{
    public partial class ReplicateClient
    {
        /// <inheritdoc/>
        public override Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogWarning("Replicate does not provide a public API endpoint for listing available models");
            throw new NotSupportedException(
                "Replicate does not provide a public API endpoint for listing available models. " +
                "Model discovery must be done through the Replicate website or documentation. " +
                "Configure specific model IDs directly in your application settings.");
        }

        /// <inheritdoc/>
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateEmbeddingAsync");

            // While Replicate does have embedding models, the implementation would be similar to chat completion
            // For now, we'll throw NotSupportedException, but this could be implemented in the future
            Logger.LogWarning("Embeddings are not currently supported by ReplicateClientRevised.");
            return await Task.FromException<EmbeddingResponse>(
                new NotSupportedException("Embeddings are not currently supported by ReplicateClientRevised."));
        }
    }
}