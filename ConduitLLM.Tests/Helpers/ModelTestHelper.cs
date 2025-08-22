using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Helpers
{
    /// <summary>
    /// Helper class for creating Model entities in tests
    /// </summary>
    public static class ModelTestHelper
    {
        private static int _nextModelId = 1000;
        private static int _nextCapabilityId = 1000;

        /// <summary>
        /// Creates a GPT-4 model with chat and vision capabilities
        /// </summary>
        public static Model CreateGPT4Model(int? modelId = null)
        {
            return new Model
            {
                Id = modelId ?? _nextModelId++,
                Name = "gpt-4",
                ModelSeriesId = 1,
                ModelCapabilitiesId = _nextCapabilityId,
                Capabilities = new ModelCapabilities
                {
                    Id = _nextCapabilityId++,
                    SupportsChat = true,
                    SupportsVision = true,
                    SupportsFunctionCalling = true,
                    SupportsStreaming = true,
                    MaxTokens = 8192,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };
        }

        /// <summary>
        /// Creates a text-embedding-ada-002 model with embedding capabilities
        /// </summary>
        public static Model CreateTextEmbeddingModel(int? modelId = null)
        {
            return new Model
            {
                Id = modelId ?? _nextModelId++,
                Name = "text-embedding-ada-002",
                ModelSeriesId = 1,
                ModelCapabilitiesId = _nextCapabilityId,
                Capabilities = new ModelCapabilities
                {
                    Id = _nextCapabilityId++,
                    SupportsEmbeddings = true,
                    MaxTokens = 8192,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };
        }

        /// <summary>
        /// Creates a DALL-E 3 model with image generation capabilities
        /// </summary>
        public static Model CreateDallE3Model(int? modelId = null)
        {
            return new Model
            {
                Id = modelId ?? _nextModelId++,
                Name = "dall-e-3",
                ModelSeriesId = 2,
                ModelCapabilitiesId = _nextCapabilityId,
                Capabilities = new ModelCapabilities
                {
                    Id = _nextCapabilityId++,
                    SupportsImageGeneration = true,
                    MaxTokens = 4000,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };
        }

        /// <summary>
        /// Creates a DALL-E 2 model with image generation capabilities
        /// </summary>
        public static Model CreateDallE2Model(int? modelId = null)
        {
            return new Model
            {
                Id = modelId ?? _nextModelId++,
                Name = "dall-e-2",
                ModelSeriesId = 2,
                ModelCapabilitiesId = _nextCapabilityId,
                Capabilities = new ModelCapabilities
                {
                    Id = _nextCapabilityId++,
                    SupportsImageGeneration = true,
                    MaxTokens = 1000,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };
        }

        /// <summary>
        /// Creates a Stable Diffusion XL model
        /// </summary>
        public static Model CreateSDXLModel(int? modelId = null)
        {
            return new Model
            {
                Id = modelId ?? _nextModelId++,
                Name = "sdxl",
                ModelSeriesId = 3,
                ModelCapabilitiesId = _nextCapabilityId,
                Capabilities = new ModelCapabilities
                {
                    Id = _nextCapabilityId++,
                    SupportsImageGeneration = true,
                    MaxTokens = 77,
                    TokenizerType = TokenizerType.Cl100KBase  // Use a valid tokenizer type
                }
            };
        }

        /// <summary>
        /// Creates a MiniMax image model
        /// </summary>
        public static Model CreateMiniMaxImageModel(int? modelId = null)
        {
            return new Model
            {
                Id = modelId ?? _nextModelId++,
                Name = "minimax-image",
                ModelSeriesId = 4,
                ModelCapabilitiesId = _nextCapabilityId,
                Capabilities = new ModelCapabilities
                {
                    Id = _nextCapabilityId++,
                    SupportsImageGeneration = true,
                    MaxTokens = 1000,
                    TokenizerType = TokenizerType.Cl100KBase  // Use a valid tokenizer type
                }
            };
        }

        /// <summary>
        /// Creates a video generation model
        /// </summary>
        public static Model CreateVideoModel(int? modelId = null, string? name = null)
        {
            return new Model
            {
                Id = modelId ?? _nextModelId++,
                Name = name ?? "video-generation-model",
                ModelSeriesId = 5,
                ModelCapabilitiesId = _nextCapabilityId,
                Capabilities = new ModelCapabilities
                {
                    Id = _nextCapabilityId++,
                    SupportsVideoGeneration = true,
                    MaxTokens = 1000,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };
        }

        /// <summary>
        /// Creates model series for OpenAI models
        /// </summary>
        public static ModelSeries CreateOpenAISeries()
        {
            return new ModelSeries
            {
                Id = 1,
                Name = "OpenAI",
                Description = "OpenAI model series",
                Author = new ModelAuthor
                {
                    Id = 1,
                    Name = "OpenAI"
                }
            };
        }

        /// <summary>
        /// Creates model series for DALL-E models
        /// </summary>
        public static ModelSeries CreateDallESeries()
        {
            return new ModelSeries
            {
                Id = 2,
                Name = "DALL-E",
                Description = "DALL-E image generation models",
                Author = new ModelAuthor
                {
                    Id = 1,
                    Name = "OpenAI"
                }
            };
        }

        /// <summary>
        /// Creates model series for Stable Diffusion models
        /// </summary>
        public static ModelSeries CreateStableDiffusionSeries()
        {
            return new ModelSeries
            {
                Id = 3,
                Name = "Stable Diffusion",
                Description = "Stable Diffusion models",
                Author = new ModelAuthor
                {
                    Id = 2,
                    Name = "Stability AI"
                }
            };
        }

        /// <summary>
        /// Creates model series for MiniMax models
        /// </summary>
        public static ModelSeries CreateMiniMaxSeries()
        {
            return new ModelSeries
            {
                Id = 4,
                Name = "MiniMax",
                Description = "MiniMax models",
                Author = new ModelAuthor
                {
                    Id = 3,
                    Name = "MiniMax"
                }
            };
        }

        /// <summary>
        /// Creates a ModelProviderMapping for a given model
        /// </summary>
        public static ModelProviderMapping CreateMapping(Model model, int providerId, string? modelAlias = null)
        {
            return new ModelProviderMapping
            {
                Id = _nextModelId++,
                ModelId = model.Id,
                Model = model,
                ModelAlias = modelAlias ?? model.Name,
                ProviderModelId = model.Name,
                ProviderId = providerId,
                IsEnabled = true
            };
        }

        /// <summary>
        /// Creates a complete model with author, series, and capabilities for testing
        /// </summary>
        public static Model CreateCompleteTestModel(
            string? modelName = null,
            bool supportsChat = true,
            bool supportsVideoGeneration = false,
            int maxTokens = 4096)
        {
            var modelId = _nextModelId++;
            var capabilityId = _nextCapabilityId++;
            var authorId = _nextModelId++;
            var seriesId = _nextModelId++;

            return new Model
            {
                Id = modelId,
                Name = modelName ?? $"test-model-{modelId}",
                ModelSeriesId = seriesId,
                ModelCapabilitiesId = capabilityId,
                Series = new ModelSeries
                {
                    Id = seriesId,
                    Name = $"Test Series {seriesId}",
                    AuthorId = authorId,
                    Author = new ModelAuthor
                    {
                        Id = authorId,
                        Name = $"Test Author {authorId}"
                    },
                    TokenizerType = TokenizerType.Cl100KBase
                },
                Capabilities = new ModelCapabilities
                {
                    Id = capabilityId,
                    SupportsChat = supportsChat,
                    SupportsVideoGeneration = supportsVideoGeneration,
                    MaxTokens = maxTokens,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };
        }

        /// <summary>
        /// Reset ID counters for test isolation
        /// </summary>
        public static void ResetCounters()
        {
            _nextModelId = 1000;
            _nextCapabilityId = 1000;
        }
    }
}