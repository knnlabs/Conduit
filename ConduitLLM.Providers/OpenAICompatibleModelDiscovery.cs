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
    /// Provides model discovery capabilities for OpenAI-compatible providers.
    /// </summary>
    public static class OpenAICompatibleModelDiscovery
    {
        /// <summary>
        /// Discovers available models from an OpenAI-compatible API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">API key. If null, returns empty list.</param>
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
                // Try to call the standard OpenAI models endpoint
                // Note: Base URL should be configured in httpClient
                var request = new HttpRequestMessage(HttpMethod.Get, "/v1/models");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Headers.Add("Accept", "application/json");

                var response = await httpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    // If the models endpoint fails, return generic models
                    return GetGenericModels();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<OpenAIModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null || apiResponse.Data.Count() == 0)
                {
                    return GetGenericModels();
                }

                return apiResponse.Data
                    .Where(model => !string.IsNullOrEmpty(model.Id))
                    .Select(ConvertToDiscoveredModel)
                    .ToList();
            }
            catch (Exception)
            {
                // Any error during discovery returns generic models
                return GetGenericModels();
            }
        }

        private static List<DiscoveredModel> GetGenericModels()
        {
            // Generic models that OpenAI-compatible providers might support
            var knownModels = new List<(string id, string displayName, string description)>
            {
                // Common model patterns
                ("gpt-4", "GPT-4", "Advanced language model"),
                ("gpt-3.5-turbo", "GPT-3.5 Turbo", "Fast, efficient model"),
                ("text-davinci-003", "Davinci", "Powerful completion model"),
                ("text-embedding-ada-002", "Ada Embeddings", "Text embedding model"),
                ("custom-model", "Custom Model", "Provider-specific model")
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "openaicompatible",
                Capabilities = InferCapabilitiesFromModel(model.id),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description,
                    ["compatibility"] = "OpenAI API v1"
                }
            }).ToList();
        }

        private static DiscoveredModel ConvertToDiscoveredModel(OpenAIModel model)
        {
            var capabilities = InferCapabilitiesFromModel(model.Id);
            
            return new DiscoveredModel
            {
                ModelId = model.Id,
                DisplayName = FormatDisplayName(model.Id),
                Provider = "openaicompatible",
                Capabilities = capabilities,
                Metadata = new Dictionary<string, object>
                {
                    ["created"] = model.Created ?? 0,
                    ["owned_by"] = model.OwnedBy ?? "custom"
                }
            };
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            // Embedding models
            if (modelIdLower.Contains("embedding"))
            {
                capabilities.Embeddings = true;
                capabilities.Chat = false;
                capabilities.ChatStream = false;
                capabilities.MaxTokens = 8191;
                return capabilities;
            }

            // Image generation models
            if (modelIdLower.Contains("dall-e") || modelIdLower.Contains("image"))
            {
                capabilities.ImageGeneration = true;
                capabilities.SupportedImageSizes = new List<string> { "512x512", "1024x1024" };
                return capabilities;
            }

            // Default to chat model capabilities
            capabilities.Chat = true;
            capabilities.ChatStream = true;
            
            // Assume newer models support function calling
            capabilities.FunctionCalling = modelIdLower.Contains("gpt-4") || 
                                         modelIdLower.Contains("gpt-3.5-turbo") ||
                                         modelIdLower.Contains("turbo");
            capabilities.ToolUse = capabilities.FunctionCalling;
            capabilities.JsonMode = capabilities.FunctionCalling;

            // Context windows (conservative estimates)
            if (modelIdLower.Contains("gpt-4"))
            {
                capabilities.MaxTokens = 8192;
                capabilities.MaxOutputTokens = 4096;
            }
            else if (modelIdLower.Contains("32k"))
            {
                capabilities.MaxTokens = 32768;
                capabilities.MaxOutputTokens = 4096;
            }
            else if (modelIdLower.Contains("16k"))
            {
                capabilities.MaxTokens = 16384;
                capabilities.MaxOutputTokens = 4096;
            }
            else
            {
                capabilities.MaxTokens = 4096;
                capabilities.MaxOutputTokens = 2048;
            }

            return capabilities;
        }

        private static string FormatDisplayName(string modelId)
        {
            return modelId
                .Replace("-", " ")
                .Replace("gpt", "GPT")
                .Replace("turbo", "Turbo")
                .Replace("davinci", "Davinci")
                .Replace("ada", "Ada")
                .Replace("embedding", "Embedding");
        }

        private class OpenAIModelsResponse
        {
            public List<OpenAIModel> Data { get; set; } = new();
        }

        private class OpenAIModel
        {
            public string Id { get; set; } = string.Empty;
            public long? Created { get; set; }
            public string? OwnedBy { get; set; }
        }
    }
}