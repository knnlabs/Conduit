using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Provides model discovery capabilities for Cerebras.
    /// </summary>
    public static class CerebrasModels
    {
        private const string ModelsEndpoint = "https://api.cerebras.ai/v1/models";

        /// <summary>
        /// Discovers available models from the Cerebras API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Cerebras API key. If null, returns empty list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models with their capabilities.</returns>
        public static async Task<List<DiscoveredModel>> DiscoverAsync(
            HttpClient httpClient, 
            string? apiKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return new List<DiscoveredModel>();
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Headers.Add("Accept", "application/json");

                var response = await httpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    // If the models endpoint is not available or returns an error,
                    // fall back to our known models list
                    return GetKnownModels();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<CerebrasModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
                {
                    // If API returns empty, use known models
                    return GetKnownModels();
                }

                return apiResponse.Data
                    .Where(model => !string.IsNullOrEmpty(model.Id))
                    .Select(ConvertToDiscoveredModel)
                    .ToList();
            }
            catch (Exception)
            {
                // Any error during discovery returns known models list
                return GetKnownModels();
            }
        }

        private static List<DiscoveredModel> GetKnownModels()
        {
            // Based on the models listed in CerebrasClient
            var knownModels = new List<(string id, string displayName)>
            {
                // Llama 3.1 models
                ("llama3.1-8b", "Llama 3.1 8B"),
                ("llama3.1-70b", "Llama 3.1 70B"),
                
                // Llama 3.3 models
                ("llama-3.3-70b", "Llama 3.3 70B"),
                
                // Llama 4 Scout models
                ("llama-4-scout-17b-16e-instruct", "Llama 4 Scout 17B Instruct"),
                
                // Qwen 3 models
                ("qwen-3-32b", "Qwen 3 32B"),
                ("qwen-3-235b-a22b", "Qwen 3 235B"),
                
                // DeepSeek models (private preview)
                ("deepseek-r1-distill-llama-70b", "DeepSeek R1 Distill Llama 70B")
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "cerebras",
                Capabilities = InferCapabilitiesFromModelId(model.id)
            }).ToList();
        }

        private static DiscoveredModel ConvertToDiscoveredModel(CerebrasModel model)
        {
            var capabilities = InferCapabilitiesFromModelId(model.Id);
            
            return new DiscoveredModel
            {
                ModelId = model.Id,
                DisplayName = FormatDisplayName(model.Id),
                Provider = "cerebras", // This will be replaced with proper provider by caller
                Capabilities = capabilities,
                Metadata = new Dictionary<string, object>
                {
                    ["created"] = model.Created,
                    ["owned_by"] = model.OwnedBy ?? "cerebras"
                }
            };
        }

        private static ModelCapabilities InferCapabilitiesFromModelId(string modelId)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            // All Cerebras models support chat
            capabilities.Chat = true;
            capabilities.ChatStream = true;
            
            // Cerebras models are optimized for speed, not supporting advanced features
            capabilities.FunctionCalling = false;
            capabilities.ToolUse = false;
            capabilities.JsonMode = false;
            capabilities.Vision = false;
            capabilities.VideoGeneration = false;
            capabilities.VideoUnderstanding = false;
            capabilities.ImageGeneration = false;
            capabilities.Embeddings = false;

            // Set context window based on model
            if (modelIdLower.Contains("llama"))
            {
                if (modelIdLower.Contains("8b"))
                {
                    capabilities.MaxTokens = 128000; // Llama 3.1 8B context
                    capabilities.MaxOutputTokens = 8192;
                }
                else if (modelIdLower.Contains("70b"))
                {
                    capabilities.MaxTokens = 128000; // Llama 3.1/3.3 70B context
                    capabilities.MaxOutputTokens = 8192;
                }
                else if (modelIdLower.Contains("17b"))
                {
                    capabilities.MaxTokens = 32768; // Llama 4 Scout context
                    capabilities.MaxOutputTokens = 4096;
                }
            }
            else if (modelIdLower.Contains("qwen"))
            {
                if (modelIdLower.Contains("32b"))
                {
                    capabilities.MaxTokens = 131072; // Qwen 3 32B context
                    capabilities.MaxOutputTokens = 8192;
                }
                else if (modelIdLower.Contains("235b"))
                {
                    capabilities.MaxTokens = 131072; // Qwen 3 235B context
                    capabilities.MaxOutputTokens = 8192;
                }
            }
            else if (modelIdLower.Contains("deepseek"))
            {
                capabilities.MaxTokens = 128000; // DeepSeek context
                capabilities.MaxOutputTokens = 8192;
            }
            else
            {
                // Default context for unknown models
                capabilities.MaxTokens = 8192;
                capabilities.MaxOutputTokens = 4096;
            }

            return capabilities;
        }

        private static string FormatDisplayName(string modelId)
        {
            // Format model IDs to more readable display names
            var displayName = modelId;

            // Handle Llama models
            if (modelId.Contains("llama"))
            {
                displayName = modelId
                    .Replace("llama3.1-", "Llama 3.1 ")
                    .Replace("llama-3.3-", "Llama 3.3 ")
                    .Replace("llama-4-scout-", "Llama 4 Scout ")
                    .Replace("-16e-instruct", " Instruct")
                    .Replace("8b", "8B")
                    .Replace("70b", "70B")
                    .Replace("17b", "17B");
            }
            // Handle Qwen models
            else if (modelId.Contains("qwen"))
            {
                displayName = modelId
                    .Replace("qwen-3-", "Qwen 3 ")
                    .Replace("32b", "32B")
                    .Replace("235b-a22b", "235B");
            }
            // Handle DeepSeek models
            else if (modelId.Contains("deepseek"))
            {
                displayName = modelId
                    .Replace("deepseek-r1-distill-llama-", "DeepSeek R1 Distill Llama ")
                    .Replace("70b", "70B");
            }

            return displayName;
        }

        private class CerebrasModelsResponse
        {
            public List<CerebrasModel> Data { get; set; } = new();
        }

        private class CerebrasModel
        {
            public string Id { get; set; } = string.Empty;
            public string Object { get; set; } = string.Empty;
            public long Created { get; set; }
            public string? OwnedBy { get; set; }
        }
    }
}