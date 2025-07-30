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
    /// Provides model discovery capabilities for Fireworks AI.
    /// </summary>
    public static class FireworksModels
    {
        // Fireworks has a models endpoint
        private const string ModelsEndpoint = "https://api.fireworks.ai/inference/v1/models";

        /// <summary>
        /// Discovers available models from the Fireworks API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Fireworks API key. If null, returns empty list.</param>
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
                    // API call failed, return empty list
                    return new List<DiscoveredModel>();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<FireworksModelsResponse>(content, new JsonSerializerOptions
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
            catch (Exception)
            {
                // Any error during discovery returns empty list
                return new List<DiscoveredModel>();
            }
        }


        private static DiscoveredModel ConvertToDiscoveredModel(FireworksModel model)
        {
            var modelType = DetermineModelType(model.Id);
            var capabilities = InferCapabilitiesFromModel(model.Id, modelType);
            
            return new DiscoveredModel
            {
                ModelId = model.Id,
                DisplayName = FormatDisplayName(model.Id),
                Provider = "fireworks", // This will be replaced with proper provider by caller
                Capabilities = capabilities,
                Metadata = new Dictionary<string, object>
                {
                    ["created"] = model.Created ?? 0,
                    ["object"] = model.Object ?? "model"
                }
            };
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId, ModelType type)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            switch (type)
            {
                case ModelType.Chat:
                    capabilities.Chat = true;
                    capabilities.ChatStream = true;
                    
                    // Vision capabilities
                    capabilities.Vision = modelIdLower.Contains("vision") || 
                                       modelIdLower.Contains("llava") ||
                                       modelIdLower.Contains("11b-vision");
                    
                    // Function calling for specific models
                    capabilities.FunctionCalling = modelIdLower.Contains("firefunction") ||
                                                 modelIdLower.Contains("mixtral") ||
                                                 modelIdLower.Contains("llama-v3");
                    capabilities.ToolUse = capabilities.FunctionCalling;
                    
                    // JSON mode support
                    capabilities.JsonMode = modelIdLower.Contains("firefunction") ||
                                          modelIdLower.Contains("llama-v3p1") ||
                                          modelIdLower.Contains("mixtral");
                    
                    // Context windows
                    if (modelIdLower.Contains("405b"))
                    {
                        capabilities.MaxTokens = 131072;
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("70b") || modelIdLower.Contains("72b"))
                    {
                        capabilities.MaxTokens = modelIdLower.Contains("llama-v3p1") ? 131072 : 32768;
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("mixtral-8x22b"))
                    {
                        capabilities.MaxTokens = 65536;
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("phi-3-vision"))
                    {
                        capabilities.MaxTokens = 128000;
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("deepseek"))
                    {
                        capabilities.MaxTokens = 16384;
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else
                    {
                        capabilities.MaxTokens = 8192;
                        capabilities.MaxOutputTokens = 2048;
                    }
                    break;

                case ModelType.Embedding:
                    capabilities.Embeddings = true;
                    capabilities.Chat = false;
                    capabilities.ChatStream = false;
                    capabilities.MaxTokens = modelIdLower.Contains("e5-mistral") ? 32768 : 512;
                    break;

                case ModelType.ImageGeneration:
                    capabilities.ImageGeneration = true;
                    capabilities.SupportedImageSizes = new List<string> 
                    { 
                        "512x512", "768x768", "1024x1024", "1024x768", "768x1024" 
                    };
                    if (modelIdLower.Contains("xl"))
                    {
                        capabilities.SupportedImageSizes.Add("1536x1536");
                        capabilities.SupportedImageSizes.Add("2048x2048");
                    }
                    break;
            }

            return capabilities;
        }

        private static ModelType DetermineModelType(string modelId)
        {
            var idLower = modelId.ToLowerInvariant();
            
            if (idLower.Contains("embed") || idLower.Contains("bge") || idLower.Contains("e5-"))
                return ModelType.Embedding;
            if (idLower.Contains("stable-diffusion") || idLower.Contains("playground"))
                return ModelType.ImageGeneration;
            
            return ModelType.Chat;
        }

        private static string FormatDisplayName(string modelId)
        {
            // Extract the model name from the full path
            var parts = modelId.Split('/');
            var modelName = parts.Length > 0 ? parts[parts.Length - 1] : modelId;
            
            // Format the name
            return modelName
                .Replace("-instruct", " Instruct")
                .Replace("-vision", " Vision")
                .Replace("-", " ")
                .Replace("llama v3p2", "Llama 3.2")
                .Replace("llama v3p1", "Llama 3.1")
                .Replace("llama v3", "Llama 3")
                .Replace("mixtral", "Mixtral")
                .Replace("qwen2p5", "Qwen 2.5")
                .Replace("qwen2", "Qwen 2")
                .Replace("firefunction", "FireFunction")
                .Replace("deepseek", "DeepSeek")
                .Replace("gemma2", "Gemma 2")
                .Replace("phi 3", "Phi-3");
        }

        private class FireworksModelsResponse
        {
            public List<FireworksModel> Data { get; set; } = new();
            public string Object { get; set; } = string.Empty;
        }

        private class FireworksModel
        {
            public string Id { get; set; } = string.Empty;
            public string Object { get; set; } = string.Empty;
            public long? Created { get; set; }
        }

        private enum ModelType
        {
            Chat,
            Embedding,
            ImageGeneration
        }
    }
}