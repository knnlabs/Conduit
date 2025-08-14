using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Core.Caching
{
    /// <summary>
    /// A decorator for ILLMClient that adds caching functionality
    /// </summary>
    public class CachingLLMClient : ILLMClient
    {
        private readonly ILLMClient _innerClient;
        private readonly ICacheService _cacheService;
        private readonly ICacheMetricsService _metricsService;
        private readonly IOptions<CacheOptions> _cacheOptions;
        private readonly ILogger<CachingLLMClient> _logger;
        private readonly bool _isEnabled;

        // Cache key prefixes for different operations
        private const string COMPLETION_CACHE_PREFIX = "llm:completion:";
        private const string MODEL_LIST_CACHE_PREFIX = "llm:models:";

        /// <summary>
        /// Creates a new instance of the CachingLLMClient
        /// </summary>
        /// <param name="innerClient">The inner LLM client to decorate</param>
        /// <param name="cacheService">The cache service</param>
        /// <param name="metricsService">The cache metrics service</param>
        /// <param name="cacheOptions">The cache options</param>
        /// <param name="logger">The logger</param>
        public CachingLLMClient(
            ILLMClient innerClient,
            ICacheService cacheService,
            ICacheMetricsService metricsService,
            IOptions<CacheOptions> cacheOptions,
            ILogger<CachingLLMClient> logger)
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _cacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _isEnabled = _cacheOptions.Value.IsEnabled;
        }

        /// <inheritdoc />
        public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Skip caching if disabled or for streaming requests
            if (!_isEnabled || (request.Stream.HasValue && request.Stream.Value))
            {
                return await _innerClient.CreateChatCompletionAsync(request, apiKey, cancellationToken);
            }

            // Generate a cache key based on the request
            string cacheKey = GenerateCacheKey(request, apiKey);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Try to get from cache first
                var cachedResponse = _cacheService.Get<ChatCompletionResponse>(cacheKey);

                if (cachedResponse != null)
                {
                    stopwatch.Stop();
                    _logger.LogDebug("Cache hit for key {CacheKey}, retrieval took {ElapsedMs}ms", cacheKey, stopwatch.ElapsedMilliseconds);

                    // Track metrics for cache hit
                    _metricsService.RecordHit(stopwatch.ElapsedMilliseconds, request.Model);

                    // Clone the cached response to ensure we don't modify the cached object
                    // This is important for thread safety and to avoid odd side effects
                    return CloneChatCompletionResponse(cachedResponse) ?? await _innerClient.CreateChatCompletionAsync(request, apiKey, cancellationToken);
                }

                // Cache miss, get from the actual LLM provider
                _logger.LogDebug("Cache miss for key {CacheKey}", cacheKey);
                _metricsService.RecordMiss(request.Model);

                // Measure time to get from provider
                var providerStopwatch = Stopwatch.StartNew();
                var response = await _innerClient.CreateChatCompletionAsync(request, apiKey, cancellationToken);
                providerStopwatch.Stop();

                _logger.LogDebug("LLM provider response took {ElapsedMs}ms", providerStopwatch.ElapsedMilliseconds);

                // Cache the response with appropriate TTL
                TimeSpan? cacheExpiration = GetCacheExpiration(request.Model);

                if (cacheExpiration.HasValue)
                {
                    _cacheService.Set(cacheKey, response, cacheExpiration);
                    _logger.LogDebug("Cached response with key {CacheKey} for {ExpirationMinutes} minutes",
                        cacheKey, cacheExpiration.Value.TotalMinutes);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in caching layer for {CacheKey}", cacheKey);
                // Fall back to the inner client
                return await _innerClient.CreateChatCompletionAsync(request, apiKey, cancellationToken);
            }
        }

        /// <inheritdoc />
        public IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // We don't cache streaming responses
            return _innerClient.StreamChatCompletionAsync(request, apiKey, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<List<string>> ListModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Skip caching if disabled
            if (!_isEnabled)
            {
                return await _innerClient.ListModelsAsync(apiKey, cancellationToken);
            }

            // Generate a cache key for the models list
            string cacheKey = $"{MODEL_LIST_CACHE_PREFIX}{_innerClient.GetType().Name}:{(!string.IsNullOrEmpty(apiKey) ? ComputeHash(apiKey) : "default")}";

            try
            {
                // Try to get from cache first with longer TTL for model lists
                var result = await _cacheService.GetOrCreateAsync(
                    cacheKey,
                    async () => await _innerClient.ListModelsAsync(apiKey, cancellationToken),
                    TimeSpan.FromHours(1)); // Cache model lists for an hour

                return result ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in caching layer for models list");
                // Fall back to the inner client
                return await _innerClient.ListModelsAsync(apiKey, cancellationToken);
            }
        }

        /// <inheritdoc />
        public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
            => _innerClient.CreateEmbeddingAsync(request, apiKey, cancellationToken);

        /// <inheritdoc />
        public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
            => _innerClient.CreateImageAsync(request, apiKey, cancellationToken);

        /// <summary>
        /// Generates a cache key for the request
        /// </summary>
        private string GenerateCacheKey(ChatCompletionRequest request, string? apiKey)
        {
            var options = _cacheOptions.Value;
            var keyBuilder = new StringBuilder(COMPLETION_CACHE_PREFIX);

            // Add model to the key
            keyBuilder.Append(request.Model.ToLowerInvariant());

            // Optionally include provider information
            if (options.IncludeProviderInKey && _innerClient.GetType() != null)
            {
                keyBuilder.Append(':');
                keyBuilder.Append(_innerClient.GetType().Name);
            }

            // Add a prefix for the API key if provided (use a hash for security)
            if (!string.IsNullOrEmpty(apiKey) && options.IncludeApiKeyInKey)
            {
                keyBuilder.Append(':');
                keyBuilder.Append(ComputeHash(apiKey));
            }

            // Start a separate hash content for the request's content
            var requestHashContent = new StringBuilder();

            // Include messages (always included)
            foreach (var message in request.Messages)
            {
                requestHashContent.Append(message.Role);
                requestHashContent.Append(':');
                requestHashContent.Append(message.Content);
                requestHashContent.Append(';');
            }

            // Optional parameters based on configuration
            if (options.IncludeTemperatureInKey && request.Temperature.HasValue)
            {
                requestHashContent.Append($"temp:{request.Temperature};");
            }

            if (options.IncludeMaxTokensInKey && request.MaxTokens.HasValue)
            {
                requestHashContent.Append($"max:{request.MaxTokens};");
            }

            if (options.IncludeTopPInKey && request.TopP.HasValue)
            {
                requestHashContent.Append($"topp:{request.TopP};");
            }

            // Compute a hash of the request content based on the chosen algorithm
            string requestHash = options.HashAlgorithm.ToUpperInvariant() switch
            {
                "MD5" => ComputeMD5Hash(requestHashContent.ToString()),
                "SHA256" => ComputeSHA256Hash(requestHashContent.ToString()),
                _ => ComputeMD5Hash(requestHashContent.ToString()) // Default to MD5
            };

            // Append the content hash to the key
            keyBuilder.Append(':');
            keyBuilder.Append(requestHash);

            return keyBuilder.ToString();
        }

        /// <summary>
        /// Gets the cache expiration for a specific model
        /// </summary>
        private TimeSpan? GetCacheExpiration(string model)
        {
            var options = _cacheOptions.Value;

            // Check model-specific rules first
            if (options.ModelSpecificRules != null && options.ModelSpecificRules.Count() > 0)
            {
                foreach (var rule in options.ModelSpecificRules)
                {
                    if (string.IsNullOrEmpty(rule.ModelNamePattern))
                        continue;

                    if (model.Contains(rule.ModelNamePattern, StringComparison.OrdinalIgnoreCase))
                    {
                        if (rule.CacheBehavior.Equals(CacheBehavior.Never))
                        {
                            return null; // Don't cache
                        }

                        if (rule.ExpirationMinutes.HasValue && rule.ExpirationMinutes.Value > 0)
                        {
                            return TimeSpan.FromMinutes(rule.ExpirationMinutes.Value);
                        }
                    }
                }
            }

            // Fall back to default expiration
            return TimeSpan.FromMinutes(options.DefaultExpirationMinutes);
        }

        /// <summary>
        /// Computes a generic hash of a string
        /// </summary>
        private string ComputeHash(string input)
        {
            return ComputeMD5Hash(input);
        }

        /// <summary>
        /// Computes an MD5 hash of a string
        /// </summary>
        private string ComputeMD5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Computes a SHA256 hash of a string
        /// </summary>
        private string ComputeSHA256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = sha256.ComputeHash(inputBytes);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Clones a ChatCompletionResponse object to ensure thread safety
        /// </summary>
        private ChatCompletionResponse? CloneChatCompletionResponse(ChatCompletionResponse? original)
        {
            if (original == null)
                return null;

            try
            {
                // Use serialization for a deep clone
                var json = JsonSerializer.Serialize(original);
                return JsonSerializer.Deserialize<ChatCompletionResponse>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cloning chat completion response");

                // Create a minimal valid response with required properties
                return new ChatCompletionResponse
                {
                    Id = original.Id ?? "fallback-id",
                    Object = original.Object ?? "chat.completion",
                    Created = original.Created,
                    Model = original.Model ?? "unknown",
                    Choices = original.Choices ?? new List<Choice>(),
                    Usage = original.Usage
                };
            }
        }

        /// <summary>
        /// Gets the capabilities supported by the provider.
        /// </summary>
        public Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            // No caching needed for capabilities
            return _innerClient.GetCapabilitiesAsync(modelId);
        }
    }
}
