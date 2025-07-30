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
    /// Provides model discovery capabilities for AWS Bedrock.
    /// </summary>
    public static class BedrockModelDiscovery
    {
        /// <summary>
        /// Discovers available models from AWS Bedrock.
        /// Note: Bedrock requires AWS SDK and region-specific discovery. We return known available models.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">AWS credentials. If null, returns empty list.</param>
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

            // Bedrock models are region-specific and require AWS SDK
            // Return the commonly available foundation models
            return await Task.FromResult(GetAvailableModels());
        }

        private static List<DiscoveredModel> GetAvailableModels()
        {
            // Based on AWS Bedrock foundation models
            var knownModels = new List<(string id, string displayName, string provider, string description)>
            {
                // Anthropic Claude models
                ("anthropic.claude-3-opus-20240229", "Claude 3 Opus", "Anthropic", "Most capable Claude model"),
                ("anthropic.claude-3-sonnet-20240229", "Claude 3 Sonnet", "Anthropic", "Balanced performance Claude model"),
                ("anthropic.claude-3-haiku-20240307", "Claude 3 Haiku", "Anthropic", "Fast, lightweight Claude model"),
                ("anthropic.claude-v2:1", "Claude v2.1", "Anthropic", "Previous generation Claude"),
                ("anthropic.claude-v2", "Claude v2", "Anthropic", "Previous generation Claude"),
                ("anthropic.claude-instant-v1", "Claude Instant v1", "Anthropic", "Fast Claude model"),
                
                // Amazon Titan models
                ("amazon.titan-text-express-v1", "Titan Text Express", "Amazon", "Cost-effective text generation"),
                ("amazon.titan-text-lite-v1", "Titan Text Lite", "Amazon", "Lightweight text model"),
                ("amazon.titan-text-premier-v1:0", "Titan Text Premier", "Amazon", "Advanced text generation"),
                ("amazon.titan-embed-text-v1", "Titan Embeddings", "Amazon", "Text embedding model"),
                ("amazon.titan-embed-text-v2:0", "Titan Embeddings v2", "Amazon", "Enhanced text embeddings"),
                ("amazon.titan-image-generator-v1", "Titan Image Generator", "Amazon", "Image generation model"),
                ("amazon.titan-multimodal-embeddings-g1", "Titan Multimodal Embeddings", "Amazon", "Multimodal embeddings"),
                
                // AI21 Jurassic models
                ("ai21.j2-ultra-v1", "Jurassic-2 Ultra", "AI21", "Large language model"),
                ("ai21.j2-mid-v1", "Jurassic-2 Mid", "AI21", "Medium language model"),
                
                // Cohere models
                ("cohere.command-text-v14", "Command", "Cohere", "Text generation model"),
                ("cohere.command-light-text-v14", "Command Light", "Cohere", "Lightweight text model"),
                ("cohere.embed-english-v3", "Embed English v3", "Cohere", "English text embeddings"),
                ("cohere.embed-multilingual-v3", "Embed Multilingual v3", "Cohere", "Multilingual embeddings"),
                
                // Meta Llama models
                ("meta.llama3-8b-instruct-v1:0", "Llama 3 8B", "Meta", "Efficient instruction model"),
                ("meta.llama3-70b-instruct-v1:0", "Llama 3 70B", "Meta", "Large instruction model"),
                ("meta.llama2-13b-chat-v1", "Llama 2 13B Chat", "Meta", "Chat-optimized model"),
                ("meta.llama2-70b-chat-v1", "Llama 2 70B Chat", "Meta", "Large chat model"),
                
                // Mistral models
                ("mistral.mistral-7b-instruct-v0:2", "Mistral 7B", "Mistral", "Efficient instruction model"),
                ("mistral.mixtral-8x7b-instruct-v0:1", "Mixtral 8x7B", "Mistral", "Mixture of experts model"),
                ("mistral.mistral-large-2402-v1:0", "Mistral Large", "Mistral", "Large language model"),
                
                // Stability AI models
                ("stability.stable-diffusion-xl-v1", "Stable Diffusion XL", "Stability AI", "Image generation model")
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "bedrock",
                Capabilities = InferCapabilitiesFromModel(model.id, model.provider),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description,
                    ["foundation_provider"] = model.provider,
                    ["model_arn_format"] = $"arn:aws:bedrock:*::foundation-model/{model.id}"
                }
            }).ToList();
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId, string foundationProvider)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            // Claude models
            if (modelIdLower.Contains("claude"))
            {
                capabilities.Chat = true;
                capabilities.ChatStream = true;
                capabilities.Vision = modelIdLower.Contains("claude-3");
                capabilities.ToolUse = true;
                capabilities.JsonMode = false;
                
                if (modelIdLower.Contains("opus"))
                {
                    capabilities.MaxTokens = 200000;
                    capabilities.MaxOutputTokens = 4096;
                }
                else if (modelIdLower.Contains("sonnet") || modelIdLower.Contains("haiku"))
                {
                    capabilities.MaxTokens = 200000;
                    capabilities.MaxOutputTokens = 4096;
                }
                else
                {
                    capabilities.MaxTokens = 100000;
                    capabilities.MaxOutputTokens = 4096;
                }
            }
            // Titan models
            else if (modelIdLower.Contains("titan"))
            {
                if (modelIdLower.Contains("embed"))
                {
                    capabilities.Embeddings = true;
                    capabilities.Chat = false;
                    capabilities.MaxTokens = 8192;
                }
                else if (modelIdLower.Contains("image"))
                {
                    capabilities.ImageGeneration = true;
                    capabilities.SupportedImageSizes = new List<string> { "512x512", "1024x1024" };
                }
                else
                {
                    capabilities.Chat = true;
                    capabilities.ChatStream = true;
                    capabilities.MaxTokens = modelIdLower.Contains("premier") ? 32768 : 8192;
                    capabilities.MaxOutputTokens = 4096;
                }
            }
            // Llama models
            else if (modelIdLower.Contains("llama"))
            {
                capabilities.Chat = true;
                capabilities.ChatStream = true;
                capabilities.FunctionCalling = false;
                capabilities.ToolUse = false;
                
                if (modelIdLower.Contains("70b"))
                {
                    capabilities.MaxTokens = 4096;
                    capabilities.MaxOutputTokens = 2048;
                }
                else
                {
                    capabilities.MaxTokens = 4096;
                    capabilities.MaxOutputTokens = 1024;
                }
            }
            // Jurassic models
            else if (modelIdLower.Contains("j2"))
            {
                capabilities.Chat = true;
                capabilities.ChatStream = true;
                capabilities.MaxTokens = 8192;
                capabilities.MaxOutputTokens = 2048;
            }
            // Cohere models
            else if (modelIdLower.Contains("cohere"))
            {
                if (modelIdLower.Contains("embed"))
                {
                    capabilities.Embeddings = true;
                    capabilities.Chat = false;
                    capabilities.MaxTokens = 512;
                }
                else
                {
                    capabilities.Chat = true;
                    capabilities.ChatStream = true;
                    capabilities.MaxTokens = 4000;
                    capabilities.MaxOutputTokens = 2048;
                }
            }
            // Mistral models
            else if (modelIdLower.Contains("mistral") || modelIdLower.Contains("mixtral"))
            {
                capabilities.Chat = true;
                capabilities.ChatStream = true;
                capabilities.FunctionCalling = !modelIdLower.Contains("7b");
                capabilities.ToolUse = capabilities.FunctionCalling;
                capabilities.JsonMode = modelIdLower.Contains("large") || modelIdLower.Contains("mixtral");
                capabilities.MaxTokens = 32768;
                capabilities.MaxOutputTokens = modelIdLower.Contains("large") ? 4096 : 2048;
            }
            // Stable Diffusion
            else if (modelIdLower.Contains("stable-diffusion"))
            {
                capabilities.ImageGeneration = true;
                capabilities.SupportedImageSizes = new List<string> 
                { 
                    "512x512", "768x768", "1024x1024", "1024x768", "768x1024" 
                };
            }

            return capabilities;
        }
    }
}