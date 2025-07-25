using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Model discovery provider for Cerebras high-performance inference models.
    /// Uses Cerebras's model discovery API with Bearer token authentication.
    /// </summary>
    public class CerebrasDiscoveryProvider : BaseModelDiscoveryProvider
    {
        private const string ModelsEndpoint = "https://api.cerebras.ai/v1/models";
        private readonly string? _apiKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="CerebrasDiscoveryProvider"/> class.
        /// </summary>
        /// <param name="httpClient">HTTP client for making API requests.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <param name="apiKey">API key for Cerebras authentication.</param>
        public CerebrasDiscoveryProvider(
            HttpClient httpClient, 
            ILogger<CerebrasDiscoveryProvider> logger,
            string? apiKey = null) 
            : base(httpClient, logger)
        {
            _apiKey = apiKey;
        }

        /// <inheritdoc />
        public override string ProviderName => "cerebras";

        /// <inheritdoc />
        public override bool SupportsDiscovery => !string.IsNullOrEmpty(_apiKey);
        
        /// <inheritdoc />
        public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            // If no API key, return false for availability
            if (string.IsNullOrEmpty(_apiKey))
            {
                return false;
            }

            try
            {
                // Try to make a simple request to check if the service is available
                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                request.Headers.UserAgent.ParseAdd("ConduitLLM-CerebrasDiscovery/1.0");

                using var response = await HttpClient.SendAsync(request, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Cerebras availability check failed");
                return false;
            }
        }

        /// <inheritdoc />
        public override async Task<List<ModelMetadata>> DiscoverModelsAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Logger.LogWarning("No API key provided for Cerebras model discovery");
                return GetFallbackModels();
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                request.Headers.UserAgent.ParseAdd("ConduitLLM-CerebrasDiscovery/1.0");

                using var response = await HttpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Cerebras models API returned {StatusCode}: {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    return GetFallbackModels();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return ParseApiResponse(content);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching models from Cerebras API");
                return GetFallbackModels();
            }
        }

        /// <summary>
        /// Parses the Cerebras API response to extract model information.
        /// </summary>
        /// <param name="jsonContent">The JSON response from the Cerebras API.</param>
        /// <returns>A list of model metadata.</returns>
        private List<ModelMetadata> ParseApiResponse(string jsonContent)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                var models = new List<ModelMetadata>();

                if (document.RootElement.TryGetProperty("data", out var dataElement))
                {
                    foreach (var modelElement in dataElement.EnumerateArray())
                    {
                        var modelInfo = ParseModelElement(modelElement);
                        if (modelInfo != null)
                        {
                            models.Add(modelInfo);
                        }
                    }
                }

                Logger.LogInformation("Successfully parsed {Count} models from Cerebras API", models.Count);
                return models.Any() ? models : GetFallbackModels();
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Failed to parse Cerebras API response: {Content}", jsonContent);
                return GetFallbackModels();
            }
        }

        /// <summary>
        /// Parses an individual model element from the API response.
        /// </summary>
        /// <param name="modelElement">The JSON element representing a model.</param>
        /// <returns>Model metadata or null if parsing fails.</returns>
        private ModelMetadata? ParseModelElement(JsonElement modelElement)
        {
            try
            {
                if (!modelElement.TryGetProperty("id", out var idElement))
                {
                    return null;
                }

                var modelId = idElement.GetString();
                if (string.IsNullOrEmpty(modelId))
                {
                    return null;
                }

                // Extract additional metadata if available
                var displayName = modelId;
                var description = string.Empty;
                var contextLength = GetContextLengthForModel(modelId);
                
                if (modelElement.TryGetProperty("object", out var objectElement))
                {
                    var objectType = objectElement.GetString();
                    if (objectType != "model")
                    {
                        // Skip non-model objects
                        return null;
                    }
                }

                return new ModelMetadata
                {
                    ModelId = modelId,
                    DisplayName = FormatDisplayName(modelId),
                    Provider = ProviderName,
                    MaxContextTokens = contextLength,
                    MaxOutputTokens = contextLength,
                    Source = ModelDiscoverySource.ProviderApi,
                    LastUpdated = DateTime.UtcNow,
                    Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities
                    {
                        Chat = true,
                        ChatStream = true,
                        Vision = false,
                        FunctionCalling = true,
                        ToolUse = false,
                        Embeddings = false,
                        ImageGeneration = false,
                        VideoGeneration = false,
                        VideoUnderstanding = false
                    },
                    AdditionalMetadata = new Dictionary<string, object>
                    {
                        ["description"] = GetModelDescription(modelId),
                        ["cerebras_optimized"] = true
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse model element");
                return null;
            }
        }

        /// <summary>
        /// Gets the context length for a specific Cerebras model.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <returns>The context length for the model.</returns>
        private static int GetContextLengthForModel(string modelId)
        {
            return modelId.ToLowerInvariant() switch
            {
                var id when id.Contains("llama3.1") => 131072,  // 128K context
                var id when id.Contains("llama-3.3") => 131072, // 128K context  
                var id when id.Contains("llama-4-scout") => 32768, // 32K context
                var id when id.Contains("qwen-3") => 32768,     // 32K context
                var id when id.Contains("deepseek") => 32768,   // 32K context
                _ => 32768 // Default to 32K
            };
        }

        /// <summary>
        /// Formats a model ID into a user-friendly display name.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <returns>A formatted display name.</returns>
        private static string FormatDisplayName(string modelId)
        {
            return modelId switch
            {
                "llama3.1-8b" => "Llama 3.1 8B",
                "llama3.1-70b" => "Llama 3.1 70B",
                "llama-3.3-70b" => "Llama 3.3 70B",
                "llama-4-scout-17b-16e-instruct" => "Llama 4 Scout 17B Instruct",
                "qwen-3-32b" => "Qwen 3 32B",
                "qwen-3-235b-a22b" => "Qwen 3 235B",
                "deepseek-r1-distill-llama-70b" => "DeepSeek R1 Distill Llama 70B",
                _ => modelId
            };
        }

        /// <summary>
        /// Gets a description for a specific model.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <returns>A description of the model.</returns>
        private static string GetModelDescription(string modelId)
        {
            return modelId switch
            {
                var id when id.Contains("llama3.1") => "High-performance Llama 3.1 model optimized for Cerebras hardware",
                var id when id.Contains("llama-3.3") => "Latest Llama 3.3 model with enhanced capabilities",
                var id when id.Contains("llama-4-scout") => "Preview of Llama 4 Scout with advanced reasoning",
                var id when id.Contains("qwen-3") => "Qwen 3 model series with strong multilingual capabilities",
                var id when id.Contains("deepseek") => "DeepSeek R1 distilled model for efficient reasoning",
                _ => "High-performance language model accelerated by Cerebras hardware"
            };
        }

        /// <summary>
        /// Gets fallback models when API discovery is not available.
        /// </summary>
        /// <returns>A list of known Cerebras models.</returns>
        private List<ModelMetadata> GetFallbackModels()
        {
            Logger.LogInformation("Using fallback models for Cerebras provider");
            
            return new List<ModelMetadata>
            {
                new()
                {
                    ModelId = "llama3.1-8b",
                    DisplayName = "Llama 3.1 8B",
                    Provider = ProviderName,
                    MaxContextTokens = 131072,
                    MaxOutputTokens = 131072,
                    Source = ModelDiscoverySource.HardcodedPattern,
                    LastUpdated = DateTime.UtcNow,
                    Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities
                    {
                        Chat = true,
                        ChatStream = true,
                        Vision = false,
                        FunctionCalling = true,
                        ToolUse = false,
                        Embeddings = false,
                        ImageGeneration = false,
                        VideoGeneration = false,
                        VideoUnderstanding = false
                    },
                    AdditionalMetadata = new Dictionary<string, object>
                    {
                        ["description"] = "High-performance Llama 3.1 8B model optimized for Cerebras hardware",
                        ["cerebras_optimized"] = true
                    }
                },
                CreateFallbackMetadata("llama3.1-70b", "Llama 3.1 70B", 131072, "High-performance Llama 3.1 70B model optimized for Cerebras hardware"),
                CreateFallbackMetadata("llama-3.3-70b", "Llama 3.3 70B", 131072, "Latest Llama 3.3 70B model with enhanced capabilities"),
                CreateFallbackMetadata("llama-4-scout-17b-16e-instruct", "Llama 4 Scout 17B Instruct", 32768, "Preview of Llama 4 Scout with advanced reasoning capabilities"),
                CreateFallbackMetadata("qwen-3-32b", "Qwen 3 32B", 32768, "Qwen 3 32B model with strong multilingual capabilities"),
                CreateFallbackMetadata("qwen-3-235b-a22b", "Qwen 3 235B", 32768, "Large-scale Qwen 3 235B model for complex tasks"),
                CreateFallbackMetadata("deepseek-r1-distill-llama-70b", "DeepSeek R1 Distill Llama 70B", 32768, "DeepSeek R1 distilled model for efficient reasoning (Private Preview)")
            };
        }

        /// <summary>
        /// Creates a fallback model metadata entry.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <param name="displayName">The display name.</param>
        /// <param name="contextLength">The context length.</param>
        /// <param name="description">The description.</param>
        /// <returns>A fallback model metadata.</returns>
        private ModelMetadata CreateFallbackMetadata(string modelId, string displayName, int contextLength, string description)
        {
            return new ModelMetadata
            {
                ModelId = modelId,
                DisplayName = displayName,
                Provider = ProviderName,
                MaxContextTokens = contextLength,
                MaxOutputTokens = contextLength,
                Source = ModelDiscoverySource.HardcodedPattern,
                LastUpdated = DateTime.UtcNow,
                Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities
                {
                    Chat = true,
                    ChatStream = true,
                    Vision = false,
                    FunctionCalling = true,
                    ToolUse = false,
                    Embeddings = false,
                    ImageGeneration = false,
                    VideoGeneration = false,
                    VideoUnderstanding = false
                },
                AdditionalMetadata = new Dictionary<string, object>
                {
                    ["description"] = description,
                    ["cerebras_optimized"] = true
                }
            };
        }
    }
}