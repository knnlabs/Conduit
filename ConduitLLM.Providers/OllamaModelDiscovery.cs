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
    /// Provides model discovery capabilities for Ollama.
    /// </summary>
    public static class OllamaModelDiscovery
    {
        // Ollama has a local models endpoint
        private const string ModelsEndpoint = "http://localhost:11434/api/tags";

        /// <summary>
        /// Discovers available models from Ollama.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Not used for Ollama (local service).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models with their capabilities.</returns>
        public static async Task<List<DiscoveredModel>> DiscoverAsync(
            HttpClient httpClient, 
            string? apiKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Ollama doesn't require authentication
                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                request.Headers.Add("Accept", "application/json");

                var response = await httpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    // If the local Ollama service is not running, return empty list
                    return new List<DiscoveredModel>();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<OllamaModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Models == null || apiResponse.Models.Count == 0)
                {
                    return new List<DiscoveredModel>();
                }

                return apiResponse.Models
                    .Where(model => !string.IsNullOrEmpty(model.Name))
                    .Select(ConvertToDiscoveredModel)
                    .ToList();
            }
            catch (Exception)
            {
                // If Ollama is not running or accessible, return empty list
                return new List<DiscoveredModel>();
            }
        }


        private static DiscoveredModel ConvertToDiscoveredModel(OllamaModel model)
        {
            var modelName = ExtractModelName(model.Name);
            var capabilities = InferCapabilitiesFromModel(modelName, DetermineBaseModel(modelName));
            
            return new DiscoveredModel
            {
                ModelId = model.Name,
                DisplayName = FormatDisplayName(modelName),
                Provider = "ollama",
                Capabilities = capabilities,
                Metadata = new Dictionary<string, object>
                {
                    ["size"] = FormatSize(model.Size),
                    ["modified"] = model.ModifiedAt ?? "",
                    ["digest"] = model.Digest ?? ""
                }
            };
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId, string baseModel)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            // Embedding models
            if (modelIdLower.Contains("embed") || modelIdLower.Contains("minilm") || 
                modelIdLower.Contains("bge") || modelIdLower.Contains("e5"))
            {
                capabilities.Embeddings = true;
                capabilities.Chat = false;
                capabilities.ChatStream = false;
                capabilities.MaxTokens = 512; // Typical for embeddings
                return capabilities;
            }

            // Vision models
            if (modelIdLower.Contains("llava") || modelIdLower.Contains("bakllava") || 
                modelIdLower.Contains("moondream") || modelIdLower.Contains("llama3.2"))
            {
                capabilities.Vision = true;
            }

            // All other Ollama models support chat
            capabilities.Chat = true;
            capabilities.ChatStream = true;
            
            // Most models don't support function calling in Ollama
            capabilities.FunctionCalling = false;
            capabilities.ToolUse = false;
            capabilities.JsonMode = false;

            // Context windows vary by model
            if (modelIdLower.Contains("llama3") || modelIdLower.Contains("llama-3"))
            {
                capabilities.MaxTokens = modelIdLower.Contains("3.1") || modelIdLower.Contains("3.2") ? 131072 : 8192;
                capabilities.MaxOutputTokens = 4096;
            }
            else if (modelIdLower.Contains("mixtral"))
            {
                capabilities.MaxTokens = 32768;
                capabilities.MaxOutputTokens = 4096;
            }
            else if (modelIdLower.Contains("qwen2.5") || modelIdLower.Contains("qwen2"))
            {
                capabilities.MaxTokens = 32768;
                capabilities.MaxOutputTokens = 4096;
            }
            else if (modelIdLower.Contains("phi3"))
            {
                capabilities.MaxTokens = modelIdLower.Contains("3.5") ? 128000 : 4096;
                capabilities.MaxOutputTokens = 2048;
            }
            else if (modelIdLower.Contains("gemma"))
            {
                capabilities.MaxTokens = 8192;
                capabilities.MaxOutputTokens = 2048;
            }
            else if (modelIdLower.Contains("deepseek"))
            {
                capabilities.MaxTokens = 16384;
                capabilities.MaxOutputTokens = 4096;
            }
            else
            {
                // Default context for most models
                capabilities.MaxTokens = 4096;
                capabilities.MaxOutputTokens = 2048;
            }

            return capabilities;
        }

        private static string ExtractModelName(string fullName)
        {
            // Remove tag suffix (e.g., ":latest", ":7b", ":13b-instruct-q4_0")
            var colonIndex = fullName.IndexOf(':');
            return colonIndex > 0 ? fullName.Substring(0, colonIndex) : fullName;
        }

        private static string DetermineBaseModel(string modelName)
        {
            var nameLower = modelName.ToLowerInvariant();
            
            if (nameLower.Contains("llama") || nameLower.Contains("codellama"))
                return "meta-llama";
            if (nameLower.Contains("mistral") || nameLower.Contains("mixtral"))
                return "mistral";
            if (nameLower.Contains("gemma") || nameLower.Contains("codegemma"))
                return "google";
            if (nameLower.Contains("phi") || nameLower.Contains("orca"))
                return "microsoft";
            if (nameLower.Contains("qwen"))
                return "alibaba";
            if (nameLower.Contains("deepseek"))
                return "deepseek";
            
            return "community";
        }

        private static string FormatDisplayName(string modelName)
        {
            return modelName
                .Replace("-", " ")
                .Replace("llama", "Llama")
                .Replace("mistral", "Mistral")
                .Replace("mixtral", "Mixtral")
                .Replace("gemma", "Gemma")
                .Replace("qwen", "Qwen")
                .Replace("phi", "Phi")
                .Replace("orca", "Orca")
                .Replace("deepseek", "DeepSeek")
                .Replace("coder", "Coder")
                .Replace("embed", "Embed");
        }

        private static string FormatSize(long? sizeInBytes)
        {
            if (!sizeInBytes.HasValue) return "Unknown";
            
            var size = sizeInBytes.Value;
            if (size < 1024) return $"{size} B";
            if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
            if (size < 1024 * 1024 * 1024) return $"{size / (1024.0 * 1024.0):F1} MB";
            return $"{size / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        private class OllamaModelsResponse
        {
            public List<OllamaModel> Models { get; set; } = new();
        }

        private class OllamaModel
        {
            public string Name { get; set; } = string.Empty;
            public string? ModifiedAt { get; set; }
            public long? Size { get; set; }
            public string? Digest { get; set; }
        }
    }
}