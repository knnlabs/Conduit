using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Base class for LLM client implementations that provides common functionality 
    /// and standardized handling of requests, responses, and errors.
    /// </summary>
    public abstract class BaseLLMClient : ILLMClient
    {
        protected readonly ProviderCredentials Credentials;
        protected readonly string ProviderModelId;
        protected readonly ILogger Logger;
        protected readonly string ProviderName;
        protected readonly IHttpClientFactory? HttpClientFactory;
        protected readonly ProviderDefaultModels? DefaultModels;

        protected static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseLLMClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials to use for requests.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use for logging.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for creating HttpClient instances.</param>
        /// <param name="providerName">The name of this LLM provider.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        protected BaseLLMClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            string? providerName = null,
            ProviderDefaultModels? defaultModels = null)
        {
            Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            ProviderModelId = providerModelId ?? throw new ArgumentNullException(nameof(providerModelId));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            HttpClientFactory = httpClientFactory;
            ProviderName = providerName ?? GetType().Name.Replace("Client", string.Empty);
            DefaultModels = defaultModels;

            ValidateCredentials();
        }

        /// <summary>
        /// Validates that the required credentials are present.
        /// Override in derived classes to add provider-specific validation.
        /// </summary>
        protected virtual void ValidateCredentials()
        {
            if (string.IsNullOrWhiteSpace(Credentials.ApiKey))
            {
                throw new ConfigurationException($"API key is missing for provider '{ProviderName}'");
            }
        }

        /// <summary>
        /// Creates a configured HttpClient for making requests to the provider API.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <returns>A configured HttpClient instance.</returns>
        protected virtual HttpClient CreateHttpClient(string? apiKey = null)
        {
            HttpClient client;

            if (HttpClientFactory != null)
            {
                client = HttpClientFactory.CreateClient($"{ProviderName}LLMClient");
            }
            else
            {
                client = new HttpClient();
            }

            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey!;
            if (string.IsNullOrWhiteSpace(effectiveApiKey))
            {
                throw new ConfigurationException($"API key is missing for provider '{ProviderName}'");
            }

            ConfigureHttpClient(client, effectiveApiKey);
            return client;
        }

        /// <summary>
        /// Configures the HttpClient with necessary headers and settings.
        /// Override in derived classes to add provider-specific configuration.
        /// </summary>
        /// <param name="client">The HttpClient to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        protected virtual void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // Configure authentication
            ConfigureAuthentication(client, apiKey);
            
            // Configure timeout
            client.Timeout = TimeSpan.FromMinutes(5);
        }
        
        /// <summary>
        /// Configures authentication for the HttpClient.
        /// Override in derived classes to use provider-specific authentication methods.
        /// </summary>
        /// <param name="client">The HttpClient to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        protected virtual void ConfigureAuthentication(HttpClient client, string apiKey)
        {
            // Default Bearer token authentication - can be overridden by providers
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <summary>
        /// Creates a chat completion using the LLM provider.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response.</returns>
        public abstract Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams a chat completion using the LLM provider.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        public abstract IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available models from the LLM provider.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available models.</returns>
        public abstract Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists available model IDs from the LLM provider.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available model IDs.</returns>
        public virtual async Task<List<string>> ListModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Default implementation calls GetModelsAsync and extracts just the IDs
            var models = await GetModelsAsync(apiKey, cancellationToken);
            return models.Select(m => m.Id).ToList();
        }

        /// <summary>
        /// Creates embeddings using the LLM provider.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An embedding response.</returns>
        public abstract Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates images using the LLM provider.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An image generation response.</returns>
        public abstract Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads error content from an HTTP response.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The error content as a string.</returns>
        protected async Task<string> ReadErrorContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return await ConduitLLM.Core.Utilities.HttpClientHelper.ReadErrorContentAsync(response, cancellationToken);
        }

        /// <summary>
        /// Safely executes an API request with standardized error handling.
        /// </summary>
        /// <typeparam name="TResult">The type of result expected from the operation.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="operationName">The name of the operation for error messages.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The result of the operation.</returns>
        protected async Task<TResult> ExecuteApiRequestAsync<TResult>(
            Func<Task<TResult>> operation,
            string operationName,
            CancellationToken cancellationToken)
        {
            return await ExceptionHandler.HandleHttpRequestAsync(
                async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await operation();
                },
                Logger,
                $"{ProviderName} ({operationName})");
        }

        /// <summary>
        /// Prepares and validates a request before sending it to the API.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <param name="request">The request to validate.</param>
        /// <param name="operationName">The name of the operation for error messages.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        protected virtual void ValidateRequest<TRequest>(TRequest request, string operationName)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request),
                    $"Request cannot be null for {operationName} operation");
            }
        }

        /// <summary>
        /// Creates a dictionary of standard headers for API requests.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <returns>A dictionary of headers.</returns>
        protected virtual Dictionary<string, string> CreateStandardHeaders(string? apiKey = null)
        {
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey!;

            var headers = new Dictionary<string, string>
            {
                ["User-Agent"] = "ConduitLLM"
            };

            // Add authentication - default to Bearer
            // Override in derived classes to use different auth methods
            headers["Authorization"] = $"Bearer {effectiveApiKey}";

            return headers;
        }
    }
}
