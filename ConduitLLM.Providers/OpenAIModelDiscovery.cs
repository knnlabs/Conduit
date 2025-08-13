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
    /// Provides model discovery capabilities for OpenAI.
    /// </summary>
    public static class OpenAIModelDiscovery
    {
        private const string ModelsEndpoint = "https://api.openai.com/v1/models";

        /// <summary>
        /// Discovers available models from the OpenAI API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">OpenAI API key. If null, returns empty list.</param>
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
                var apiResponse = JsonSerializer.Deserialize<OpenAIModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null || apiResponse.Data.Count() == 0)
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

        private static DiscoveredModel ConvertToDiscoveredModel(OpenAIModel model)
        {
            var capabilities = InferCapabilities(model);
            
            return new DiscoveredModel
            {
                ModelId = model.Id,
                DisplayName = FormatDisplayName(model.Id),
                Provider = "openai", // This will be replaced with proper provider by caller
                Capabilities = capabilities,
                Metadata = new Dictionary<string, object>
                {
                    ["created"] = model.Created,
                    ["owned_by"] = model.OwnedBy ?? "openai"
                }
            };
        }

        private static ModelCapabilities InferCapabilities(OpenAIModel model)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = model.Id.ToLowerInvariant();

            // GPT models (chat)
            if (modelIdLower.Contains("gpt"))
            {
                capabilities.Chat = true;
                capabilities.ChatStream = true;
                capabilities.FunctionCalling = true;
                capabilities.ToolUse = true;
                capabilities.JsonMode = true;

                // Vision capabilities
                capabilities.Vision = modelIdLower.Contains("vision") || 
                                   modelIdLower.Contains("gpt-4-turbo") || 
                                   modelIdLower.Contains("gpt-4o");

                // Context window sizes
                if (modelIdLower.Contains("gpt-4-turbo") || modelIdLower.Contains("gpt-4o"))
                {
                    capabilities.MaxTokens = 128000;
                    capabilities.MaxOutputTokens = 4096;
                }
                else if (modelIdLower.Contains("gpt-4-32k"))
                {
                    capabilities.MaxTokens = 32768;
                    capabilities.MaxOutputTokens = 4096;
                }
                else if (modelIdLower.Contains("gpt-4"))
                {
                    capabilities.MaxTokens = 8192;
                    capabilities.MaxOutputTokens = 4096;
                }
                else if (modelIdLower.Contains("gpt-3.5-turbo-16k"))
                {
                    capabilities.MaxTokens = 16385;
                    capabilities.MaxOutputTokens = 4096;
                }
                else if (modelIdLower.Contains("gpt-3.5"))
                {
                    capabilities.MaxTokens = 4097;
                    capabilities.MaxOutputTokens = 4096;
                }
            }
            // o1 models (reasoning)
            else if (modelIdLower.Contains("o1"))
            {
                capabilities.Chat = true;
                capabilities.ChatStream = true;
                capabilities.FunctionCalling = false; // o1 models don't support function calling
                capabilities.ToolUse = false;
                capabilities.JsonMode = false;
                capabilities.Vision = false;
                
                if (modelIdLower.Contains("o1-preview"))
                {
                    capabilities.MaxTokens = 128000;
                    capabilities.MaxOutputTokens = 32768;
                }
                else if (modelIdLower.Contains("o1-mini"))
                {
                    capabilities.MaxTokens = 128000;
                    capabilities.MaxOutputTokens = 65536;
                }
            }
            // Embedding models
            else if (modelIdLower.Contains("embedding"))
            {
                capabilities.Embeddings = true;
                capabilities.Chat = false;
                capabilities.ChatStream = false;
                
                if (modelIdLower.Contains("ada-002"))
                {
                    capabilities.MaxTokens = 8191;
                }
                else if (modelIdLower.Contains("3-small"))
                {
                    capabilities.MaxTokens = 8191;
                }
                else if (modelIdLower.Contains("3-large"))
                {
                    capabilities.MaxTokens = 8191;
                }
            }
            // DALL-E models (image generation)
            else if (modelIdLower.Contains("dall-e"))
            {
                capabilities.ImageGeneration = true;
                capabilities.Chat = false;
                capabilities.ChatStream = false;
                
                if (modelIdLower.Contains("dall-e-3"))
                {
                    capabilities.SupportedImageSizes = new List<string> 
                    { 
                        "1024x1024", "1792x1024", "1024x1792" 
                    };
                }
                else if (modelIdLower.Contains("dall-e-2"))
                {
                    capabilities.SupportedImageSizes = new List<string> 
                    { 
                        "256x256", "512x512", "1024x1024" 
                    };
                }
            }
            // Whisper models (audio transcription)
            else if (modelIdLower.Contains("whisper"))
            {
                // Audio transcription capability not in current ModelCapabilities
                capabilities.Chat = false;
                capabilities.ChatStream = false;
            }
            // TTS models (text to speech)
            else if (modelIdLower.Contains("tts"))
            {
                // Text to speech capability not in current ModelCapabilities
                capabilities.Chat = false;
                capabilities.ChatStream = false;
            }
            // Legacy models
            else if (modelIdLower.Contains("davinci") || modelIdLower.Contains("curie") || 
                     modelIdLower.Contains("babbage") || modelIdLower.Contains("ada"))
            {
                // Legacy completion models (deprecated)
                capabilities.Chat = false;
                capabilities.ChatStream = false;
            }

            return capabilities;
        }

        private static string FormatDisplayName(string modelId)
        {
            // Format model IDs to more readable display names
            var displayName = modelId;

            // Handle GPT models
            if (modelId.StartsWith("gpt-"))
            {
                displayName = modelId
                    .Replace("gpt-", "GPT-")
                    .Replace("-turbo", " Turbo")
                    .Replace("-preview", " Preview")
                    .Replace("-16k", " (16K)")
                    .Replace("-32k", " (32K)")
                    .Replace("-0125", " (Jan 2025)")
                    .Replace("-1106", " (Nov 2023)")
                    .Replace("-0613", " (Jun 2023)")
                    .Replace("-0314", " (Mar 2023)")
                    .Replace("-0301", " (Mar 2023)");
            }
            // Handle o1 models
            else if (modelId.StartsWith("o1"))
            {
                displayName = modelId
                    .Replace("o1-", "O1 ")
                    .Replace("-preview", " Preview")
                    .Replace("-mini", " Mini");
            }
            // Handle DALL-E models
            else if (modelId.Contains("dall-e"))
            {
                displayName = modelId.Replace("dall-e-", "DALL-E ");
            }
            // Handle embedding models
            else if (modelId.Contains("embedding"))
            {
                displayName = modelId
                    .Replace("text-embedding-", "Text Embedding ")
                    .Replace("-ada-", " Ada ")
                    .Replace("-", " ");
            }
            // Handle Whisper
            else if (modelId.Contains("whisper"))
            {
                displayName = "Whisper";
            }
            // Handle TTS
            else if (modelId.Contains("tts"))
            {
                displayName = modelId
                    .Replace("tts-", "TTS ")
                    .Replace("-", " ");
            }

            // Capitalize first letter of each word
            var words = displayName.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0 && !words[i].All(char.IsUpper))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }

            return string.Join(" ", words);
        }

        private class OpenAIModelsResponse
        {
            public List<OpenAIModel> Data { get; set; } = new();
        }

        private class OpenAIModel
        {
            public string Id { get; set; } = string.Empty;
            public string Object { get; set; } = string.Empty;
            public long Created { get; set; }
            public string? OwnedBy { get; set; }
        }
    }
}