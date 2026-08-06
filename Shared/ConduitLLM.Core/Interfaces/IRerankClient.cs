using ConduitLLM.Core.Models.Rerank;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Optional capability: document reranking. Implemented by provider clients that support a
    /// <c>/rerank</c> endpoint and discovered via
    /// <see cref="LLMClientDecoratorExtensions.FindInChain{T}"/> (it is not part of <see cref="ILLMClient"/>).
    /// </summary>
    public interface IRerankClient
    {
        /// <summary>
        /// Scores and orders the supplied documents by relevance to the query.
        /// </summary>
        Task<RerankResponse> CreateRerankAsync(
            RerankRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);
    }
}
