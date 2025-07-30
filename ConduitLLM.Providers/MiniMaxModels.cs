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
    /// Provides model discovery capabilities for MiniMax.
    /// </summary>
    public static class MiniMaxModels
    {
        // MiniMax has a models endpoint
        private const string ModelsEndpoint = "https://api.minimax.chat/v1/models";

        /// <summary>
        /// Discovers available models from the MiniMax API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">MiniMax API key. If null, returns empty list.</param>
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
                var apiResponse = JsonSerializer.Deserialize<MiniMaxModelsResponse>(content, new JsonSerializerOptions
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


        private static DiscoveredModel ConvertToDiscoveredModel(MiniMaxModel model)
        {
            var capabilities = InferCapabilitiesFromModel(model.Id, DetermineModelType(model));
            
            return new DiscoveredModel
            {
                ModelId = model.Id,
                DisplayName = model.Name ?? FormatDisplayName(model.Id),
                Provider = "minimax", // This will be replaced with proper provider by caller
                Capabilities = capabilities,
                Metadata = new Dictionary<string, object>
                {
                    ["created"] = model.Created ?? 0,
                    ["owned_by"] = model.OwnedBy ?? "minimax",
                    ["type"] = DetermineModelType(model).ToString()
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
                    capabilities.Vision = true; // All ABAB models support vision
                    capabilities.FunctionCalling = false;
                    capabilities.ToolUse = false;
                    capabilities.JsonMode = false;
                    
                    // Context windows based on model
                    if (modelIdLower.Contains("6.5"))
                    {
                        capabilities.MaxTokens = 245760; // 245K context
                        capabilities.MaxOutputTokens = 8192;
                    }
                    else
                    {
                        capabilities.MaxTokens = 32768; // Default for older models
                        capabilities.MaxOutputTokens = 4096;
                    }
                    break;

                case ModelType.Image:
                    capabilities.ImageGeneration = true;
                    capabilities.SupportedImageSizes = new List<string> 
                    { 
                        "1:1", "16:9", "9:16", "4:3", "3:4", 
                        "2.35:1", "1:2.35", "21:9", "9:21" 
                    };
                    break;

                case ModelType.Video:
                    capabilities.VideoGeneration = true;
                    capabilities.SupportedVideoResolutions = new List<string> 
                    { 
                        "720x480", "1280x720", "1920x1080", "720x1280", "1080x1920" 
                    };
                    capabilities.MaxVideoDurationSeconds = 6;
                    break;

                case ModelType.Audio:
                    // Text-to-speech capabilities
                    capabilities.Chat = false;
                    capabilities.ChatStream = false;
                    // Note: TTS capabilities not fully represented in current interface
                    break;

                case ModelType.Embedding:
                    capabilities.Embeddings = true;
                    capabilities.MaxTokens = 8192; // Typical for embeddings
                    break;
            }

            return capabilities;
        }

        private static ModelType DetermineModelType(MiniMaxModel model)
        {
            var idLower = model.Id.ToLowerInvariant();
            
            if (idLower.Contains("chat"))
                return ModelType.Chat;
            if (idLower.Contains("image"))
                return ModelType.Image;
            if (idLower.Contains("video"))
                return ModelType.Video;
            if (idLower.Contains("speech"))
                return ModelType.Audio;
            if (idLower.Contains("embo"))
                return ModelType.Embedding;
            
            // Default to chat if type cannot be determined
            return ModelType.Chat;
        }

        private static string FormatDisplayName(string modelId)
        {
            // Format model IDs to more readable display names
            var displayName = modelId
                .Replace("-", " ")
                .Replace("abab", "ABAB")
                .Replace("embo", "Embo")
                .Replace("image", "Image")
                .Replace("video", "Video")
                .Replace("speech", "Speech");

            // Capitalize words
            var words = displayName.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0 && !words[i].Any(char.IsDigit) && 
                    !words[i].Equals("ABAB", StringComparison.OrdinalIgnoreCase))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }

        private class MiniMaxModelsResponse
        {
            public List<MiniMaxModel> Data { get; set; } = new();
        }

        private class MiniMaxModel
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; }
            public long? Created { get; set; }
            public string? OwnedBy { get; set; }
            public string? Object { get; set; }
        }

        private enum ModelType
        {
            Chat,
            Image,
            Video,
            Audio,
            Embedding
        }
    }
}