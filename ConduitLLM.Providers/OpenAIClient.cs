using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with OpenAI-compatible APIs, including standard OpenAI,
    /// Azure OpenAI, and other compatible endpoints.
    /// </summary>
    /// <remarks>
    /// This client implements the ILLMClient interface for OpenAI-compatible APIs,
    /// providing a consistent interface for chat completions, embeddings, and image generation.
    /// It supports both OpenAI's standard API endpoint structure and Azure OpenAI's deployment-based
    /// endpoints, with automatic URL and authentication format selection based on the provider name.
    /// </remarks>
    public class OpenAIClient : OpenAICompatibleClient
    {
        // Default API configuration constants
        private static class Constants
        {
            public static class Urls
            {
                public const string DefaultOpenAIApiBase = "https://api.openai.com/v1";
            }
            
            public static class ApiVersions
            {
                public const string DefaultAzureApiVersion = "2024-02-01";
            }
            
            public static class Endpoints
            {
                public const string ChatCompletions = "/chat/completions";
                public const string Models = "/models";
                public const string Embeddings = "/embeddings";
                public const string ImageGenerations = "/images/generations";
            }
        }
        
        private readonly bool _isAzure;

        /// <summary>
        /// Initializes a new instance of the OpenAIClient class.
        /// </summary>
        /// <param name="credentials">Provider credentials containing API key and endpoint configuration.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider. For Azure, this is the deployment name.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <param name="providerName">Optional provider name override. If not specified, uses credentials.ProviderName or defaults to "openai".</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing for non-Azure providers.</exception>
        public OpenAIClient(
            ProviderCredentials credentials, 
            string providerModelId, 
            ILogger<OpenAIClient> logger,
            IHttpClientFactory httpClientFactory, 
            string? providerName = null)
            : base(
                credentials,
                providerModelId,
                logger,
                httpClientFactory,
                providerName ?? credentials.ProviderName ?? "openai",
                DetermineBaseUrl(credentials, providerName ?? credentials.ProviderName ?? "openai"))
        {
            _isAzure = (providerName ?? credentials.ProviderName ?? "openai").Equals("azure", StringComparison.OrdinalIgnoreCase);
            
            // Specific validation for Azure credentials
            if (_isAzure && string.IsNullOrWhiteSpace(credentials.ApiBase))
            {
                throw new ConfigurationException("ApiBase (Azure resource endpoint) is required for the 'azure' provider.");
            }
        }

        /// <summary>
        /// Determines the appropriate base URL based on the provider and credentials.
        /// </summary>
        private static string DetermineBaseUrl(ProviderCredentials credentials, string providerName)
        {
            // For Azure, we'll handle this specially in the endpoint methods
            if (providerName.Equals("azure", StringComparison.OrdinalIgnoreCase))
            {
                return credentials.ApiBase ?? "";
            }
            
            // For standard OpenAI or compatible providers
            return string.IsNullOrWhiteSpace(credentials.ApiBase) 
                ? Constants.Urls.DefaultOpenAIApiBase 
                : credentials.ApiBase.TrimEnd('/');
        }

        /// <summary>
        /// Configures the HTTP client with appropriate headers and settings.
        /// </summary>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            
            // Different authentication method for Azure vs. standard OpenAI
            if (_isAzure)
            {
                client.DefaultRequestHeaders.Add("api-key", apiKey);
            }
            else
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
        }

        /// <summary>
        /// Gets the chat completion endpoint, which differs between Azure and standard OpenAI.
        /// </summary>
        protected override string GetChatCompletionEndpoint()
        {
            if (_isAzure)
            {
                string apiVersion = !string.IsNullOrWhiteSpace(Credentials.ApiVersion) 
                    ? Credentials.ApiVersion 
                    : Constants.ApiVersions.DefaultAzureApiVersion;
                
                return $"{BaseUrl.TrimEnd('/')}/openai/deployments/{ProviderModelId}/chat/completions?api-version={apiVersion}";
            }
            
            return $"{BaseUrl}{Constants.Endpoints.ChatCompletions}";
        }

        /// <summary>
        /// Gets the models endpoint, which differs between Azure and standard OpenAI.
        /// </summary>
        protected override string GetModelsEndpoint()
        {
            if (_isAzure)
            {
                string apiVersion = !string.IsNullOrWhiteSpace(Credentials.ApiVersion) 
                    ? Credentials.ApiVersion 
                    : Constants.ApiVersions.DefaultAzureApiVersion;
                
                return $"{BaseUrl.TrimEnd('/')}/openai/deployments?api-version={apiVersion}";
            }
            
            return $"{BaseUrl}{Constants.Endpoints.Models}";
        }

        /// <summary>
        /// Gets the embedding endpoint, which differs between Azure and standard OpenAI.
        /// </summary>
        protected override string GetEmbeddingEndpoint()
        {
            if (_isAzure)
            {
                string apiVersion = !string.IsNullOrWhiteSpace(Credentials.ApiVersion) 
                    ? Credentials.ApiVersion 
                    : Constants.ApiVersions.DefaultAzureApiVersion;
                
                return $"{BaseUrl.TrimEnd('/')}/openai/deployments/{ProviderModelId}/embeddings?api-version={apiVersion}";
            }
            
            return $"{BaseUrl}{Constants.Endpoints.Embeddings}";
        }

        /// <summary>
        /// Gets the image generation endpoint, which differs between Azure and standard OpenAI.
        /// </summary>
        protected override string GetImageGenerationEndpoint()
        {
            if (_isAzure)
            {
                string apiVersion = !string.IsNullOrWhiteSpace(Credentials.ApiVersion) 
                    ? Credentials.ApiVersion 
                    : Constants.ApiVersions.DefaultAzureApiVersion;
                
                return $"{BaseUrl.TrimEnd('/')}/openai/deployments/{ProviderModelId}/images/generations?api-version={apiVersion}";
            }
            
            return $"{BaseUrl}{Constants.Endpoints.ImageGenerations}";
        }
        
        /// <summary>
        /// Maps the Azure OpenAI response format to the standard models list.
        /// </summary>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (_isAzure)
            {
                // Azure has a different response format for listing models
                return await ExecuteApiRequestAsync(async () =>
                {
                    using var client = CreateHttpClient(apiKey);
                    var endpoint = GetModelsEndpoint();
                    
                    var response = await ConduitLLM.Core.Utilities.HttpClientHelper.SendJsonRequestAsync<object, AzureOpenAIModels.ListDeploymentsResponse>(
                        client,
                        HttpMethod.Get,
                        endpoint,
                        // Use empty string instead of null to avoid possible null reference
                        string.Empty,
                        new Dictionary<string, string>(),
                        DefaultJsonOptions,
                        Logger,
                        cancellationToken);
                    
                    return response.Data
                        .Select(m => {
                            var model = ExtendedModelInfo.Create(m.DeploymentId, ProviderName, m.DeploymentId)
                                .WithName(m.Model ?? m.DeploymentId)
                                .WithCapabilities(new ModelCapabilities {
                                    Chat = true,
                                    TextGeneration = true
                                });
                                
                            // Can't add custom properties directly, but they'll be ignored anyway
                            return model;
                        })
                        .ToList();
                }, "GetModels", cancellationToken);
            }
            
            // Use the base implementation for standard OpenAI
            return await base.GetModelsAsync(apiKey, cancellationToken);
        }
    }
    
    // Azure-specific model response structures
    namespace AzureOpenAIModels
    {
        public class ListDeploymentsResponse
        {
            [JsonPropertyName("data")]
            public List<DeploymentInfo> Data { get; set; } = new();
        }
        
        public class DeploymentInfo
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;
            
            [JsonPropertyName("deploymentId")]
            public string DeploymentId { get; set; } = string.Empty;
            
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
            
            [JsonPropertyName("provisioningState")]
            public string ProvisioningState { get; set; } = string.Empty;
        }
    }
}