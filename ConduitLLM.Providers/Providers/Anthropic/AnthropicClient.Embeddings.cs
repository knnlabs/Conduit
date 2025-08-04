using System;
using System.Threading;
using System.Threading.Tasks;

using CoreModels = ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Providers.Anthropic
{
    /// <summary>
    /// AnthropicClient partial class containing embedding functionality.
    /// </summary>
    public partial class AnthropicClient
    {
        /// <summary>
        /// Creates embeddings using the Anthropic API (not currently supported).
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>This method does not return as it throws a NotSupportedException.</returns>
        /// <remarks>
        /// <para>
        /// As of early 2025, Anthropic does not provide a public embeddings API.
        /// This method is implemented to fulfill the ILLMClient interface but will
        /// always throw a NotSupportedException.
        /// </para>
        /// <para>
        /// If Anthropic adds embedding support in the future, this method should be
        /// updated to implement the actual API call.
        /// </para>
        /// </remarks>
        /// <exception cref="NotSupportedException">Always thrown as embeddings are not supported by Anthropic.</exception>
        public override Task<CoreModels.EmbeddingResponse> CreateEmbeddingAsync(
            CoreModels.EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Embeddings are not currently supported by the Anthropic API");
        }
    }
}