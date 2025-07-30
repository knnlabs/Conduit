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
                    // If the local Ollama service is not running, return popular models
                    return GetPopularModels();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<OllamaModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Models == null || apiResponse.Models.Count == 0)
                {
                    return GetPopularModels();
                }

                return apiResponse.Models
                    .Where(model => !string.IsNullOrEmpty(model.Name))
                    .Select(ConvertToDiscoveredModel)
                    .ToList();
            }
            catch (Exception)
            {
                // If Ollama is not running or accessible, return popular models
                return GetPopularModels();
            }
        }

        private static List<DiscoveredModel> GetPopularModels()
        {
            // Popular Ollama models that users commonly download
            var knownModels = new List<(string id, string displayName, string description, string baseModel)>
            {
                // Llama models
                ("llama3.2", "Llama 3.2", "Latest Llama model with vision capabilities", "meta-llama"),
                ("llama3.1", "Llama 3.1", "Advanced Llama model", "meta-llama"),
                ("llama3", "Llama 3", "Meta's Llama 3 model", "meta-llama"),
                ("llama2", "Llama 2", "Meta's Llama 2 model", "meta-llama"),
                ("codellama", "Code Llama", "Code-specialized Llama model", "meta-llama"),
                
                // Mistral models
                ("mistral", "Mistral 7B", "Mistral's base model", "mistral"),
                ("mixtral", "Mixtral 8x7B", "Mistral's MoE model", "mistral"),
                ("mistral-nemo", "Mistral Nemo", "Efficient Mistral model", "mistral"),
                
                // Google models
                ("gemma2", "Gemma 2", "Google's latest open model", "google"),
                ("gemma", "Gemma", "Google's open model", "google"),
                ("codegemma", "CodeGemma", "Code-focused Gemma", "google"),
                
                // Microsoft models
                ("phi3", "Phi-3", "Microsoft's small language model", "microsoft"),
                ("phi3.5", "Phi-3.5", "Latest Phi model", "microsoft"),
                ("orca-mini", "Orca Mini", "Compact reasoning model", "microsoft"),
                
                // Qwen models
                ("qwen2.5", "Qwen 2.5", "Alibaba's latest model", "alibaba"),
                ("qwen2", "Qwen 2", "Alibaba's Qwen model", "alibaba"),
                ("qwen", "Qwen", "Alibaba's base model", "alibaba"),
                
                // Other popular models
                ("deepseek-coder-v2", "DeepSeek Coder v2", "Advanced code model", "deepseek"),
                ("neural-chat", "Neural Chat", "Intel's optimized chat model", "intel"),
                ("starling-lm", "Starling LM", "Berkeley's RLHF model", "berkeley"),
                ("zephyr", "Zephyr", "Fine-tuned Mistral model", "huggingface"),
                ("vicuna", "Vicuna", "LMSYS chat model", "lmsys"),
                ("orca2", "Orca 2", "Microsoft reasoning model", "microsoft"),
                ("solar", "Solar", "Upstage's LLM", "upstage"),
                ("tinyllama", "TinyLlama", "Compact 1.1B model", "community"),
                ("dolphin-mixtral", "Dolphin Mixtral", "Uncensored Mixtral", "cognitivecomputations"),
                
                // Embedding models
                ("nomic-embed-text", "Nomic Embed Text", "Text embedding model", "nomic"),
                ("all-minilm", "All-MiniLM", "Sentence embeddings", "sentence-transformers"),
                ("mxbai-embed-large", "MixedBread Embed", "High-quality embeddings", "mixedbread"),
                
                // Vision models
                ("llava", "LLaVA", "Vision-language model", "community"),
                ("bakllava", "BakLLaVA", "Improved LLaVA model", "community"),
                ("moondream", "Moondream", "Efficient vision model", "vikhyat")
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "ollama",
                Capabilities = InferCapabilitiesFromModel(model.id, model.baseModel),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description,
                    ["base_model"] = model.baseModel,
                    ["pull_command"] = $"ollama pull {model.id}"
                }
            }).ToList();
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