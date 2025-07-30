using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Provides model discovery capabilities for AWS SageMaker.
    /// </summary>
    public static class SageMakerModelDiscovery
    {
        /// <summary>
        /// Discovers available models from AWS SageMaker.
        /// Note: SageMaker models are deployment-specific. We return common JumpStart models.
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

            // SageMaker models are endpoint-specific; return common JumpStart models
            return await Task.FromResult(GetJumpStartModels());
        }

        private static List<DiscoveredModel> GetJumpStartModels()
        {
            // Common AWS SageMaker JumpStart foundation models
            var knownModels = new List<(string id, string displayName, string description)>
            {
                // Meta models
                ("meta-textgeneration-llama-3-8b", "Llama 3 8B", "Meta's efficient model"),
                ("meta-textgeneration-llama-3-70b", "Llama 3 70B", "Meta's large model"),
                ("meta-textgeneration-llama-2-7b", "Llama 2 7B", "Llama 2 base model"),
                ("meta-textgeneration-llama-2-13b", "Llama 2 13B", "Llama 2 medium model"),
                ("meta-textgeneration-llama-2-70b", "Llama 2 70B", "Llama 2 large model"),
                
                // Mistral models
                ("mistral-7b-instruct", "Mistral 7B", "Mistral's base model"),
                ("mixtral-8x7b", "Mixtral 8x7B", "Mistral's MoE model"),
                
                // Cohere models
                ("cohere-gpt-medium", "Cohere GPT Medium", "Cohere's medium model"),
                ("cohere-gpt-xlarge", "Cohere GPT XLarge", "Cohere's large model"),
                
                // AI21 models
                ("ai21-j2-grande-instruct", "Jurassic-2 Grande", "AI21's instruction model"),
                ("ai21-j2-jumbo-instruct", "Jurassic-2 Jumbo", "AI21's largest model"),
                
                // Stability AI models
                ("stable-diffusion-2-1-base", "Stable Diffusion 2.1", "Image generation"),
                ("stable-diffusion-xl-base", "SDXL Base", "High-res image generation"),
                
                // Falcon models
                ("falcon-7b-instruct", "Falcon 7B", "TII's efficient model"),
                ("falcon-40b-instruct", "Falcon 40B", "TII's large model"),
                
                // Other models
                ("flan-t5-xxl", "FLAN-T5 XXL", "Google's instruction-tuned T5"),
                ("bloom-7b1", "BLOOM 7B", "BigScience multilingual model"),
                ("gpt-j-6b", "GPT-J 6B", "EleutherAI's open model")
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "sagemaker",
                Capabilities = InferCapabilitiesFromModel(model.id),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description,
                    ["deployment_type"] = "SageMaker Endpoint",
                    ["jumpstart_model"] = true
                }
            }).ToList();
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            // Image generation models
            if (modelIdLower.Contains("stable-diffusion"))
            {
                capabilities.ImageGeneration = true;
                capabilities.SupportedImageSizes = modelIdLower.Contains("xl") 
                    ? new List<string> { "1024x1024", "1024x768", "768x1024" }
                    : new List<string> { "512x512", "768x768" };
                return capabilities;
            }

            // All other models are text generation
            capabilities.Chat = true;
            capabilities.ChatStream = true;
            capabilities.FunctionCalling = false; // SageMaker endpoints typically don't support this
            capabilities.ToolUse = false;
            capabilities.JsonMode = false;

            // Set context windows based on model
            if (modelIdLower.Contains("llama-3"))
            {
                capabilities.MaxTokens = 8192;
                capabilities.MaxOutputTokens = 2048;
            }
            else if (modelIdLower.Contains("llama-2-70b") || modelIdLower.Contains("falcon-40b"))
            {
                capabilities.MaxTokens = 4096;
                capabilities.MaxOutputTokens = 2048;
            }
            else if (modelIdLower.Contains("mixtral"))
            {
                capabilities.MaxTokens = 32768;
                capabilities.MaxOutputTokens = 4096;
            }
            else if (modelIdLower.Contains("flan-t5"))
            {
                capabilities.MaxTokens = 512;
                capabilities.MaxOutputTokens = 512;
            }
            else
            {
                capabilities.MaxTokens = 2048;
                capabilities.MaxOutputTokens = 1024;
            }

            return capabilities;
        }
    }
}