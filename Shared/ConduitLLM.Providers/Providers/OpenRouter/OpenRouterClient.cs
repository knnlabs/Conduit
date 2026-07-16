using System.Text.Json;
using System.Text.Json.Serialization;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Configuration;
using InternalModels = ConduitLLM.Providers.Common.Models;
using CoreUtils = ConduitLLM.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.OpenRouter
{
    /// <summary>
    /// Client for interacting with OpenRouter's OpenAI-compatible API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenRouter is a meta-provider that routes requests to various underlying LLM providers
    /// (OpenAI, Anthropic, Google, Meta, etc.) via a unified OpenAI-compatible API.
    /// This client extends OpenAICompatibleClient with OpenRouter-specific headers and error handling.
    /// </para>
    /// <para>
    /// Key features:
    /// - Access to 100+ models from multiple providers through a single API
    /// - Full OpenAI API compatibility for chat completions
    /// - Support for streaming and non-streaming responses
    /// - Tool/function calling support (dependent on routed model)
    /// - Vision support (dependent on routed model)
    /// - Model IDs use provider/model-name format (e.g., openai/gpt-4o)
    /// - Provider routing preferences via ExtensionData (provider object)
    /// </para>
    /// <para>
    /// OpenRouter-specific request parameters can be passed via ExtensionData:
    /// - "provider": object with routing preferences (order, ignore, only, data_collection, sort, etc.)
    /// - "transforms": string[] for prompt transformations (e.g., "middle-out")
    /// - "models": string[] with "route": "fallback" for multi-model fallback
    /// </para>
    /// </remarks>
    public class OpenRouterClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        private static ProviderErrorMessages OpenRouterErrorMessages =>
            ProviderConfigurationRegistry.GetErrorMessages(ProviderType.OpenRouter);

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenRouterClient"/> class.
        /// </summary>
        /// <param name="provider">The provider configuration.</param>
        /// <param name="keyCredential">The API key credential.</param>
        /// <param name="providerModelId">The model identifier to use (e.g., openai/gpt-4o, anthropic/claude-3.5-sonnet).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for advanced usage scenarios.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public OpenRouterClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "OpenRouter",
                baseUrl: ProviderConfigurationRegistry.GetDefaultBaseUrl(ProviderType.OpenRouter),
                defaultModels: defaultModels)
        {
        }

        /// <summary>
        /// Gets available models from OpenRouter's API.
        /// </summary>
        /// <remarks>
        /// <para>
        /// OpenRouter's /models endpoint is public and does not validate API keys.
        /// To ensure the key is valid, this method first calls GET /key which requires
        /// authentication and returns 401 for invalid keys.
        /// </para>
        /// <para>
        /// OpenRouter's /models response does not include the 'owned_by' field that the base
        /// OpenAI model data type requires. This override uses a permissive model type
        /// that only requires the 'id' field.
        /// </para>
        /// </remarks>
        public override async Task<List<InternalModels.ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteApiRequestAsync(async () =>
                {
                    using var client = CreateHttpClient(apiKey);
                    var headers = CreateStandardHeaders(apiKey);

                    // Validate the API key first via GET /key (the /models endpoint is public
                    // and does not require authentication)
                    await ValidateApiKeyAsync(client, headers, cancellationToken);

                    var endpoint = GetModelsEndpoint();

                    Logger.LogDebug("Getting available models from {Provider} at {Endpoint}", ProviderName, endpoint);

                    var response = await CoreUtils.HttpClientHelper.GetJsonAsync<OpenRouterModelsResponse>(
                        client,
                        endpoint,
                        headers,
                        DefaultJsonOptions,
                        Logger,
                        cancellationToken);

                    return response.Data
                        .Select(m => InternalModels.ExtendedModelInfo.Create(m.Id, ProviderName, m.Id))
                        .ToList();
                }, "GetModels", cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to retrieve models from {Provider} API.", ProviderName);
                throw;
            }
        }

        /// <summary>
        /// Validates the API key by calling OpenRouter's GET /key endpoint.
        /// </summary>
        /// <remarks>
        /// Unlike the /models endpoint which is public, GET /key requires authentication
        /// and returns 401 for invalid keys.
        /// </remarks>
        private async Task ValidateApiKeyAsync(
            HttpClient client,
            Dictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            var keyEndpoint = $"{BaseUrl}/key";

            Logger.LogDebug("Validating API key via {Endpoint}", keyEndpoint);

            using var request = new HttpRequestMessage(HttpMethod.Get, keyEndpoint);
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            using var response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Core.Exceptions.LLMCommunicationException(
                    "Invalid API key for OpenRouter. Please verify your API key is correct.",
                    System.Net.HttpStatusCode.Unauthorized,
                    null);
            }

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning(
                    "OpenRouter key validation returned {StatusCode}, proceeding with model listing",
                    response.StatusCode);
            }
        }

        /// <summary>
        /// Refines error classification for OpenRouter-specific error patterns.
        /// </summary>
        /// <remarks>
        /// OpenRouter has specific error semantics:
        /// - 503: No provider meets routing requirements — classify as ModelNotFound
        ///   since it typically means the requested model/routing combo is unavailable
        /// - Error responses use numeric code field instead of OpenAI's string type
        /// </remarks>
        protected override ProviderErrorType RefineErrorClassification(
            ProviderErrorType baseType,
            string? responseBody)
        {
            // Apply common patterns first (quota, rate limit, model not found)
            var refined = base.RefineErrorClassification(baseType, responseBody);
            if (refined != baseType)
                return refined;

            if (string.IsNullOrEmpty(responseBody))
                return baseType;

            // OpenRouter-specific: 503 "no endpoints" → ModelNotFound
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (!doc.RootElement.TryGetProperty("error", out var error))
                    return baseType;

                var message = error.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString() ?? ""
                    : "";

                if (baseType == ProviderErrorType.ServiceUnavailable &&
                    (message.Contains("no endpoints", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("no provider", StringComparison.OrdinalIgnoreCase)))
                {
                    return ProviderErrorType.ModelNotFound;
                }
            }
            catch (JsonException)
            {
                // Not valid JSON, use base classification
            }

            return baseType;
        }

        /// <summary>
        /// Extracts enhanced error messages for OpenRouter-specific error patterns.
        /// Adds OpenRouter-specific keyword matching on top of base extraction.
        /// </summary>
        protected override string ExtractEnhancedErrorMessage(Exception ex)
        {
            var baseResult = base.ExtractEnhancedErrorMessage(ex);

            // If the base found something useful beyond the raw message, use it
            if (!string.IsNullOrEmpty(baseResult) &&
                !baseResult.Equals(ex.Message) &&
                !baseResult.Contains("Exception of type"))
            {
                return baseResult;
            }

            // OpenRouter-specific keyword matching
            var msg = ex.Message;

            if (msg.Contains("model not found", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return OpenRouterErrorMessages.ModelNotFound;
            }

            if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
            {
                return OpenRouterErrorMessages.RateLimitExceeded;
            }

            if (msg.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
            {
                return "Insufficient OpenRouter credits. Add credits at openrouter.ai/credits.";
            }

            if (msg.Contains("no endpoints found", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("no provider", StringComparison.OrdinalIgnoreCase))
            {
                return "No OpenRouter provider available for this model. The model may be temporarily unavailable or routing constraints are too restrictive.";
            }

            if (msg.Contains("moderation", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("flagged", StringComparison.OrdinalIgnoreCase))
            {
                return "Request was flagged by OpenRouter content moderation.";
            }

            // Fallback: use base result with provider prefix
            return $"OpenRouter API error: {baseResult}";
        }
    }

    /// <summary>
    /// OpenRouter-specific models list response.
    /// Unlike OpenAI, OpenRouter does not include 'owned_by' in model data.
    /// </summary>
    internal record OpenRouterModelsResponse
    {
        [JsonPropertyName("data")]
        public required List<OpenRouterModelData> Data { get; init; }
    }

    /// <summary>
    /// Minimal model data from OpenRouter's /models endpoint.
    /// Only requires 'id' — OpenRouter includes many extra fields (pricing, context_length, etc.)
    /// that are safely ignored during deserialization.
    /// </summary>
    internal record OpenRouterModelData
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
