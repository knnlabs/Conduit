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
                    // If the models endpoint fails, return known models
                    return GetKnownModels();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<FireworksModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
                {
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
            // Based on Fireworks AI's popular models
            var knownModels = new List<(string id, string displayName, string description, ModelType type)>
            {
                // Llama models
                ("accounts/fireworks/models/llama-v3p2-3b-instruct", "Llama 3.2 3B", "Fast, efficient Llama model", ModelType.Chat),
                ("accounts/fireworks/models/llama-v3p2-11b-vision-instruct", "Llama 3.2 11B Vision", "Multimodal Llama model", ModelType.Chat),
                ("accounts/fireworks/models/llama-v3p1-405b-instruct", "Llama 3.1 405B", "Most capable Llama model", ModelType.Chat),
                ("accounts/fireworks/models/llama-v3p1-70b-instruct", "Llama 3.1 70B", "Large Llama model", ModelType.Chat),
                ("accounts/fireworks/models/llama-v3p1-8b-instruct", "Llama 3.1 8B", "Efficient Llama model", ModelType.Chat),
                
                // Mixtral models
                ("accounts/fireworks/models/mixtral-8x7b-instruct", "Mixtral 8x7B", "MoE model for efficiency", ModelType.Chat),
                ("accounts/fireworks/models/mixtral-8x22b-instruct", "Mixtral 8x22B", "Large MoE model", ModelType.Chat),
                
                // Qwen models
                ("accounts/fireworks/models/qwen2-72b-instruct", "Qwen2 72B", "Alibaba's large model", ModelType.Chat),
                ("accounts/fireworks/models/qwen2p5-72b-instruct", "Qwen2.5 72B", "Latest Qwen model", ModelType.Chat),
                
                // Other language models
                ("accounts/fireworks/models/deepseek-coder-v2-instruct", "DeepSeek Coder v2", "Code generation specialist", ModelType.Chat),
                ("accounts/fireworks/models/yi-large", "Yi Large", "01.AI's large model", ModelType.Chat),
                ("accounts/fireworks/models/gemma2-9b-it", "Gemma 2 9B", "Google's open model", ModelType.Chat),
                ("accounts/fireworks/models/phi-3-vision-128k-instruct", "Phi-3 Vision", "Microsoft's vision model", ModelType.Chat),
                
                // Function calling models
                ("accounts/fireworks/models/firefunction-v2", "FireFunction v2", "Optimized for function calling", ModelType.Chat),
                ("accounts/fireworks/models/firefunction-v1", "FireFunction v1", "Function calling model", ModelType.Chat),
                
                // Embedding models
                ("accounts/fireworks/models/nomic-embed-text-v1.5", "Nomic Embed v1.5", "Text embedding model", ModelType.Embedding),
                ("accounts/fireworks/models/e5-mistral-7b-instruct", "E5 Mistral 7B", "Instruction-tuned embeddings", ModelType.Embedding),
                ("accounts/fireworks/models/bge-base-en-v1-5", "BGE Base EN", "English embeddings", ModelType.Embedding),
                
                // Image generation models
                ("accounts/fireworks/models/stable-diffusion-xl-1024-v1-0", "SDXL 1.0", "High-res image generation", ModelType.ImageGeneration),
                ("accounts/fireworks/models/stable-diffusion-xl-lightning", "SDXL Lightning", "Fast image generation", ModelType.ImageGeneration),
                ("accounts/fireworks/models/playground-v2-1024px-aesthetic", "Playground v2", "Aesthetic image generation", ModelType.ImageGeneration),
                
                // Japanese models
                ("accounts/fireworks/models/japanese-llava-v1.6-mistral-7b", "Japanese LLaVA", "Japanese vision-language model", ModelType.Chat),
                ("accounts/fireworks/models/llama-v3-70b-instruct-hf-japanese", "Llama 3 70B Japanese", "Japanese language model", ModelType.Chat)
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "fireworks",
                Capabilities = InferCapabilitiesFromModel(model.id, model.type),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description,
                    ["type"] = model.type.ToString(),
                    ["deployment_note"] = "Optimized for fast inference"
                }
            }).ToList();
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