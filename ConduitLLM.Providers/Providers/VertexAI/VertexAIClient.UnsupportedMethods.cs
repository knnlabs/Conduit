using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.VertexAI
{
    /// <summary>
    /// VertexAIClient partial class containing unsupported method implementations.
    /// </summary>
    public partial class VertexAIClient
    {
        /// <inheritdoc/>
        public override Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogWarning("Vertex AI does not provide a simple API endpoint for listing models via API key");
            throw new NotSupportedException(
                "Vertex AI does not provide a simple API endpoint for listing models via API key. " +
                "Model availability must be confirmed through Google Cloud documentation. " +
                "Configure specific model IDs directly in your application settings.");
        }

        /// <inheritdoc/>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<EmbeddingResponse>(
                new UnsupportedProviderException(ProviderModelId, "Embeddings are not supported by this provider."));
        }

        /// <inheritdoc/>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<ImageGenerationResponse>(
                new UnsupportedProviderException(ProviderModelId, "Image generation is not supported by this provider."));
        }
    }
}