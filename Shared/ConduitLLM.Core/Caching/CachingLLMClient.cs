using System.Security.Cryptography;
using System.Text;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Caching
{
    /// <summary>
    /// A decorator for <see cref="ILLMClient"/> that caches provider model metadata.
    /// Chat completion responses are always delegated to the underlying provider.
    /// </summary>
    public class CachingLLMClient : ILLMClient
    {
        private readonly ILLMClient _innerClient;
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<CachingLLMClient> _logger;

        public CachingLLMClient(
            ILLMClient innerClient,
            ICacheManager cacheManager,
            ILogger<CachingLLMClient> logger)
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default) =>
            _innerClient.CreateChatCompletionAsync(request, apiKey, cancellationToken);

        /// <inheritdoc />
        public IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default) =>
            _innerClient.StreamChatCompletionAsync(request, apiKey, cancellationToken);

        /// <inheritdoc />
        public async Task<List<string>> ListModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = $"models:{_innerClient.GetType().Name}:{(string.IsNullOrEmpty(apiKey) ? "default" : ComputeHash(apiKey))}";

            try
            {
                var result = await _cacheManager.GetOrCreateAsync(
                    cacheKey,
                    () => _innerClient.ListModelsAsync(apiKey, cancellationToken),
                    CacheRegion.ModelMetadata,
                    TimeSpan.FromHours(1),
                    cancellationToken);

                return result ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching provider model metadata");
                return await _innerClient.ListModelsAsync(apiKey, cancellationToken);
            }
        }

        /// <inheritdoc />
        public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default) =>
            _innerClient.CreateEmbeddingAsync(request, apiKey, cancellationToken);

        /// <inheritdoc />
        public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default) =>
            _innerClient.CreateImageAsync(request, apiKey, cancellationToken);

        /// <inheritdoc />
        public Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null) =>
            _innerClient.GetCapabilitiesAsync(modelId);

        private static string ComputeHash(string input) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
