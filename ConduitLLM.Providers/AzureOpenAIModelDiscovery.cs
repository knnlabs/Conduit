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
    /// Provides model discovery capabilities for Azure OpenAI Service.
    /// </summary>
    public static class AzureOpenAIModelDiscovery
    {
        /// <summary>
        /// Discovers available models from Azure OpenAI.
        /// Note: Azure OpenAI doesn't have a standard models endpoint, so we return known deployable models.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Azure OpenAI API key. If null, returns empty list.</param>
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

            // Azure OpenAI uses deployment names rather than model discovery
            // Return the available models that can be deployed
            return await Task.FromResult(GetDeployableModels());
        }

        private static List<DiscoveredModel> GetDeployableModels()
        {
            // Based on Azure OpenAI deployable models
            var knownModels = new List<(string id, string displayName, string description, string version)>
            {
                // GPT-4 models
                ("gpt-4", "GPT-4", "Most capable GPT-4 model", "0613"),
                ("gpt-4-32k", "GPT-4 32K", "GPT-4 with 32K context", "0613"),
                ("gpt-4-turbo", "GPT-4 Turbo", "Latest GPT-4 Turbo with vision", "2024-04-09"),
                ("gpt-4-turbo-2024-04-09", "GPT-4 Turbo (April 2024)", "GPT-4 Turbo with vision capabilities", "2024-04-09"),
                ("gpt-4o", "GPT-4o", "Multimodal GPT-4 optimized model", "2024-05-13"),
                ("gpt-4o-mini", "GPT-4o Mini", "Smaller, faster GPT-4o variant", "2024-07-18"),
                
                // GPT-3.5 models
                ("gpt-35-turbo", "GPT-3.5 Turbo", "Fast, efficient model", "0613"),
                ("gpt-35-turbo-16k", "GPT-3.5 Turbo 16K", "GPT-3.5 with 16K context", "0613"),
                ("gpt-35-turbo-instruct", "GPT-3.5 Turbo Instruct", "Instruction-following model", "0914"),
                
                // Embedding models
                ("text-embedding-ada-002", "Ada Embeddings v2", "Second generation embeddings", "2"),
                ("text-embedding-3-small", "Embeddings 3 Small", "Efficient embedding model", "3"),
                ("text-embedding-3-large", "Embeddings 3 Large", "High-quality embeddings", "3"),
                
                // DALL-E models
                ("dall-e-3", "DALL-E 3", "Latest image generation model", "3.0"),
                ("dall-e-2", "DALL-E 2", "Image generation model", "2.0"),
                
                // Whisper model
                ("whisper", "Whisper", "Speech to text model", "001"),
                
                // TTS models
                ("tts-1", "TTS-1", "Text to speech model", "001"),
                ("tts-1-hd", "TTS-1 HD", "High quality text to speech", "001")
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "azureopenai",
                Capabilities = InferCapabilitiesFromModel(model.id),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description,
                    ["version"] = model.version,
                    ["deployment_note"] = "Requires deployment in Azure OpenAI resource"
                }
            }).ToList();
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            // GPT models
            if (modelIdLower.StartsWith("gpt-"))
            {
                capabilities.Chat = true;
                capabilities.ChatStream = true;
                capabilities.FunctionCalling = true;
                capabilities.ToolUse = true;
                capabilities.JsonMode = true;
                
                // Vision capabilities
                capabilities.Vision = modelIdLower.Contains("gpt-4-turbo") || 
                                   modelIdLower.Contains("gpt-4o") ||
                                   modelIdLower.Contains("vision");
                
                // Context windows
                if (modelIdLower.Contains("32k"))
                {
                    capabilities.MaxTokens = 32768;
                    capabilities.MaxOutputTokens = 4096;
                }
                else if (modelIdLower.Contains("16k"))
                {
                    capabilities.MaxTokens = 16384;
                    capabilities.MaxOutputTokens = 4096;
                }
                else if (modelIdLower.Contains("gpt-4"))
                {
                    capabilities.MaxTokens = modelIdLower.Contains("turbo") ? 128000 : 8192;
                    capabilities.MaxOutputTokens = 4096;
                }
                else
                {
                    capabilities.MaxTokens = 4096;
                    capabilities.MaxOutputTokens = 4096;
                }
            }
            // Embedding models
            else if (modelIdLower.Contains("embedding"))
            {
                capabilities.Embeddings = true;
                capabilities.Chat = false;
                capabilities.ChatStream = false;
                capabilities.MaxTokens = 8191; // Standard for embeddings
            }
            // DALL-E models
            else if (modelIdLower.Contains("dall-e"))
            {
                capabilities.ImageGeneration = true;
                capabilities.SupportedImageSizes = modelIdLower.Contains("3") 
                    ? new List<string> { "1024x1024", "1792x1024", "1024x1792" }
                    : new List<string> { "256x256", "512x512", "1024x1024" };
            }
            // Whisper (speech to text)
            else if (modelIdLower.Contains("whisper"))
            {
                // Speech recognition capabilities
                capabilities.Chat = false;
                capabilities.ChatStream = false;
            }
            // TTS models
            else if (modelIdLower.Contains("tts"))
            {
                // Text to speech capabilities
                capabilities.Chat = false;
                capabilities.ChatStream = false;
            }

            return capabilities;
        }
    }
}