using ConduitLLM.Configuration;
using ConduitLLM.Providers.Authentication;

namespace ConduitLLM.Providers.Configuration
{
    /// <summary>
    /// Centralized registry for provider configurations.
    /// Eliminates duplicated Constants classes across provider implementations.
    /// </summary>
    public static class ProviderConfigurationRegistry
    {
        /// <summary>
        /// Registry of provider configurations keyed by ProviderType.
        /// </summary>
        private static readonly Dictionary<ProviderType, ProviderConfiguration> Configurations = new()
        {
            [ProviderType.OpenAI] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.openai.com/v1",
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                EmbeddingsEndpoint = "/embeddings",
                ImageGenerationsEndpoint = "/images/generations",
                AudioTranscriptionsEndpoint = "/audio/transcriptions",
                AudioSpeechEndpoint = "/audio/speech",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for OpenAI. Please verify your API key is correct.",
                    RateLimitExceeded = "OpenAI API rate limit exceeded. Please try again later.",
                    InsufficientBalance = "Insufficient balance in your OpenAI account.",
                    ModelNotFound = "Model not found. Please verify the model ID is correct."
                }
            },

            [ProviderType.Groq] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.groq.com/openai/v1",
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for Groq. Please verify your API key is correct.",
                    RateLimitExceeded = "Groq API rate limit exceeded. Please try again later or reduce your request frequency.",
                    ModelNotFound = "Model not found. Available Groq models include: llama3-8b-8192, llama3-70b-8192, mixtral-8x7b-32768, gemma-7b-it"
                }
            },

            [ProviderType.Fireworks] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.fireworks.ai/inference/v1",
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                EmbeddingsEndpoint = "/embeddings",
                ImageGenerationsEndpoint = "/images/generations",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for Fireworks. Please verify your API key is correct.",
                    RateLimitExceeded = "Fireworks API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Please verify the model ID is correct."
                }
            },

            [ProviderType.Cerebras] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.cerebras.ai/v1",
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for Cerebras. Please verify your API key is correct.",
                    RateLimitExceeded = "Cerebras API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Please verify the model ID is correct.",
                    MissingApiKey = "API key is required for Cerebras"
                }
            },

            [ProviderType.SambaNova] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.sambanova.ai/v1",
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for SambaNova. Please verify your API key is correct.",
                    RateLimitExceeded = "SambaNova API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Please verify the model ID is correct."
                }
            },

            [ProviderType.DeepInfra] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.deepinfra.com/v1/openai",
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                EmbeddingsEndpoint = "/embeddings",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for DeepInfra. Please verify your API key is correct.",
                    RateLimitExceeded = "DeepInfra API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Please verify the model ID is correct."
                }
            },

            [ProviderType.Cloudflare] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1",
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                EmbeddingsEndpoint = "/embeddings",
                SupportsModelsList = false,
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API token for Cloudflare. Please verify your Cloudflare API token is correct.",
                    RateLimitExceeded = "Cloudflare Workers AI rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Cloudflare Workers AI models use the @cf/provider/model-name format.",
                    MissingApiKey = "API token is required for Cloudflare Workers AI"
                }
            },

            [ProviderType.Replicate] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.replicate.com/v1",
                ModelsEndpoint = "/models",
                HealthCheckEndpoint = "/account",
                AuthenticationStrategy = TokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API token for Replicate. Please verify your API token is correct.",
                    RateLimitExceeded = "Replicate API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Please verify the model version hash is correct."
                }
            },

            [ProviderType.MiniMax] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.minimax.chat/v1",
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/text/chatcompletion_v2",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                SupportsModelsList = false, // MiniMax doesn't support listing models
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for MiniMax. Please verify your API key is correct.",
                    RateLimitExceeded = "MiniMax API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Please verify the model ID is correct."
                }
            },

            [ProviderType.OpenAICompatible] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.openai.com/v1", // Will be overridden by provider config
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                EmbeddingsEndpoint = "/embeddings",
                ImageGenerationsEndpoint = "/images/generations",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key. Please verify your API key is correct.",
                    RateLimitExceeded = "API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Please verify the model ID is correct."
                }
            },

            [ProviderType.Ultravox] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.ultravox.ai/v1",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for Ultravox. Please verify your API key is correct.",
                    RateLimitExceeded = "Ultravox API rate limit exceeded. Please try again later."
                }
            },

            [ProviderType.ElevenLabs] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://api.elevenlabs.io/v1",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for ElevenLabs. Please verify your API key is correct.",
                    RateLimitExceeded = "ElevenLabs API rate limit exceeded. Please try again later."
                }
            },

            [ProviderType.OpenRouter] = new ProviderConfiguration
            {
                DefaultBaseUrl = "https://openrouter.ai/api/v1",
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for OpenRouter. Please verify your API key is correct.",
                    RateLimitExceeded = "OpenRouter API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. OpenRouter models use provider/model-name format (e.g., openai/gpt-4o)."
                }
            }
        };

        /// <summary>
        /// Gets the configuration for a provider type.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <returns>The provider configuration, or null if not found.</returns>
        public static ProviderConfiguration? GetConfiguration(ProviderType providerType)
        {
            return Configurations.TryGetValue(providerType, out var config) ? config : null;
        }

        /// <summary>
        /// Tries to get the configuration for a provider type.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <param name="configuration">The configuration if found.</param>
        /// <returns>True if found, false otherwise.</returns>
        public static bool TryGetConfiguration(ProviderType providerType, out ProviderConfiguration? configuration)
        {
            return Configurations.TryGetValue(providerType, out configuration);
        }

        /// <summary>
        /// Gets the default base URL for a provider type.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <returns>The default base URL, or null if not found.</returns>
        public static string? GetDefaultBaseUrl(ProviderType providerType)
        {
            return GetConfiguration(providerType)?.DefaultBaseUrl;
        }

        /// <summary>
        /// Gets the authentication strategy for a provider type.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <returns>The authentication strategy, or BearerTokenStrategy as default.</returns>
        public static IAuthenticationStrategy GetAuthenticationStrategy(ProviderType providerType)
        {
            return GetConfiguration(providerType)?.AuthenticationStrategy ?? BearerTokenStrategy.Instance;
        }

        /// <summary>
        /// Gets the health check endpoint for a provider type.
        /// Returns the models endpoint by default if no specific health check endpoint is defined.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <returns>The health check endpoint path.</returns>
        public static string GetHealthCheckEndpoint(ProviderType providerType)
        {
            var config = GetConfiguration(providerType);
            if (config == null)
            {
                return "/models";
            }

            return config.HealthCheckEndpoint ?? config.ModelsEndpoint ?? "/models";
        }

        /// <summary>
        /// Gets error messages for a provider type.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <returns>The error messages, or default messages if not found.</returns>
        public static ProviderErrorMessages GetErrorMessages(ProviderType providerType)
        {
            return GetConfiguration(providerType)?.ErrorMessages ?? ProviderErrorMessages.Default;
        }

        /// <summary>
        /// Checks if a provider supports listing models.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <returns>True if the provider supports listing models, false otherwise.</returns>
        public static bool SupportsModelsList(ProviderType providerType)
        {
            var config = GetConfiguration(providerType);
            return config?.SupportsModelsList ?? true;
        }
    }

    /// <summary>
    /// Configuration for an LLM provider.
    /// </summary>
    public record ProviderConfiguration
    {
        /// <summary>
        /// The default base URL for the provider's API.
        /// </summary>
        public required string DefaultBaseUrl { get; init; }

        /// <summary>
        /// The endpoint path for listing models (e.g., "/models").
        /// </summary>
        public string? ModelsEndpoint { get; init; }

        /// <summary>
        /// The endpoint path for chat completions (e.g., "/chat/completions").
        /// </summary>
        public string? ChatCompletionsEndpoint { get; init; }

        /// <summary>
        /// The endpoint path for embeddings (e.g., "/embeddings").
        /// </summary>
        public string? EmbeddingsEndpoint { get; init; }

        /// <summary>
        /// The endpoint path for image generations (e.g., "/images/generations").
        /// </summary>
        public string? ImageGenerationsEndpoint { get; init; }

        /// <summary>
        /// The endpoint path for audio transcriptions.
        /// </summary>
        public string? AudioTranscriptionsEndpoint { get; init; }

        /// <summary>
        /// The endpoint path for audio speech synthesis.
        /// </summary>
        public string? AudioSpeechEndpoint { get; init; }

        /// <summary>
        /// The endpoint path for health checks. If null, uses ModelsEndpoint.
        /// </summary>
        public string? HealthCheckEndpoint { get; init; }

        /// <summary>
        /// The authentication strategy to use for this provider.
        /// </summary>
        public required IAuthenticationStrategy AuthenticationStrategy { get; init; }

        /// <summary>
        /// Whether this provider supports listing available models.
        /// Defaults to true.
        /// </summary>
        public bool SupportsModelsList { get; init; } = true;

        /// <summary>
        /// Error messages specific to this provider.
        /// </summary>
        public required ProviderErrorMessages ErrorMessages { get; init; }
    }

    /// <summary>
    /// Provider-specific error messages.
    /// </summary>
    public record ProviderErrorMessages
    {
        // Default message constants to avoid circular initialization
        private const string DefaultInvalidApiKey = "Invalid API key. Please verify your API key is correct.";
        private const string DefaultRateLimitExceeded = "API rate limit exceeded. Please try again later.";
        private const string DefaultModelNotFound = "Model not found. Please verify the model ID is correct.";
        private const string DefaultInsufficientBalance = "Insufficient balance in your account.";
        private const string DefaultMissingApiKey = "API key is required.";

        /// <summary>
        /// Default error messages for unknown providers.
        /// </summary>
        public static readonly ProviderErrorMessages Default = new()
        {
            InvalidApiKey = DefaultInvalidApiKey,
            RateLimitExceeded = DefaultRateLimitExceeded,
            ModelNotFound = DefaultModelNotFound,
            InsufficientBalance = DefaultInsufficientBalance,
            MissingApiKey = DefaultMissingApiKey
        };

        /// <summary>
        /// Message for invalid API key errors.
        /// </summary>
        public string InvalidApiKey { get; init; } = DefaultInvalidApiKey;

        /// <summary>
        /// Message for rate limit exceeded errors.
        /// </summary>
        public string RateLimitExceeded { get; init; } = DefaultRateLimitExceeded;

        /// <summary>
        /// Message for model not found errors.
        /// </summary>
        public string ModelNotFound { get; init; } = DefaultModelNotFound;

        /// <summary>
        /// Message for insufficient balance errors.
        /// </summary>
        public string InsufficientBalance { get; init; } = DefaultInsufficientBalance;

        /// <summary>
        /// Message for missing API key errors.
        /// </summary>
        public string MissingApiKey { get; init; } = DefaultMissingApiKey;
    }
}
