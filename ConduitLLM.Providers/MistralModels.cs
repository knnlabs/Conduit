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
    /// Provides model discovery capabilities for Mistral AI.
    /// </summary>
    public static class MistralModels
    {
        // Mistral has a models endpoint
        private const string ModelsEndpoint = "https://api.mistral.ai/v1/models";

        /// <summary>
        /// Discovers available models from the Mistral API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Mistral API key. If null, returns empty list.</param>
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
                var apiResponse = JsonSerializer.Deserialize<MistralModelsResponse>(content, new JsonSerializerOptions
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


        private static DiscoveredModel ConvertToDiscoveredModel(MistralModel model)
        {
            var capabilities = InferCapabilitiesFromModel(model.Id, model.MaxTokens ?? 32768);
            
            return new DiscoveredModel
            {
                ModelId = model.Id,
                DisplayName = FormatDisplayName(model.Id),
                Provider = "mistral", // This will be replaced with proper provider by caller
                Capabilities = capabilities,
                Metadata = new Dictionary<string, object>
                {
                    ["created"] = model.Created ?? 0,
                    ["owned_by"] = model.OwnedBy ?? "mistralai",
                    ["object"] = model.Object ?? "model"
                }
            };
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId, int contextLength)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            // Check if it's an embedding model
            if (modelIdLower.Contains("embed"))
            {
                capabilities.Embeddings = true;
                capabilities.Chat = false;
                capabilities.ChatStream = false;
                capabilities.MaxTokens = contextLength;
                return capabilities;
            }

            // All other Mistral models support chat
            capabilities.Chat = true;
            capabilities.ChatStream = true;
            capabilities.MaxTokens = contextLength;
            
            // Mistral models support function calling (as of late 2023)
            capabilities.FunctionCalling = !modelIdLower.Contains("7b"); // Except base 7B model
            capabilities.ToolUse = capabilities.FunctionCalling;
            
            // JSON mode support
            capabilities.JsonMode = modelIdLower.Contains("large") || 
                                  modelIdLower.Contains("medium") ||
                                  modelIdLower.Contains("mixtral");

            // Set output token limits based on model size
            if (modelIdLower.Contains("large"))
            {
                capabilities.MaxOutputTokens = 4096;
            }
            else if (modelIdLower.Contains("medium") || modelIdLower.Contains("8x22b"))
            {
                capabilities.MaxOutputTokens = 2048;
            }
            else
            {
                capabilities.MaxOutputTokens = 1024;
            }

            // Code models have special capabilities
            if (modelIdLower.Contains("codestral"))
            {
                capabilities.MaxOutputTokens = 4096;
                capabilities.FunctionCalling = true;
                capabilities.ToolUse = true;
                capabilities.JsonMode = true;
            }

            return capabilities;
        }

        private static string FormatDisplayName(string modelId)
        {
            // Format model IDs to more readable display names
            var displayName = modelId
                .Replace("-latest", " (Latest)")
                .Replace("-", " ")
                .Replace("open ", "Open ")
                .Replace("mistral", "Mistral")
                .Replace("mixtral", "Mixtral")
                .Replace("codestral", "Codestral");

            // Capitalize first letter of each word
            var words = displayName.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0 && !words[i].Contains("x") && !words[i].StartsWith("("))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }

        private class MistralModelsResponse
        {
            public List<MistralModel> Data { get; set; } = new();
            public string Object { get; set; } = string.Empty;
        }

        private class MistralModel
        {
            public string Id { get; set; } = string.Empty;
            public string Object { get; set; } = string.Empty;
            public long? Created { get; set; }
            public string? OwnedBy { get; set; }
            public int? MaxTokens { get; set; }
        }
    }
}