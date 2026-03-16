using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Configuration;

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
            if (string.IsNullOrEmpty(responseBody))
                return baseType;

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (!doc.RootElement.TryGetProperty("error", out var error))
                    return baseType;

                var message = error.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString() ?? ""
                    : "";

                // OpenRouter 503 "no endpoints found" means no provider can serve
                // this model/routing combo — more like ModelNotFound than ServiceUnavailable
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
        /// </summary>
        protected override string ExtractEnhancedErrorMessage(Exception ex)
        {
            var baseErrorMessage = base.ExtractEnhancedErrorMessage(ex);

            if (!string.IsNullOrEmpty(baseErrorMessage) &&
                !baseErrorMessage.Equals(ex.Message) &&
                !baseErrorMessage.Contains("Exception of type"))
            {
                return baseErrorMessage;
            }

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

            if (ex.Data.Contains("Body") && ex.Data["Body"] is string body && !string.IsNullOrEmpty(body))
            {
                return $"OpenRouter API error: {body}";
            }

            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
            {
                return $"OpenRouter API error: {ex.InnerException.Message}";
            }

            return $"OpenRouter API error: {msg}";
        }
    }
}
