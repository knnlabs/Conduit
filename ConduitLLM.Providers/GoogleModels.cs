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
    /// Provides model discovery capabilities for Google Gemini.
    /// </summary>
    public static class GoogleModels
    {
        // Google Gemini has a models endpoint
        private const string ModelsEndpoint = "https://generativelanguage.googleapis.com/v1beta/models";

        /// <summary>
        /// Discovers available models from the Google Gemini API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Google API key. If null, returns empty list.</param>
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
                // Google uses API key as a query parameter
                var requestUrl = $"{ModelsEndpoint}?key={apiKey}";
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Add("Accept", "application/json");

                var response = await httpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    // If the models endpoint fails, return known models
                    return GetKnownModels();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<GoogleModelsResponse>(content, new JsonSerializerOptions
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
            // Based on current Google Gemini models
            var knownModels = new List<(string id, string displayName, string description)>
            {
                // Gemini 1.5 models
                ("gemini-1.5-pro", "Gemini 1.5 Pro", "Most capable model for complex tasks"),
                ("gemini-1.5-pro-002", "Gemini 1.5 Pro 002", "Latest version of Gemini 1.5 Pro"),
                ("gemini-1.5-flash", "Gemini 1.5 Flash", "Fast and efficient for high-volume tasks"),
                ("gemini-1.5-flash-002", "Gemini 1.5 Flash 002", "Latest version of Gemini 1.5 Flash"),
                ("gemini-1.5-flash-8b", "Gemini 1.5 Flash 8B", "Smaller, faster variant of Flash"),
                
                // Gemini 1.0 Pro (legacy)
                ("gemini-1.0-pro", "Gemini 1.0 Pro", "Legacy model"),
                
                // Embedding models
                ("text-embedding-004", "Text Embedding 004", "Latest text embedding model"),
                ("embedding-001", "Embedding 001", "Legacy embedding model"),
                
                // Experimental models
                ("gemini-exp-1206", "Gemini Experimental 1206", "Experimental model with enhanced capabilities"),
                ("learnlm-1.5-pro-experimental", "LearnLM 1.5 Pro", "Education-focused experimental model")
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "google",
                Capabilities = InferCapabilitiesFromModelId(model.id),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description
                }
            }).ToList();
        }

        private static DiscoveredModel ConvertToDiscoveredModel(GoogleModel model)
        {
            // Extract model ID from the full name (e.g., "models/gemini-1.5-pro" -> "gemini-1.5-pro")
            var modelId = model.Name.StartsWith("models/") ? model.Name.Substring(7) : model.Name;
            
            var capabilities = InferCapabilitiesFromModel(model);
            
            return new DiscoveredModel
            {
                ModelId = modelId,
                DisplayName = model.DisplayName ?? FormatDisplayName(modelId),
                Provider = "google", // This will be replaced with proper provider by caller
                Capabilities = capabilities,
                Metadata = new Dictionary<string, object>
                {
                    ["name"] = model.Name,
                    ["version"] = model.Version ?? "1",
                    ["description"] = model.Description ?? "",
                    ["supported_generation_methods"] = model.SupportedGenerationMethods ?? new List<string>()
                }
            };
        }

        private static ModelCapabilities InferCapabilitiesFromModel(GoogleModel model)
        {
            var capabilities = new ModelCapabilities();
            
            // Check supported generation methods
            var methods = model.SupportedGenerationMethods ?? new List<string>();
            capabilities.Chat = methods.Contains("generateContent") || methods.Contains("streamGenerateContent");
            capabilities.ChatStream = methods.Contains("streamGenerateContent");
            capabilities.Embeddings = methods.Contains("embedContent");
            
            // Use model-specific information if available
            if (model.InputTokenLimit.HasValue)
                capabilities.MaxTokens = model.InputTokenLimit.Value;
            
            if (model.OutputTokenLimit.HasValue)
                capabilities.MaxOutputTokens = model.OutputTokenLimit.Value;
            
            // Infer additional capabilities from model ID
            var modelId = model.Name?.ToLowerInvariant() ?? "";
            return MergeCapabilities(capabilities, InferCapabilitiesFromModelId(modelId));
        }

        private static ModelCapabilities InferCapabilitiesFromModelId(string modelId)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            if (modelIdLower.Contains("gemini"))
            {
                // All Gemini models support chat
                capabilities.Chat = true;
                capabilities.ChatStream = true;
                
                // All Gemini models support vision (multimodal)
                capabilities.Vision = true;
                capabilities.VideoUnderstanding = modelIdLower.Contains("1.5"); // Gemini 1.5 supports video
                
                // Gemini doesn't support OpenAI-style function calling
                capabilities.FunctionCalling = false;
                capabilities.ToolUse = false;
                capabilities.JsonMode = false;
                
                // Context windows based on model
                if (modelIdLower.Contains("1.5-pro"))
                {
                    capabilities.MaxTokens = 2097152; // 2M context
                    capabilities.MaxOutputTokens = 8192;
                }
                else if (modelIdLower.Contains("1.5-flash"))
                {
                    capabilities.MaxTokens = 1048576; // 1M context
                    capabilities.MaxOutputTokens = 8192;
                }
                else if (modelIdLower.Contains("1.0-pro"))
                {
                    capabilities.MaxTokens = 32768;
                    capabilities.MaxOutputTokens = 8192;
                }
            }
            else if (modelIdLower.Contains("embedding") || modelIdLower.Contains("text-embedding"))
            {
                capabilities.Embeddings = true;
                capabilities.Chat = false;
                capabilities.ChatStream = false;
                capabilities.MaxTokens = 3072; // Typical for embeddings
            }
            else if (modelIdLower.Contains("learnlm"))
            {
                // LearnLM models are chat models optimized for education
                capabilities.Chat = true;
                capabilities.ChatStream = true;
                capabilities.Vision = true;
                capabilities.MaxTokens = 32768;
                capabilities.MaxOutputTokens = 8192;
            }

            return capabilities;
        }

        private static ModelCapabilities MergeCapabilities(ModelCapabilities primary, ModelCapabilities secondary)
        {
            // Merge two capability sets, preferring values from primary
            return new ModelCapabilities
            {
                Chat = primary.Chat || secondary.Chat,
                ChatStream = primary.ChatStream || secondary.ChatStream,
                Embeddings = primary.Embeddings || secondary.Embeddings,
                ImageGeneration = primary.ImageGeneration || secondary.ImageGeneration,
                Vision = primary.Vision || secondary.Vision,
                VideoGeneration = primary.VideoGeneration || secondary.VideoGeneration,
                VideoUnderstanding = primary.VideoUnderstanding || secondary.VideoUnderstanding,
                FunctionCalling = primary.FunctionCalling || secondary.FunctionCalling,
                ToolUse = primary.ToolUse || secondary.ToolUse,
                JsonMode = primary.JsonMode || secondary.JsonMode,
                MaxTokens = primary.MaxTokens ?? secondary.MaxTokens,
                MaxOutputTokens = primary.MaxOutputTokens ?? secondary.MaxOutputTokens,
                SupportedImageSizes = primary.SupportedImageSizes ?? secondary.SupportedImageSizes,
                SupportedVideoResolutions = primary.SupportedVideoResolutions ?? secondary.SupportedVideoResolutions,
                MaxVideoDurationSeconds = primary.MaxVideoDurationSeconds ?? secondary.MaxVideoDurationSeconds
            };
        }

        private static string FormatDisplayName(string modelId)
        {
            // Format model IDs to more readable display names
            var displayName = modelId
                .Replace("gemini-", "Gemini ")
                .Replace("-", " ")
                .Replace("1.5", "1.5")
                .Replace("1.0", "1.0")
                .Replace("pro", "Pro")
                .Replace("flash", "Flash")
                .Replace("8b", "8B")
                .Replace("exp", "Experimental")
                .Replace("learnlm", "LearnLM")
                .Replace("text embedding", "Text Embedding");

            // Capitalize first letter of each word
            var words = displayName.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0 && !words[i].Any(char.IsDigit) && !words[i].All(char.IsUpper))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }

        private class GoogleModelsResponse
        {
            public List<GoogleModel> Models { get; set; } = new();
        }

        private class GoogleModel
        {
            public string Name { get; set; } = string.Empty;
            public string? Version { get; set; }
            public string? DisplayName { get; set; }
            public string? Description { get; set; }
            public int? InputTokenLimit { get; set; }
            public int? OutputTokenLimit { get; set; }
            public List<string>? SupportedGenerationMethods { get; set; }
            public double? Temperature { get; set; }
            public double? TopP { get; set; }
            public int? TopK { get; set; }
        }
    }
}