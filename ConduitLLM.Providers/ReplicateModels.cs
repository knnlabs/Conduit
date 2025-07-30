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
    /// Provides model discovery capabilities for Replicate.
    /// </summary>
    public static class ReplicateModels
    {
        // Replicate has a models endpoint
        private const string ModelsEndpoint = "https://api.replicate.com/v1/models";

        /// <summary>
        /// Discovers available models from the Replicate API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Replicate API key. If null, returns empty list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models with their capabilities.</returns>
        public static Task<List<DiscoveredModel>> DiscoverAsync(
            HttpClient httpClient, 
            string? apiKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return Task.FromResult(new List<DiscoveredModel>());
            }

            try
            {
                // For Replicate, we'll return a curated list of popular models
                // The full API requires pagination and returns thousands of models
                return Task.FromResult(GetPopularModels());
            }
            catch (Exception)
            {
                // Any error during discovery returns known models list
                return Task.FromResult(GetPopularModels());
            }
        }

        private static List<DiscoveredModel> GetPopularModels()
        {
            // Based on popular Replicate models
            var knownModels = new List<(string id, string displayName, string description, ModelType type)>
            {
                // Flux models (state-of-the-art image generation)
                ("black-forest-labs/flux-schnell", "FLUX.1 Schnell", "Fast image generation model", ModelType.ImageGeneration),
                ("black-forest-labs/flux-dev", "FLUX.1 Dev", "High-quality image generation", ModelType.ImageGeneration),
                ("black-forest-labs/flux-pro", "FLUX.1 Pro", "Professional image generation", ModelType.ImageGeneration),
                
                // Stable Diffusion models
                ("stability-ai/stable-diffusion", "Stable Diffusion 1.5", "Classic text-to-image model", ModelType.ImageGeneration),
                ("stability-ai/sdxl", "Stable Diffusion XL", "High-resolution image generation", ModelType.ImageGeneration),
                ("stability-ai/stable-diffusion-3", "Stable Diffusion 3", "Latest SD generation", ModelType.ImageGeneration),
                
                // Video generation models
                ("stability-ai/stable-video-diffusion", "Stable Video Diffusion", "Image-to-video generation", ModelType.VideoGeneration),
                ("runway/gen-3-alpha-turbo", "Gen-3 Alpha Turbo", "Fast video generation", ModelType.VideoGeneration),
                ("runway/gen-3-alpha", "Gen-3 Alpha", "High-quality video generation", ModelType.VideoGeneration),
                
                // LLM models
                ("meta/llama-2-70b-chat", "Llama 2 70B Chat", "Large language model", ModelType.Chat),
                ("meta/llama-2-13b-chat", "Llama 2 13B Chat", "Efficient language model", ModelType.Chat),
                ("meta/llama-2-7b-chat", "Llama 2 7B Chat", "Compact language model", ModelType.Chat),
                ("meta/meta-llama-3-70b-instruct", "Llama 3 70B Instruct", "Latest Llama model", ModelType.Chat),
                ("meta/meta-llama-3-8b-instruct", "Llama 3 8B Instruct", "Efficient Llama 3", ModelType.Chat),
                
                // Code models
                ("meta/codellama-70b-instruct", "Code Llama 70B", "Code generation model", ModelType.Chat),
                ("meta/codellama-34b-instruct", "Code Llama 34B", "Code generation model", ModelType.Chat),
                
                // Music generation
                ("meta/musicgen", "MusicGen", "Text-to-music generation", ModelType.Audio),
                ("suno-ai/bark", "Bark", "Text-to-speech with emotion", ModelType.Audio),
                
                // Image understanding
                ("salesforce/blip", "BLIP", "Image captioning model", ModelType.Vision),
                ("pharmapsychotic/clip-interrogator", "CLIP Interrogator", "Image analysis", ModelType.Vision),
                
                // Whisper
                ("openai/whisper", "Whisper", "Speech recognition", ModelType.SpeechToText),
                
                // Image editing
                ("tencentarc/photomaker", "PhotoMaker", "Personalized image generation", ModelType.ImageGeneration),
                ("lucataco/remove-bg", "Remove Background", "Background removal", ModelType.ImageEditing),
                ("sczhou/codeformer", "CodeFormer", "Face restoration", ModelType.ImageEditing),
                ("jingyunliang/swinir", "SwinIR", "Image super-resolution", ModelType.ImageEditing)
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "replicate",
                Capabilities = InferCapabilitiesFromModel(model.id, model.type),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description,
                    ["type"] = model.type.ToString()
                }
            }).ToList();
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId, ModelType type)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            switch (type)
            {
                case ModelType.Chat:
                    capabilities.Chat = true;
                    capabilities.ChatStream = true;
                    capabilities.FunctionCalling = false;
                    capabilities.ToolUse = false;
                    capabilities.JsonMode = false;
                    
                    // Context windows based on model
                    if (modelIdLower.Contains("70b"))
                    {
                        capabilities.MaxTokens = 4096;
                        capabilities.MaxOutputTokens = 2048;
                    }
                    else if (modelIdLower.Contains("34b") || modelIdLower.Contains("13b"))
                    {
                        capabilities.MaxTokens = 4096;
                        capabilities.MaxOutputTokens = 1024;
                    }
                    else
                    {
                        capabilities.MaxTokens = 2048;
                        capabilities.MaxOutputTokens = 512;
                    }
                    break;

                case ModelType.ImageGeneration:
                    capabilities.ImageGeneration = true;
                    if (modelIdLower.Contains("flux") || modelIdLower.Contains("sdxl"))
                    {
                        capabilities.SupportedImageSizes = new List<string> 
                        { 
                            "1024x1024", "1024x768", "768x1024", "1280x720", "720x1280",
                            "1920x1080", "1080x1920", "2048x2048"
                        };
                    }
                    else
                    {
                        capabilities.SupportedImageSizes = new List<string> 
                        { 
                            "512x512", "768x768", "1024x1024" 
                        };
                    }
                    break;

                case ModelType.VideoGeneration:
                    capabilities.VideoGeneration = true;
                    capabilities.SupportedVideoResolutions = new List<string> 
                    { 
                        "576x1024", "1024x576", "768x768", "1280x720", "720x1280" 
                    };
                    capabilities.MaxVideoDurationSeconds = modelIdLower.Contains("turbo") ? 5 : 10;
                    break;

                case ModelType.Audio:
                    // Audio generation capabilities
                    capabilities.Chat = false;
                    capabilities.ChatStream = false;
                    break;

                case ModelType.Vision:
                    capabilities.Vision = true;
                    capabilities.Chat = true; // Most vision models can describe images
                    capabilities.ChatStream = false;
                    break;

                case ModelType.SpeechToText:
                    // Speech recognition capabilities
                    capabilities.Chat = false;
                    capabilities.ChatStream = false;
                    break;

                case ModelType.ImageEditing:
                    capabilities.ImageGeneration = true; // Image-to-image
                    capabilities.Vision = true; // Can process input images
                    break;
            }

            return capabilities;
        }

        private enum ModelType
        {
            Chat,
            ImageGeneration,
            VideoGeneration,
            Audio,
            Vision,
            SpeechToText,
            ImageEditing
        }
    }
}