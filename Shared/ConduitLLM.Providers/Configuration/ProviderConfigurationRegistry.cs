using System.Text.RegularExpressions;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Providers;
using ConduitLLM.Core.Exceptions;
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
        /// The Azure OpenAI REST API version used when an operator supplies none. Declared as the
        /// <c>api_version</c> setting's default so it is visible and overridable per provider rather
        /// than pinned in client code.
        /// </summary>
        public const string AzureDefaultApiVersion = "2024-02-01";

        /// <summary>
        /// Registry of provider configurations keyed by ProviderType.
        /// </summary>
        private static readonly Dictionary<ProviderType, ProviderConfiguration> Configurations = new()
        {
            [ProviderType.OpenAI] = new ProviderConfiguration
            {
                DefaultBaseUrl = DefaultUrl(ProviderType.OpenAI),
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
                },
                Settings = new[]
                {
                    new ProviderSettingDefinition
                    {
                        Key = "organization",
                        Label = "Organization ID",
                        HelpText = "Scopes requests and usage attribution to a specific OpenAI organization. Leave blank to use the API key's default organization.",
                        Placeholder = "org-...",
                        Required = false,
                        Binding = ProviderSettingBinding.Header,
                        BindingTarget = "OpenAI-Organization",
                        ValidationRegex = "^org-[A-Za-z0-9]+$"
                    },
                    new ProviderSettingDefinition
                    {
                        Key = "project",
                        Label = "Project ID",
                        HelpText = "Scopes requests and usage attribution to a specific project within the organization. Leave blank to use the API key's default project.",
                        Placeholder = "proj_...",
                        Required = false,
                        Binding = ProviderSettingBinding.Header,
                        BindingTarget = "OpenAI-Project",
                        ValidationRegex = "^proj_[A-Za-z0-9]+$"
                    }
                }
            },

            [ProviderType.Groq] = new ProviderConfiguration
            {
                DefaultBaseUrl = DefaultUrl(ProviderType.Groq),
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
                DefaultBaseUrl = DefaultUrl(ProviderType.Fireworks),
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
                DefaultBaseUrl = DefaultUrl(ProviderType.Cerebras),
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
                DefaultBaseUrl = DefaultUrl(ProviderType.SambaNova),
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
                DefaultBaseUrl = DefaultUrl(ProviderType.DeepInfra),
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
                DefaultBaseUrl = DefaultUrl(ProviderType.Cloudflare),
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                EmbeddingsEndpoint = "/embeddings",
                // Model discovery is supported via the native /ai/models/search endpoint
                // (see CloudflareClient.GetModelsAsync), not the OpenAI-style /models path.
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API token for Cloudflare. Please verify your Cloudflare API token is correct.",
                    RateLimitExceeded = "Cloudflare Workers AI rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Cloudflare Workers AI models use the @cf/provider/model-name format.",
                    MissingApiKey = "API token is required for Cloudflare Workers AI"
                },
                Settings = new[]
                {
                    new ProviderSettingDefinition
                    {
                        Key = "account_id",
                        Label = "Account ID",
                        HelpText = "Your Cloudflare account ID (shown in the dashboard URL and on the Workers AI page). Used to build the API base URL.",
                        Placeholder = "e.g. 0123456789abcdef0123456789abcdef",
                        Required = true,
                        Binding = ProviderSettingBinding.UrlPathToken,
                        BindingTarget = "account_id",
                        ValidationRegex = "^[0-9a-fA-F]{32}$"
                    }
                }
            },

            [ProviderType.Replicate] = new ProviderConfiguration
            {
                DefaultBaseUrl = DefaultUrl(ProviderType.Replicate),
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
                DefaultBaseUrl = DefaultUrl(ProviderType.MiniMax),
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
                DefaultBaseUrl = DefaultUrl(ProviderType.OpenAICompatible), // Explicit provider URL remains required.
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
                DefaultBaseUrl = DefaultUrl(ProviderType.Ultravox),
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for Ultravox. Please verify your API key is correct.",
                    RateLimitExceeded = "Ultravox API rate limit exceeded. Please try again later."
                }
            },

            [ProviderType.ElevenLabs] = new ProviderConfiguration
            {
                DefaultBaseUrl = DefaultUrl(ProviderType.ElevenLabs),
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for ElevenLabs. Please verify your API key is correct.",
                    RateLimitExceeded = "ElevenLabs API rate limit exceeded. Please try again later."
                }
            },

            [ProviderType.OpenRouter] = new ProviderConfiguration
            {
                DefaultBaseUrl = DefaultUrl(ProviderType.OpenRouter),
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for OpenRouter. Please verify your API key is correct.",
                    RateLimitExceeded = "OpenRouter API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. OpenRouter models use provider/model-name format (e.g., openai/gpt-4o)."
                }
            },

            // Azure OpenAI is deployment-scoped: every operation lives under
            // /openai/deployments/{deployment}/... on a per-resource host, and every request carries
            // an api-version. The deployment is per-model and comes from the model mapping's
            // provider model ID; the resource and api-version are provider-scoped and declared here.
            [ProviderType.Azure] = new ProviderConfiguration
            {
                DefaultBaseUrl = DefaultUrl(ProviderType.Azure),
                ModelsEndpoint = "/openai/deployments",
                ChatCompletionsEndpoint = "/chat/completions",
                EmbeddingsEndpoint = "/embeddings",
                ImageGenerationsEndpoint = "/images/generations",
                AuthenticationStrategy = ApiKeyHeaderStrategy.AzureInstance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for Azure OpenAI. Please verify the key from your Azure OpenAI resource.",
                    RateLimitExceeded = "Azure OpenAI rate limit exceeded. Please try again later or raise the deployment's quota.",
                    ModelNotFound = "Deployment not found. Azure addresses models by deployment name, not model name - verify the deployment exists on this resource.",
                    MissingApiKey = "API key is required for Azure OpenAI"
                },
                Settings = new[]
                {
                    new ProviderSettingDefinition
                    {
                        Key = "resource_name",
                        Label = "Resource Name",
                        HelpText = "The name of your Azure OpenAI resource, as it appears in the portal. Used to build the endpoint https://<resource>.openai.azure.com. Set a custom API endpoint instead if your resource uses a private or custom domain.",
                        Placeholder = "e.g. my-openai-resource",
                        Required = true,
                        Binding = ProviderSettingBinding.UrlPathToken,
                        BindingTarget = "resource_name",
                        ValidationRegex = "^[A-Za-z0-9][A-Za-z0-9-]{1,62}$"
                    },
                    new ProviderSettingDefinition
                    {
                        Key = "api_version",
                        Label = "API Version",
                        HelpText = "The Azure OpenAI REST API version sent with every request. Leave blank to use the version Conduit was tested against.",
                        Placeholder = AzureDefaultApiVersion,
                        Required = false,
                        Binding = ProviderSettingBinding.QueryParam,
                        BindingTarget = "api-version",
                        DefaultValue = AzureDefaultApiVersion,
                        ValidationRegex = "^[0-9]{4}-[0-9]{2}-[0-9]{2}(-preview)?$"
                    }
                }
            },

            [ProviderType.Meta] = new ProviderConfiguration
            {
                DefaultBaseUrl = DefaultUrl(ProviderType.Meta),
                ModelsEndpoint = "/models",
                ChatCompletionsEndpoint = "/chat/completions",
                AuthenticationStrategy = BearerTokenStrategy.Instance,
                ErrorMessages = new ProviderErrorMessages
                {
                    InvalidApiKey = "Invalid API key for Meta. Please verify your API key is correct.",
                    RateLimitExceeded = "Meta API rate limit exceeded. Please try again later.",
                    ModelNotFound = "Model not found. Please verify the Meta model ID is correct."
                }
            }
        };

        private static string DefaultUrl(ProviderType providerType) =>
            ProviderAdapterDefaultsRegistry.GetRequired(providerType).DefaultBaseUrl;

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
        /// Gets every provider type with immutable adapter configuration.
        /// </summary>
        public static IReadOnlyCollection<ProviderType> GetRegisteredProviderTypes() =>
            Configurations.Keys.ToArray();

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
        /// Resolves the effective base URL from the provider's structured settings, falling back to
        /// the operator's raw database override.
        /// </summary>
        /// <remarks>
        /// Dual-read (issue #1183). Before structured settings existed, provider-scoped identifiers
        /// could only be supplied by hand-crafting the whole URL, so an existing base URL is often
        /// nothing more than the registered default with those identifiers baked in. When it takes
        /// that shape and the settings supply every identifier, the settings win — otherwise editing
        /// the Account ID field would appear to save but never reach the wire. A base URL pointing
        /// anywhere else is a deliberate override (a proxy, a private gateway) and still takes
        /// precedence.
        /// </remarks>
        public static string ResolveBaseUrl(Provider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            var defaultBaseUrl = GetDefaultBaseUrl(provider.ProviderType);
            if (string.IsNullOrWhiteSpace(provider.BaseUrl))
            {
                var fallback = defaultBaseUrl
                    ?? throw new InvalidOperationException($"No default base URL is registered for {provider.ProviderType}.");
                return ApplyUrlPathTokens(fallback, provider.ProviderType, provider.Settings);
            }

            var rawBaseUrl = provider.BaseUrl.TrimEnd('/');
            if (defaultBaseUrl != null
                && SuppliesEveryUrlPathToken(provider.ProviderType, provider.Settings)
                && MatchesDefaultUrlShape(rawBaseUrl, defaultBaseUrl))
            {
                rawBaseUrl = defaultBaseUrl;
            }

            return ApplyUrlPathTokens(rawBaseUrl, provider.ProviderType, provider.Settings);
        }

        /// <summary>
        /// Whether the provider type declares at least one URL-path-token setting and the supplied
        /// settings give every one of them a value.
        /// </summary>
        private static bool SuppliesEveryUrlPathToken(
            ProviderType providerType,
            IReadOnlyDictionary<string, string>? settings)
        {
            var tokens = GetConfiguration(providerType)?.Settings
                .Where(definition => definition.Binding == ProviderSettingBinding.UrlPathToken)
                .ToList();

            return tokens is { Count: > 0 }
                && tokens.TrueForAll(definition =>
                    settings != null
                    && settings.TryGetValue(definition.Key, out var value)
                    && !string.IsNullOrWhiteSpace(value));
        }

        /// <summary>
        /// Whether a stored base URL is the registered default with its <c>{token}</c> segments
        /// filled in — that is, a URL that carries no information the settings do not already hold.
        /// Compared segment by segment so a different host, scheme or path depth is never mistaken
        /// for the default.
        /// </summary>
        private static bool MatchesDefaultUrlShape(string baseUrl, string defaultBaseUrl)
        {
            var actual = baseUrl.Split('/');
            var template = defaultBaseUrl.TrimEnd('/').Split('/');
            if (actual.Length != template.Length)
            {
                return false;
            }

            for (var index = 0; index < template.Length; index++)
            {
                var segment = template[index];
                if (segment.Length > 2 && segment[0] == '{' && segment[^1] == '}')
                {
                    if (string.IsNullOrEmpty(actual[index]))
                    {
                        return false;
                    }

                    continue;
                }

                if (!string.Equals(segment, actual[index], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly Regex UnresolvedTokenPattern =
            new(@"\{([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Substitutes <c>{token}</c> placeholders in a base URL using the provider's structured
        /// settings (for example Cloudflare's <c>{account_id}</c>). Any placeholder left unresolved
        /// means a required setting was not supplied, which raises an actionable configuration error
        /// rather than allowing a malformed request to be sent.
        /// </summary>
        /// <param name="baseUrl">The raw base URL, possibly containing <c>{token}</c> placeholders.</param>
        /// <param name="providerType">The provider type whose setting definitions drive substitution.</param>
        /// <param name="settings">The operator-supplied setting values, keyed by setting key.</param>
        /// <returns>The base URL with all URL-path-token settings substituted.</returns>
        /// <exception cref="ConfigurationException">Thrown when a required <c>{token}</c> is unresolved.</exception>
        public static string ApplyUrlPathTokens(
            string baseUrl,
            ProviderType providerType,
            IReadOnlyDictionary<string, string>? settings)
        {
            var definitions = GetConfiguration(providerType)?.Settings
                ?? (IReadOnlyList<ProviderSettingDefinition>)Array.Empty<ProviderSettingDefinition>();

            var result = baseUrl;
            foreach (var definition in definitions)
            {
                if (definition.Binding != ProviderSettingBinding.UrlPathToken)
                {
                    continue;
                }

                if (settings != null
                    && settings.TryGetValue(definition.Key, out var value)
                    && !string.IsNullOrWhiteSpace(value))
                {
                    result = result.Replace("{" + definition.EffectiveBindingTarget + "}", value.Trim());
                }
            }

            var unresolved = UnresolvedTokenPattern.Matches(result)
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unresolved.Count > 0)
            {
                var missing = string.Join(", ", unresolved.Select(token => DescribeSetting(providerType, token)));
                throw new ConfigurationException(
                    $"{providerType} is missing required configuration: {missing}. "
                    + "Provide the value in the provider settings.");
            }

            return result.TrimEnd('/');
        }

        /// <summary>
        /// Resolves the value of a single structured setting, falling back to the value declared in
        /// the registry when the operator supplied none.
        /// </summary>
        /// <param name="providerType">The provider type whose setting definitions are consulted.</param>
        /// <param name="settings">The operator-supplied setting values, keyed by setting key.</param>
        /// <param name="key">The setting key to resolve.</param>
        /// <returns>The effective value, or null when neither a value nor a default exists.</returns>
        public static string? GetSettingValue(
            ProviderType providerType,
            IReadOnlyDictionary<string, string>? settings,
            string key)
        {
            if (settings != null && settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            var definition = GetConfiguration(providerType)?.Settings
                .FirstOrDefault(setting => string.Equals(setting.Key, key, StringComparison.Ordinal));

            return string.IsNullOrWhiteSpace(definition?.DefaultValue) ? null : definition!.DefaultValue;
        }

        /// <summary>
        /// Resolves the HTTP headers a provider's structured settings contribute to every outbound
        /// request (for example OpenAI's <c>OpenAI-Organization</c>).
        /// </summary>
        /// <param name="providerType">The provider type whose setting definitions drive the mapping.</param>
        /// <param name="settings">The operator-supplied setting values, keyed by setting key.</param>
        /// <returns>Header name/value pairs; empty when the provider declares or supplies none.</returns>
        public static IReadOnlyList<KeyValuePair<string, string>> GetHeaderSettings(
            ProviderType providerType,
            IReadOnlyDictionary<string, string>? settings)
        {
            if (settings == null || settings.Count == 0)
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            var definitions = GetConfiguration(providerType)?.Settings;
            if (definitions == null || definitions.Count == 0)
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            var headers = new List<KeyValuePair<string, string>>();
            foreach (var definition in definitions)
            {
                if (definition.Binding != ProviderSettingBinding.Header)
                {
                    continue;
                }

                if (settings.TryGetValue(definition.Key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    headers.Add(new KeyValuePair<string, string>(definition.EffectiveBindingTarget, value.Trim()));
                }
            }

            return headers;
        }

        /// <summary>
        /// Resolves a human-readable label for an unresolved URL token, preferring the declared
        /// setting label and falling back to the raw token name.
        /// </summary>
        private static string DescribeSetting(ProviderType providerType, string token)
        {
            var definition = GetConfiguration(providerType)?.Settings
                .FirstOrDefault(setting =>
                    string.Equals(setting.EffectiveBindingTarget, token, StringComparison.OrdinalIgnoreCase));

            return definition?.Label ?? token;
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

        /// <summary>
        /// Structured, provider-scoped settings the operator supplies in addition to the API key
        /// (for example a Cloudflare account ID). Empty for providers that need only a key and URL.
        /// </summary>
        public IReadOnlyList<ProviderSettingDefinition> Settings { get; init; } = Array.Empty<ProviderSettingDefinition>();
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
