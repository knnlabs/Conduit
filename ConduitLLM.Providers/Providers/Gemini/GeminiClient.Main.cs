using System;
using System.Net.Http;
using System.Net.Http.Headers;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Gemini
{
    /// <summary>
    /// Main partial class for GeminiClient containing constructor and configuration.
    /// </summary>
    public partial class GeminiClient : CustomProviderClient
    {
        // Gemini-specific constants
        private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/";
        private const string DefaultApiVersion = "v1beta";

        /// <summary>
        /// Initializes a new instance of the <see cref="GeminiClient"/> class.
        /// </summary>
        /// <param name="provider">The provider entity containing configuration.</param>
        /// <param name="keyCredential">The key credential for accessing the Gemini API.</param>
        /// <param name="providerModelId">The provider model ID to use (e.g., gemini-1.5-flash-latest).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for advanced usage scenarios.</param>
        /// <param name="apiVersion">The API version to use. Defaults to v1beta.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public GeminiClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<GeminiClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            string? apiVersion = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "Gemini",
                defaultModels: defaultModels)
        {
            // API version is now constant, ignoring apiVersion parameter
        }

        /// <inheritdoc/>
        protected override void ValidateCredentials()
        {
            base.ValidateCredentials();

            if (string.IsNullOrWhiteSpace(PrimaryKeyCredential.ApiKey))
            {
                throw new ConfigurationException($"API key is missing for provider '{ProviderName}'.");
            }
        }

        /// <summary>
        /// Validates that the request is compatible with the selected model,
        /// particularly checking if multimodal content is being sent to a text-only model.
        /// </summary>
        /// <param name="request">The request to validate</param>
        /// <param name="methodName">Name of the calling method for logging</param>
        protected override void ValidateRequest<TRequest>(TRequest request, string methodName)
        {
            base.ValidateRequest(request, methodName);

            // Only apply vision validation for chat completion requests
            if (request is ChatCompletionRequest chatRequest)
            {
                // Check if we're sending multimodal content to a non-vision model
                bool containsImages = false;

                foreach (var message in chatRequest.Messages)
                {
                    if (message.Content != null && message.Content is not string)
                    {
                        // If content is not a string, assume it might contain images
                        containsImages = true;
                        break;
                    }
                }

                if (containsImages && !IsVisionCapableModel(ProviderModelId))
                {
                    Logger.LogWarning(
                        "Multimodal content detected but model '{ProviderModelId}' does not support vision capabilities.",
                        ProviderModelId);
                    throw new ValidationException(
                        $"Cannot send image content to model '{ProviderModelId}' as it does not support vision capabilities. " +
                        $"Please use a vision-capable model such as 'gemini-pro-vision' or 'gemini-1.5-pro'.");
                }
            }
        }

        /// <inheritdoc/>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Don't call base to avoid setting Bearer authorization as Gemini uses query param authentication
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // For Gemini, the API key is added to the query string in each request, not in the headers

            // Set the base URL with no trailing slash
            if (client.BaseAddress == null && !string.IsNullOrEmpty(BaseUrl))
            {
                client.BaseAddress = new Uri(BaseUrl.TrimEnd('/'));
            }
        }
    }
}