using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Providers.MiniMax
{
    /// <summary>
    /// MiniMaxClient partial class containing embeddings functionality.
    /// </summary>
    public partial class MiniMaxClient
    {
        /// <inheritdoc />
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateEmbedding");

            throw new System.NotSupportedException("MiniMax provider does not support embeddings.");
        }
    }
}