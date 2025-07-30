using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Provides model discovery capabilities for OpenRouter.
    /// </summary>
    public static class OpenRouterModels
    {
        private const string ModelsEndpoint = "https://openrouter.ai/api/v1/models";

        /// <summary>
        /// Discovers available models from the OpenRouter API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">OpenRouter API key. If null, returns empty list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models with their capabilities.</returns>
        public static async Task<List<DiscoveredModel>> DiscoverAsync(
            HttpClient httpClient, 
            string? apiKey,
            CancellationToken cancellationToken = default)
        {
            // OpenRouter allows discovery without API key for public models
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                
                // Add API key if provided
                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");
                }
                request.Headers.Add("Accept", "application/json");

                var response = await httpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    // API call failed, return empty list
                    return new List<DiscoveredModel>();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<OpenRouterModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
                {
                    return new List<DiscoveredModel>();
                }

                return apiResponse.Data
                    .Where(model => !string.IsNullOrEmpty(model.Id))
                    .Select(ConvertToDiscoveredModel)
                    .ToList();
            }
            catch
            {
                // Any error during discovery returns empty list
                return new List<DiscoveredModel>();
            }
        }

        private static DiscoveredModel ConvertToDiscoveredModel(OpenRouterModel model)
        {
            var capabilities = InferCapabilities(model);
            
            // Add "openrouter/" prefix to prevent naming collisions
            var modelId = model.Id.StartsWith("openrouter/") ? model.Id : $"openrouter/{model.Id}";
            
            return new DiscoveredModel
            {
                ModelId = modelId,
                DisplayName = !string.IsNullOrEmpty(model.Name) ? model.Name : FormatDisplayName(model.Id),
                Provider = "openrouter",
                Capabilities = capabilities
            };
        }

        private static ModelCapabilities InferCapabilities(OpenRouterModel model)
        {
            var capabilities = new ModelCapabilities();

            // Use OpenRouter's explicit capability flags
            capabilities.Chat = true; // All OpenRouter models support chat
            capabilities.ChatStream = true; // All support streaming
            
            // Map from OpenRouter's architecture info
            if (model.Architecture != null)
            {
                capabilities.Vision = model.Architecture.Modality?.Contains("multimodal") == true ||
                                    model.Architecture.Modality?.Contains("vision") == true;
                
                capabilities.ImageGeneration = model.Architecture.Modality?.Contains("image") == true &&
                                             model.Architecture.TokenizerType?.Contains("generator") == true;
                
                capabilities.VideoGeneration = model.Architecture.Modality?.Contains("video") == true;
                
                capabilities.Embeddings = model.Architecture.Modality?.Contains("embeddings") == true;
            }

            // Tool support
            capabilities.FunctionCalling = model.Architecture?.ToolUse == true;
            capabilities.ToolUse = model.Architecture?.ToolUse == true;
            
            // JSON mode support
            capabilities.JsonMode = model.Architecture?.JsonMode == true;

            // Context and output limits
            capabilities.MaxTokens = model.ContextLength ?? 4096;
            capabilities.MaxOutputTokens = model.MaxOutputTokens ?? 4096;

            return capabilities;
        }

        private static string FormatDisplayName(string modelId)
        {
            // OpenRouter IDs are in format "provider/model-name"
            // Return as-is since they're already descriptive
            return modelId;
        }

        private class OpenRouterModelsResponse
        {
            public List<OpenRouterModel> Data { get; set; } = new();
        }

        private class OpenRouterModel
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; }
            public int? ContextLength { get; set; }
            public int? MaxOutputTokens { get; set; }
            public decimal? PricingInput { get; set; }
            public decimal? PricingOutput { get; set; }
            public OpenRouterArchitecture? Architecture { get; set; }
        }

        private class OpenRouterArchitecture
        {
            public string? Modality { get; set; }
            public string? TokenizerType { get; set; }
            public bool? ToolUse { get; set; }
            public bool? JsonMode { get; set; }
        }
    }
}