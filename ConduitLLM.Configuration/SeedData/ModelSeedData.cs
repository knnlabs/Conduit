using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.SeedData
{
    /// <summary>
    /// Seed data for common AI models.
    /// This class provides pre-configured models that are commonly used across providers.
    /// </summary>
    public static class ModelSeedData
    {
        // Authors
        public static readonly ModelAuthor OpenAI = new() { Id = 1, Name = "OpenAI", Description = "OpenAI, creator of GPT models", WebsiteUrl = "https://openai.com" };
        public static readonly ModelAuthor Anthropic = new() { Id = 2, Name = "Anthropic", Description = "Anthropic, creator of Claude models", WebsiteUrl = "https://anthropic.com" };
        public static readonly ModelAuthor Meta = new() { Id = 3, Name = "Meta", Description = "Meta AI, creator of Llama models", WebsiteUrl = "https://ai.meta.com" };
        public static readonly ModelAuthor Google = new() { Id = 4, Name = "Google", Description = "Google AI, creator of Gemini models", WebsiteUrl = "https://ai.google" };
        public static readonly ModelAuthor Mistral = new() { Id = 5, Name = "Mistral AI", Description = "Mistral AI, creator of Mistral models", WebsiteUrl = "https://mistral.ai" };
        public static readonly ModelAuthor StabilityAI = new() { Id = 6, Name = "Stability AI", Description = "Stability AI, creator of Stable Diffusion", WebsiteUrl = "https://stability.ai" };
        public static readonly ModelAuthor Cohere = new() { Id = 7, Name = "Cohere", Description = "Cohere, creator of Command models", WebsiteUrl = "https://cohere.com" };
        public static readonly ModelAuthor Runway = new() { Id = 8, Name = "Runway", Description = "Runway, creator of Gen video models", WebsiteUrl = "https://runwayml.com" };

        // Capabilities - Shared capabilities for similar models
        public static readonly ModelCapabilities GPT4TurboCapabilities = new()
        {
            Id = 1,
            MaxTokens = 128000,
            MinTokens = 1,
            SupportsChat = true,
            SupportsFunctionCalling = true,
            SupportsStreaming = true,
            SupportsVision = true,
            TokenizerType = TokenizerType.Cl100KBase
        };

        public static readonly ModelCapabilities GPT4Capabilities = new()
        {
            Id = 2,
            MaxTokens = 8192,
            MinTokens = 1,
            SupportsChat = true,
            SupportsFunctionCalling = true,
            SupportsStreaming = true,
            SupportsVision = false,
            TokenizerType = TokenizerType.Cl100KBase
        };

        public static readonly ModelCapabilities GPT35Capabilities = new()
        {
            Id = 3,
            MaxTokens = 16384,
            MinTokens = 1,
            SupportsChat = true,
            SupportsFunctionCalling = true,
            SupportsStreaming = true,
            SupportsVision = false,
            TokenizerType = TokenizerType.Cl100KBase
        };

        public static readonly ModelCapabilities Claude3OpusCapabilities = new()
        {
            Id = 4,
            MaxTokens = 200000,
            MinTokens = 1,
            SupportsChat = true,
            SupportsFunctionCalling = true,
            SupportsStreaming = true,
            SupportsVision = true,
            TokenizerType = TokenizerType.Claude3
        };

        public static readonly ModelCapabilities Claude3SonnetCapabilities = new()
        {
            Id = 5,
            MaxTokens = 200000,
            MinTokens = 1,
            SupportsChat = true,
            SupportsFunctionCalling = true,
            SupportsStreaming = true,
            SupportsVision = true,
            TokenizerType = TokenizerType.Claude3
        };

        public static readonly ModelCapabilities Llama3Capabilities = new()
        {
            Id = 6,
            MaxTokens = 8192,
            MinTokens = 1,
            SupportsChat = true,
            SupportsFunctionCalling = false,
            SupportsStreaming = true,
            SupportsVision = false,
            TokenizerType = TokenizerType.LLaMA3
        };

        public static readonly ModelCapabilities DallE3Capabilities = new()
        {
            Id = 7,
            SupportsImageGeneration = true,
            SupportsChat = false
        };

        public static readonly ModelCapabilities WhisperCapabilities = new()
        {
            Id = 8,
            SupportsAudioTranscription = true,
            SupportsChat = false,
            SupportedLanguages = "[\"en\",\"es\",\"fr\",\"de\",\"it\",\"pt\",\"ru\",\"ja\",\"ko\",\"zh\"]",
            SupportedFormats = "[\"mp3\",\"mp4\",\"mpeg\",\"mpga\",\"m4a\",\"wav\",\"webm\"]"
        };

        public static readonly ModelCapabilities TTSCapabilities = new()
        {
            Id = 9,
            SupportsTextToSpeech = true,
            SupportsChat = false,
            SupportedVoices = "[\"alloy\",\"echo\",\"fable\",\"onyx\",\"nova\",\"shimmer\"]",
            SupportedFormats = "[\"mp3\",\"opus\",\"aac\",\"flac\",\"wav\",\"pcm\"]"
        };

        public static readonly ModelCapabilities EmbeddingCapabilities = new()
        {
            Id = 10,
            SupportsEmbeddings = true,
            SupportsChat = false,
            MaxTokens = 8191,
            TokenizerType = TokenizerType.Cl100KBase
        };

        // Model Series
        public static readonly ModelSeries GPT4Series = new()
        {
            Id = 1,
            AuthorId = 1,
            Name = "GPT-4",
            Description = "OpenAI's most capable language model series",
            TokenizerType = TokenizerType.Cl100KBase,
            Parameters = "{}"
        };

        public static readonly ModelSeries GPT35Series = new()
        {
            Id = 2,
            AuthorId = 1,
            Name = "GPT-3.5",
            Description = "OpenAI's fast and efficient language model series",
            TokenizerType = TokenizerType.Cl100KBase,
            Parameters = "{}"
        };

        public static readonly ModelSeries Claude3Series = new()
        {
            Id = 3,
            AuthorId = 2,
            Name = "Claude 3",
            Description = "Anthropic's advanced language model series",
            TokenizerType = TokenizerType.Claude3,
            Parameters = "{}"
        };

        public static readonly ModelSeries LlamaSeies = new()
        {
            Id = 4,
            AuthorId = 3,
            Name = "Llama 3",
            Description = "Meta's open-source language model series",
            TokenizerType = TokenizerType.LLaMA3,
            Parameters = "{}"
        };

        public static readonly ModelSeries DallESeries = new()
        {
            Id = 5,
            AuthorId = 1,
            Name = "DALL-E",
            Description = "OpenAI's image generation model series",
            TokenizerType = TokenizerType.Cl100KBase,
            Parameters = "{}"
        };

        // Models
        public static readonly Model GPT4Turbo = new()
        {
            Id = 1,
            Name = "GPT-4 Turbo",
            ModelSeriesId = 1,
            ModelCapabilitiesId = 1,
            ModelType = ModelType.Text,
            Description = "Latest GPT-4 Turbo with 128K context, knowledge cutoff April 2023"
        };

        public static readonly Model GPT4 = new()
        {
            Id = 2,
            Name = "GPT-4",
            ModelSeriesId = 1,
            ModelCapabilitiesId = 2,
            ModelType = ModelType.Text,
            Description = "Original GPT-4 with 8K context"
        };

        public static readonly Model GPT35Turbo = new()
        {
            Id = 3,
            Name = "GPT-3.5 Turbo",
            ModelSeriesId = 2,
            ModelCapabilitiesId = 3,
            ModelType = ModelType.Text,
            Description = "Fast and efficient model for most tasks"
        };

        public static readonly Model Claude3Opus = new()
        {
            Id = 4,
            Name = "Claude 3 Opus",
            ModelSeriesId = 3,
            ModelCapabilitiesId = 4,
            ModelType = ModelType.Text,
            Description = "Most capable Claude model"
        };

        public static readonly Model Claude3Sonnet = new()
        {
            Id = 5,
            Name = "Claude 3 Sonnet",
            ModelSeriesId = 3,
            ModelCapabilitiesId = 5,
            ModelType = ModelType.Text,
            Description = "Balanced Claude model for most tasks"
        };

        public static readonly Model Llama3_70B = new()
        {
            Id = 6,
            Name = "Llama 3 70B",
            ModelSeriesId = 4,
            ModelCapabilitiesId = 6,
            ModelType = ModelType.Text,
            Description = "Meta's largest Llama 3 model"
        };

        public static readonly Model DallE3 = new()
        {
            Id = 7,
            Name = "DALL-E 3",
            ModelSeriesId = 5,
            ModelCapabilitiesId = 7,
            ModelType = ModelType.Image,
            Description = "OpenAI's latest image generation model"
        };

        public static readonly Model Whisper = new()
        {
            Id = 8,
            Name = "Whisper",
            ModelSeriesId = 2, // Using GPT-3.5 series for now
            ModelCapabilitiesId = 8,
            ModelType = ModelType.Audio,
            Description = "OpenAI's speech recognition model"
        };

        public static readonly Model TTS1HD = new()
        {
            Id = 9,
            Name = "TTS-1-HD",
            ModelSeriesId = 2, // Using GPT-3.5 series for now
            ModelCapabilitiesId = 9,
            ModelType = ModelType.Audio,
            Description = "OpenAI's high-quality text-to-speech model"
        };

        public static readonly Model TextEmbedding3Large = new()
        {
            Id = 10,
            Name = "text-embedding-3-large",
            ModelSeriesId = 2,
            ModelCapabilitiesId = 10,
            ModelType = ModelType.Embedding,
            Description = "OpenAI's large embedding model"
        };

        // Model Identifiers - Map various names to canonical models
        public static List<ModelIdentifier> GetModelIdentifiers()
        {
            return new List<ModelIdentifier>
            {
                // GPT-4 Turbo identifiers
                new() { Id = 1, ModelId = 1, Identifier = "gpt-4-turbo", Provider = "openai", IsPrimary = true },
                new() { Id = 2, ModelId = 1, Identifier = "gpt-4-turbo-preview", Provider = "openai" },
                new() { Id = 3, ModelId = 1, Identifier = "gpt-4-0125-preview", Provider = "openai" },
                new() { Id = 4, ModelId = 1, Identifier = "gpt-4-1106-preview", Provider = "openai" },
                new() { Id = 5, ModelId = 1, Identifier = "gpt-4-vision-preview", Provider = "openai" },

                // GPT-4 identifiers
                new() { Id = 6, ModelId = 2, Identifier = "gpt-4", Provider = "openai", IsPrimary = true },
                new() { Id = 7, ModelId = 2, Identifier = "gpt-4-0613", Provider = "openai" },
                new() { Id = 8, ModelId = 2, Identifier = "gpt-4-0314", Provider = "openai" },

                // GPT-3.5 Turbo identifiers
                new() { Id = 9, ModelId = 3, Identifier = "gpt-3.5-turbo", Provider = "openai", IsPrimary = true },
                new() { Id = 10, ModelId = 3, Identifier = "gpt-3.5-turbo-0125", Provider = "openai" },
                new() { Id = 11, ModelId = 3, Identifier = "gpt-3.5-turbo-1106", Provider = "openai" },
                new() { Id = 12, ModelId = 3, Identifier = "gpt-3.5-turbo-16k", Provider = "openai" },

                // Claude 3 Opus identifiers
                new() { Id = 13, ModelId = 4, Identifier = "claude-3-opus", Provider = "anthropic", IsPrimary = true },
                new() { Id = 14, ModelId = 4, Identifier = "claude-3-opus-20240229", Provider = "anthropic" },
                new() { Id = 15, ModelId = 4, Identifier = "anthropic.claude-3-opus", Provider = "aws" },

                // Claude 3 Sonnet identifiers
                new() { Id = 16, ModelId = 5, Identifier = "claude-3-sonnet", Provider = "anthropic", IsPrimary = true },
                new() { Id = 17, ModelId = 5, Identifier = "claude-3-sonnet-20240229", Provider = "anthropic" },
                new() { Id = 18, ModelId = 5, Identifier = "anthropic.claude-3-sonnet", Provider = "aws" },

                // Llama 3 70B identifiers
                new() { Id = 19, ModelId = 6, Identifier = "llama-3-70b", Provider = null, IsPrimary = true },
                new() { Id = 20, ModelId = 6, Identifier = "meta-llama/Llama-3-70b-chat-hf", Provider = "huggingface" },
                new() { Id = 21, ModelId = 6, Identifier = "meta-llama/Meta-Llama-3-70B-Instruct", Provider = "together" },
                new() { Id = 22, ModelId = 6, Identifier = "llama3-70b-8192", Provider = "groq" },

                // DALL-E 3 identifiers
                new() { Id = 23, ModelId = 7, Identifier = "dall-e-3", Provider = "openai", IsPrimary = true },

                // Whisper identifiers
                new() { Id = 24, ModelId = 8, Identifier = "whisper-1", Provider = "openai", IsPrimary = true },

                // TTS identifiers
                new() { Id = 25, ModelId = 9, Identifier = "tts-1-hd", Provider = "openai", IsPrimary = true },
                new() { Id = 26, ModelId = 9, Identifier = "tts-1-hd-1106", Provider = "openai" },

                // Embedding identifiers
                new() { Id = 27, ModelId = 10, Identifier = "text-embedding-3-large", Provider = "openai", IsPrimary = true },
                new() { Id = 28, ModelId = 10, Identifier = "text-embedding-ada-002", Provider = "openai" }
            };
        }

        /// <summary>
        /// Gets all seed data entities in dependency order.
        /// </summary>
        public static (
            List<ModelAuthor> Authors,
            List<ModelCapabilities> Capabilities,
            List<ModelSeries> Series,
            List<Model> Models,
            List<ModelIdentifier> Identifiers
        ) GetAllSeedData()
        {
            var authors = new List<ModelAuthor> { OpenAI, Anthropic, Meta, Google, Mistral, StabilityAI, Cohere, Runway };
            
            var capabilities = new List<ModelCapabilities>
            {
                GPT4TurboCapabilities, GPT4Capabilities, GPT35Capabilities,
                Claude3OpusCapabilities, Claude3SonnetCapabilities,
                Llama3Capabilities, DallE3Capabilities,
                WhisperCapabilities, TTSCapabilities, EmbeddingCapabilities
            };
            
            var series = new List<ModelSeries>
            {
                GPT4Series, GPT35Series, Claude3Series, LlamaSeies, DallESeries
            };
            
            var models = new List<Model>
            {
                GPT4Turbo, GPT4, GPT35Turbo,
                Claude3Opus, Claude3Sonnet,
                Llama3_70B, DallE3,
                Whisper, TTS1HD, TextEmbedding3Large
            };
            
            var identifiers = GetModelIdentifiers();

            return (authors, capabilities, series, models, identifiers);
        }
    }
}