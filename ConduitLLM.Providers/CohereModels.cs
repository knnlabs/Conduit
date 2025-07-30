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
    /// Provides model discovery capabilities for Cohere.
    /// </summary>
    public static class CohereModels
    {
        // Cohere has a models endpoint
        private const string ModelsEndpoint = "https://api.cohere.ai/v1/models";

        /// <summary>
        /// Discovers available models from the Cohere API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Cohere API key. If null, returns empty list.</param>
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
                request.Headers.Add("X-Client-Name", "conduit-llm");

                var response = await httpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    // If the models endpoint fails, return known models
                    return GetKnownModels();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<CohereModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Models == null || apiResponse.Models.Count == 0)
                {
                    return GetKnownModels();
                }

                return apiResponse.Models
                    .Where(model => !string.IsNullOrEmpty(model.Name))
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
            // Based on current Cohere models
            var knownModels = new List<(string id, string displayName, string description, ModelType type, int contextLength)>
            {
                // Command models (chat/generation)
                ("command-r-plus", "Command R+", "Most capable model for complex RAG workflows", ModelType.Chat, 128000),
                ("command-r", "Command R", "Efficient model for RAG and tool use", ModelType.Chat, 128000),
                ("command", "Command", "Text generation model", ModelType.Chat, 4096),
                ("command-light", "Command Light", "Lightweight text generation", ModelType.Chat, 4096),
                ("command-nightly", "Command Nightly", "Experimental features", ModelType.Chat, 8192),
                
                // Embedding models
                ("embed-english-v3.0", "Embed English v3.0", "Latest English embedding model", ModelType.Embedding, 512),
                ("embed-multilingual-v3.0", "Embed Multilingual v3.0", "Multilingual embedding model", ModelType.Embedding, 512),
                ("embed-english-light-v3.0", "Embed English Light v3.0", "Lightweight English embeddings", ModelType.Embedding, 512),
                ("embed-multilingual-light-v3.0", "Embed Multilingual Light v3.0", "Lightweight multilingual embeddings", ModelType.Embedding, 512),
                ("embed-english-v2.0", "Embed English v2.0", "Previous gen English embeddings", ModelType.Embedding, 512),
                ("embed-multilingual-v2.0", "Embed Multilingual v2.0", "Previous gen multilingual embeddings", ModelType.Embedding, 512),
                
                // Rerank models
                ("rerank-english-v3.0", "Rerank English v3.0", "Document reranking model", ModelType.Rerank, 4096),
                ("rerank-multilingual-v3.0", "Rerank Multilingual v3.0", "Multilingual reranking", ModelType.Rerank, 4096),
                ("rerank-english-v2.0", "Rerank English v2.0", "Previous reranking model", ModelType.Rerank, 4096),
                ("rerank-multilingual-v2.0", "Rerank Multilingual v2.0", "Previous multilingual reranking", ModelType.Rerank, 4096)
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "cohere",
                Capabilities = InferCapabilitiesFromModel(model.id, model.type, model.contextLength),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description,
                    ["type"] = model.type.ToString(),
                    ["context_length"] = model.contextLength
                }
            }).ToList();
        }

        private static DiscoveredModel ConvertToDiscoveredModel(CohereModel model)
        {
            var modelType = DetermineModelType(model.Name);
            var contextLength = model.MaxTokens ?? (modelType == ModelType.Embedding ? 512 : 4096);
            var capabilities = InferCapabilitiesFromModel(model.Name, modelType, contextLength);
            
            return new DiscoveredModel
            {
                ModelId = model.Name,
                DisplayName = FormatDisplayName(model.Name),
                Provider = "cohere", // This will be replaced with proper provider by caller
                Capabilities = capabilities,
                Metadata = new Dictionary<string, object>
                {
                    ["endpoints"] = model.Endpoints ?? new List<string>(),
                    ["finetuned"] = model.Finetuned ?? false,
                    ["context_length"] = contextLength
                }
            };
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId, ModelType type, int contextLength)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            switch (type)
            {
                case ModelType.Chat:
                    capabilities.Chat = true;
                    capabilities.ChatStream = true;
                    capabilities.MaxTokens = contextLength;
                    
                    // Command R models support tool use
                    capabilities.ToolUse = modelIdLower.Contains("command-r");
                    capabilities.FunctionCalling = capabilities.ToolUse;
                    
                    // JSON mode support for newer models
                    capabilities.JsonMode = modelIdLower.Contains("command-r") || 
                                          modelIdLower.Contains("nightly");
                    
                    // Output limits
                    if (modelIdLower.Contains("plus"))
                    {
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("command-r"))
                    {
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else
                    {
                        capabilities.MaxOutputTokens = 2048;
                    }
                    break;

                case ModelType.Embedding:
                    capabilities.Embeddings = true;
                    capabilities.Chat = false;
                    capabilities.ChatStream = false;
                    capabilities.MaxTokens = contextLength;
                    break;

                case ModelType.Rerank:
                    // Reranking is a specialized capability not in standard interface
                    capabilities.Chat = false;
                    capabilities.ChatStream = false;
                    capabilities.Embeddings = false;
                    capabilities.MaxTokens = contextLength;
                    break;
            }

            return capabilities;
        }

        private static ModelType DetermineModelType(string modelName)
        {
            var nameLower = modelName.ToLowerInvariant();
            
            if (nameLower.Contains("embed"))
                return ModelType.Embedding;
            if (nameLower.Contains("rerank"))
                return ModelType.Rerank;
            
            return ModelType.Chat;
        }

        private static string FormatDisplayName(string modelId)
        {
            // Format model IDs to more readable display names
            var displayName = modelId
                .Replace("-", " ")
                .Replace("command r plus", "Command R+")
                .Replace("command r", "Command R")
                .Replace("command", "Command")
                .Replace("embed", "Embed")
                .Replace("rerank", "Rerank")
                .Replace("v3.0", "v3.0")
                .Replace("v2.0", "v2.0");

            // Capitalize first letter of each word
            var words = displayName.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0 && !words[i].StartsWith("v") && 
                    !words[i].Equals("R", StringComparison.OrdinalIgnoreCase) &&
                    !words[i].Equals("R+", StringComparison.OrdinalIgnoreCase))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }

        private class CohereModelsResponse
        {
            public List<CohereModel> Models { get; set; } = new();
        }

        private class CohereModel
        {
            public string Name { get; set; } = string.Empty;
            public List<string>? Endpoints { get; set; }
            public bool? Finetuned { get; set; }
            public int? MaxTokens { get; set; }
            public string? Description { get; set; }
        }

        private enum ModelType
        {
            Chat,
            Embedding,
            Rerank
        }
    }
}