using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Provides model discovery capabilities for Hugging Face.
    /// </summary>
    public static class HuggingFaceModelDiscovery
    {
        /// <summary>
        /// Discovers available models from Hugging Face.
        /// Note: HF has thousands of models. We return popular inference API models.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Hugging Face API key. If null, returns empty list.</param>
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

            // Load models from static JSON file
            return await LoadStaticModelsAsync();
        }

        private static async Task<List<DiscoveredModel>> LoadStaticModelsAsync()
        {
            try
            {
                // Get the path to the JSON file relative to the assembly location
                var assembly = typeof(HuggingFaceModelDiscovery).Assembly;
                var assemblyLocation = Path.GetDirectoryName(assembly.Location);
                var jsonPath = Path.Combine(assemblyLocation!, "StaticModels", "huggingface-models.json");
                
                if (!File.Exists(jsonPath))
                {
                    // Fallback to legacy hardcoded method if JSON file not found
                    return GetPopularModels();
                }

                var json = await File.ReadAllTextAsync(jsonPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                var modelsData = JsonSerializer.Deserialize<StaticModelsData>(json, options);
                
                if (modelsData?.Models == null || modelsData.Models.Count == 0)
                {
                    return GetPopularModels();
                }

                return modelsData.Models.Select(model => new DiscoveredModel
                {
                    ModelId = model.Id,
                    DisplayName = model.Name ?? model.Id,
                    Provider = "huggingface",
                    Capabilities = ConvertCapabilities(model),
                    Metadata = new Dictionary<string, object>
                    {
                        ["created"] = model.Created ?? 0,
                        ["owned_by"] = model.OwnedBy ?? "community",
                        ["object"] = model.Object ?? "model"
                    }
                }).ToList();
            }
            catch
            {
                // If any error occurs loading from JSON, fall back to legacy method
                return GetPopularModels();
            }
        }

        private static ModelCapabilities ConvertCapabilities(StaticModelData model)
        {
            var capabilities = new ModelCapabilities();
            
            if (model.Capabilities != null)
            {
                capabilities.Chat = model.Capabilities.Chat ?? false;
                capabilities.ChatStream = model.Capabilities.Chat ?? false; // If chat is supported, streaming usually is too
                capabilities.Embeddings = model.Capabilities.Embeddings ?? false;
                capabilities.ImageGeneration = model.Capabilities.ImageGeneration ?? false;
                capabilities.Vision = model.Capabilities.Vision ?? false;
                capabilities.FunctionCalling = model.Capabilities.FunctionCalling ?? false;
                capabilities.ToolUse = model.Capabilities.FunctionCalling ?? false;
                capabilities.JsonMode = model.Capabilities.JsonMode ?? false;
                capabilities.VideoGeneration = model.Capabilities.VideoGeneration ?? false;
                capabilities.VideoUnderstanding = model.Capabilities.VideoUnderstanding ?? false;
            }
            
            capabilities.MaxTokens = model.ContextLength;
            capabilities.MaxOutputTokens = model.MaxOutputTokens;
            capabilities.SupportedImageSizes = model.SupportedImageSizes;
            capabilities.SupportedVideoResolutions = model.SupportedVideoResolutions;
            capabilities.MaxVideoDurationSeconds = model.MaxVideoDurationSeconds;
            
            return capabilities;
        }

        private static List<DiscoveredModel> GetPopularModels()
        {
            // Popular models available via HuggingFace Inference API
            var knownModels = new List<(string id, string displayName, string description, ModelType type)>
            {
                // Text Generation models
                ("meta-llama/Meta-Llama-3-8B-Instruct", "Llama 3 8B", "Meta's instruction-tuned model", ModelType.TextGeneration),
                ("meta-llama/Meta-Llama-3-70B-Instruct", "Llama 3 70B", "Large Llama 3 model", ModelType.TextGeneration),
                ("mistralai/Mistral-7B-Instruct-v0.3", "Mistral 7B v0.3", "Latest Mistral model", ModelType.TextGeneration),
                ("mistralai/Mixtral-8x7B-Instruct-v0.1", "Mixtral 8x7B", "MoE architecture", ModelType.TextGeneration),
                ("google/gemma-7b-it", "Gemma 7B IT", "Google's instruction-tuned model", ModelType.TextGeneration),
                ("microsoft/Phi-3-mini-4k-instruct", "Phi-3 Mini", "Compact powerful model", ModelType.TextGeneration),
                ("databricks/dbrx-instruct", "DBRX Instruct", "Databricks' MoE model", ModelType.TextGeneration),
                ("01-ai/Yi-1.5-34B-Chat", "Yi 1.5 34B", "01.AI's chat model", ModelType.TextGeneration),
                ("Qwen/Qwen2-72B-Instruct", "Qwen2 72B", "Alibaba's large model", ModelType.TextGeneration),
                ("deepseek-ai/deepseek-coder-33b-instruct", "DeepSeek Coder 33B", "Code generation model", ModelType.TextGeneration),
                
                // Conversational models
                ("microsoft/DialoGPT-large", "DialoGPT Large", "Conversational AI", ModelType.Conversational),
                ("facebook/blenderbot-3B", "BlenderBot 3B", "Facebook's chatbot", ModelType.Conversational),
                
                // Text-to-Image models
                ("stabilityai/stable-diffusion-xl-base-1.0", "SDXL Base", "High-res image generation", ModelType.TextToImage),
                ("stabilityai/stable-diffusion-2-1", "SD 2.1", "Stable Diffusion 2.1", ModelType.TextToImage),
                ("runwayml/stable-diffusion-v1-5", "SD 1.5", "Classic Stable Diffusion", ModelType.TextToImage),
                ("CompVis/stable-diffusion-v1-4", "SD 1.4", "Original public SD", ModelType.TextToImage),
                ("kandinsky-community/kandinsky-3", "Kandinsky 3", "Russian image model", ModelType.TextToImage),
                ("playgroundai/playground-v2.5-1024px-aesthetic", "Playground v2.5", "Aesthetic-focused model", ModelType.TextToImage),
                
                // Embeddings
                ("sentence-transformers/all-MiniLM-L6-v2", "All-MiniLM-L6", "Efficient embeddings", ModelType.FeatureExtraction),
                ("sentence-transformers/all-mpnet-base-v2", "All-MPNet-Base", "High-quality embeddings", ModelType.FeatureExtraction),
                ("BAAI/bge-large-en-v1.5", "BGE Large EN", "SOTA English embeddings", ModelType.FeatureExtraction),
                ("intfloat/e5-large-v2", "E5 Large v2", "Text embeddings", ModelType.FeatureExtraction),
                
                // Zero-shot classification
                ("facebook/bart-large-mnli", "BART MNLI", "Zero-shot classification", ModelType.ZeroShotClassification),
                ("MoritzLaurer/DeBERTa-v3-base-mnli-fever-anli", "DeBERTa v3 MNLI", "Robust zero-shot", ModelType.ZeroShotClassification),
                
                // Question Answering
                ("deepset/roberta-base-squad2", "RoBERTa SQuAD2", "Question answering", ModelType.QuestionAnswering),
                ("google/tapas-large-finetuned-wtq", "TAPAS Large", "Table QA", ModelType.TableQuestionAnswering),
                
                // Summarization
                ("facebook/bart-large-cnn", "BART CNN", "News summarization", ModelType.Summarization),
                ("google/pegasus-xsum", "Pegasus XSUM", "Abstractive summarization", ModelType.Summarization),
                
                // Translation
                ("Helsinki-NLP/opus-mt-en-de", "OPUS EN-DE", "English to German", ModelType.Translation),
                ("facebook/mbart-large-50-many-to-many-mmt", "mBART-50", "Multilingual translation", ModelType.Translation)
            };

            return knownModels.Select(model => new DiscoveredModel
            {
                ModelId = model.id,
                DisplayName = model.displayName,
                Provider = "huggingface",
                Capabilities = InferCapabilitiesFromModel(model.id, model.type),
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = model.description,
                    ["type"] = model.type.ToString(),
                    ["hub_url"] = $"https://huggingface.co/{model.id}"
                }
            }).ToList();
        }

        private static ModelCapabilities InferCapabilitiesFromModel(string modelId, ModelType type)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            switch (type)
            {
                case ModelType.TextGeneration:
                    capabilities.Chat = true;
                    capabilities.ChatStream = true;
                    capabilities.FunctionCalling = false; // Most HF models don't support this
                    capabilities.ToolUse = false;
                    capabilities.JsonMode = false;
                    
                    // Context windows vary
                    if (modelIdLower.Contains("llama-3"))
                    {
                        capabilities.MaxTokens = modelIdLower.Contains("70b") ? 8192 : 8192;
                        capabilities.MaxOutputTokens = 2048;
                    }
                    else if (modelIdLower.Contains("mixtral"))
                    {
                        capabilities.MaxTokens = 32768;
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("yi"))
                    {
                        capabilities.MaxTokens = 200000; // Yi has large context
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("qwen2"))
                    {
                        capabilities.MaxTokens = 131072;
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("phi"))
                    {
                        capabilities.MaxTokens = 4096;
                        capabilities.MaxOutputTokens = 2048;
                    }
                    else
                    {
                        capabilities.MaxTokens = 4096;
                        capabilities.MaxOutputTokens = 1024;
                    }
                    break;

                case ModelType.Conversational:
                    capabilities.Chat = true;
                    capabilities.ChatStream = false; // HF API typically doesn't stream
                    capabilities.MaxTokens = 1024;
                    capabilities.MaxOutputTokens = 512;
                    break;

                case ModelType.TextToImage:
                    capabilities.ImageGeneration = true;
                    if (modelIdLower.Contains("xl"))
                    {
                        capabilities.SupportedImageSizes = new List<string> 
                        { 
                            "1024x1024", "1024x768", "768x1024", "1152x896", "896x1152" 
                        };
                    }
                    else
                    {
                        capabilities.SupportedImageSizes = new List<string> 
                        { 
                            "512x512", "768x768" 
                        };
                    }
                    break;

                case ModelType.FeatureExtraction:
                    capabilities.Embeddings = true;
                    capabilities.MaxTokens = 512; // Standard for embeddings
                    break;

                case ModelType.ZeroShotClassification:
                case ModelType.QuestionAnswering:
                case ModelType.TableQuestionAnswering:
                case ModelType.Summarization:
                case ModelType.Translation:
                    // Specialized tasks
                    capabilities.Chat = false;
                    capabilities.ChatStream = false;
                    capabilities.MaxTokens = 1024;
                    break;
            }

            return capabilities;
        }

        private enum ModelType
        {
            TextGeneration,
            Conversational,
            TextToImage,
            FeatureExtraction,
            ZeroShotClassification,
            QuestionAnswering,
            TableQuestionAnswering,
            Summarization,
            Translation
        }

        private class StaticModelsData
        {
            public List<StaticModelData> Models { get; set; } = new();
        }

        private class StaticModelData
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; }
            public long? Created { get; set; }
            public string? OwnedBy { get; set; }
            public string? Object { get; set; }
            public int? ContextLength { get; set; }
            public int? MaxOutputTokens { get; set; }
            public int? EmbeddingDimensions { get; set; }
            public StaticModelCapabilities? Capabilities { get; set; }
            public List<string>? SupportedImageSizes { get; set; }
            public List<string>? SupportedVideoResolutions { get; set; }
            public int? MaxVideoDurationSeconds { get; set; }
            public List<string>? SupportedVoices { get; set; }
            public List<string>? SupportedAudioFormats { get; set; }
        }

        private class StaticModelCapabilities
        {
            public bool? Chat { get; set; }
            public bool? Vision { get; set; }
            public bool? FunctionCalling { get; set; }
            public bool? JsonMode { get; set; }
            public bool? SystemMessage { get; set; }
            public bool? Embeddings { get; set; }
            public bool? ImageGeneration { get; set; }
            public bool? VideoGeneration { get; set; }
            public bool? VideoUnderstanding { get; set; }
            public bool? AudioSynthesis { get; set; }
            public bool? AudioGeneration { get; set; }
            public bool? AnimationGeneration { get; set; }
        }
    }
}