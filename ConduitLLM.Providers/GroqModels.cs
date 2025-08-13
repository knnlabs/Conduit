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
    /// Provides model discovery capabilities for Groq.
    /// </summary>
    public static class GroqModels
    {
        private const string ModelsEndpoint = "https://api.groq.com/openai/v1/models";

        /// <summary>
        /// Discovers available models from the Groq API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Groq API key. If null, returns empty list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models with their capabilities.</returns>
        public static async Task<List<DiscoveredModel>> DiscoverAsync(
            HttpClient httpClient, 
            string? apiKey,
            CancellationToken cancellationToken = default)
        {
            // Write to a file to verify this method is being called
            await System.IO.File.WriteAllTextAsync("/tmp/groq-discovery.log", 
                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] GroqModels.DiscoverAsync called with API key: {!string.IsNullOrEmpty(apiKey)}\n");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                // No API key, no discovery
                Console.WriteLine("[GroqModels] No API key provided, returning empty list");
                return new List<DiscoveredModel>();
            }

            Console.WriteLine($"[GroqModels] Discovering models with API key: {apiKey.Substring(0, 10)}...");
            
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Headers.Add("Accept", "application/json");

                var response = await httpClient.SendAsync(request, cancellationToken);
                
                Console.WriteLine($"[GroqModels] API response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    // API call failed, return empty list
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"[GroqModels] API error: {errorContent}");
                    return new List<DiscoveredModel>();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<GroqModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null || apiResponse.Data.Count() == 0)
                {
                    Console.WriteLine("[GroqModels] No models found in API response");
                    return new List<DiscoveredModel>();
                }
                
                Console.WriteLine($"[GroqModels] Found {apiResponse.Data.Count()} models");

                return apiResponse.Data
                    .Where(model => !string.IsNullOrEmpty(model.Id))
                    .Select(ConvertToDiscoveredModel)
                    .ToList();
            }
            catch (Exception ex)
            {
                // Any error during discovery returns empty list
                Console.WriteLine($"[GroqModels] Exception during discovery: {ex.Message}");
                Console.WriteLine($"[GroqModels] Stack trace: {ex.StackTrace}");
                return new List<DiscoveredModel>();
            }
        }

        private static DiscoveredModel ConvertToDiscoveredModel(GroqModel model)
        {
            var capabilities = InferCapabilities(model);
            
            return new DiscoveredModel
            {
                ModelId = model.Id,
                DisplayName = FormatDisplayName(model.Id),
                Provider = "groq", // This will be replaced with proper provider by caller
                Capabilities = capabilities
            };
        }

        private static ModelCapabilities InferCapabilities(GroqModel model)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = model.Id.ToLowerInvariant();

            // Chat capabilities for language models
            var isLlm = modelIdLower.Contains("llama") || 
                        modelIdLower.Contains("mixtral") || 
                        modelIdLower.Contains("gemma");

            if (isLlm)
            {
                capabilities.Chat = true;
                capabilities.ChatStream = true;
            }

            // Vision capabilities
            capabilities.Vision = modelIdLower.Contains("vision") || modelIdLower.Contains("llava");

            // Tool use capabilities
            capabilities.ToolUse = modelIdLower.Contains("tool-use");
            capabilities.FunctionCalling = modelIdLower.Contains("tool-use");

            // Audio capabilities
            var isWhisper = modelIdLower.Contains("whisper");
            if (isWhisper)
            {
                // Whisper models don't support chat
                capabilities.Chat = false;
                capabilities.ChatStream = false;
                // TODO: We need a way to indicate audio transcription capability
            }

            // JSON mode (most modern LLMs support this)
            capabilities.JsonMode = isLlm && !modelIdLower.Contains("guard");

            // Set context window based on model ID
            if (TryExtractContextLength(model.Id, out var contextLength))
            {
                capabilities.MaxTokens = contextLength;
            }
            else
            {
                // Default context windows
                capabilities.MaxTokens = 8192;
            }

            // Output token limits (Groq models typically have these limits)
            capabilities.MaxOutputTokens = modelIdLower switch
            {
                var id when id.Contains("405b") => 4096,
                var id when id.Contains("70b") => 8192,
                _ => 4096
            };

            return capabilities;
        }

        private static string FormatDisplayName(string modelId)
        {
            // Convert model IDs to more readable display names
            var displayName = modelId
                .Replace("-", " ")
                .Replace("llama", "Llama")
                .Replace("mixtral", "Mixtral")
                .Replace("gemma", "Gemma")
                .Replace("whisper", "Whisper")
                .Replace("llava", "LLaVA")
                .Replace("distil", "Distil");

            // Capitalize words
            var words = displayName.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0 && !char.IsUpper(words[i][0]))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }

            return string.Join(" ", words);
        }

        private static bool TryExtractContextLength(string modelId, out int contextLength)
        {
            contextLength = 0;

            // Look for patterns like "8192", "32768", etc. in the model ID
            var parts = modelId.Split('-');
            foreach (var part in parts)
            {
                if (int.TryParse(part, out var length) && length >= 1024)
                {
                    contextLength = length;
                    return true;
                }
            }

            // Default context lengths for known models
            if (modelId.Contains("405b"))
            {
                contextLength = 131072;
                return true;
            }
            else if (modelId.Contains("mixtral"))
            {
                contextLength = 32768;
                return true;
            }

            return false;
        }

        private class GroqModelsResponse
        {
            public List<GroqModel> Data { get; set; } = new();
        }

        private class GroqModel
        {
            public string Id { get; set; } = string.Empty;
            public string Object { get; set; } = string.Empty;
            public long? Created { get; set; }
            public string? OwnedBy { get; set; }
        }
    }
}