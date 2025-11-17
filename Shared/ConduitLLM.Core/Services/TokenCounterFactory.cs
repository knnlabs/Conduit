using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Factory for creating appropriate ITokenCounter implementations based on model tokenizer type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The TokenCounterFactory implements the Factory pattern to select the most appropriate
    /// token counter implementation based on a model's TokenizerType. This allows the system
    /// to use accurate, provider-specific tokenizers when available while falling back to
    /// estimation-based approaches for unsupported models.
    /// </para>
    /// <para>
    /// This architecture follows SOLID principles:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Single Responsibility: Factory only responsible for creating counters</description></item>
    ///   <item><description>Open/Closed: Easy to extend with new tokenizer types</description></item>
    ///   <item><description>Liskov Substitution: All counters implement ITokenCounter</description></item>
    ///   <item><description>Dependency Inversion: Depends on abstractions, not implementations</description></item>
    /// </list>
    /// </remarks>
    public class TokenCounterFactory
    {
        private readonly ILogger<TokenCounterFactory> _logger;
        private readonly IModelCapabilityService? _capabilityService;
        private readonly ILoggerFactory _loggerFactory;

        // Cache counter instances per tokenizer type for performance
        private readonly Dictionary<TokenizerType, ITokenCounter> _counterCache = new();
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenCounterFactory"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        /// <param name="loggerFactory">Factory for creating loggers for counter instances.</param>
        /// <param name="capabilityService">Service for retrieving model capabilities.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or loggerFactory is null.</exception>
        public TokenCounterFactory(
            ILogger<TokenCounterFactory> logger,
            ILoggerFactory loggerFactory,
            IModelCapabilityService? capabilityService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _capabilityService = capabilityService;

            if (capabilityService == null)
            {
                _logger.LogWarning("ModelCapabilityService not available in TokenCounterFactory");
            }
        }

        /// <summary>
        /// Gets the appropriate token counter for a model.
        /// </summary>
        /// <param name="modelName">The model name to get a counter for.</param>
        /// <returns>An appropriate ITokenCounter implementation.</returns>
        /// <remarks>
        /// <para>
        /// This method determines the TokenizerType for the model and returns the most
        /// appropriate counter implementation:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>TiktokenCounter for OpenAI tokenizers (cl100k_base, o200k_base, etc.)</description></item>
        ///   <item><description>FallbackTokenCounter for all other tokenizers with improved heuristics</description></item>
        /// </list>
        /// <para>
        /// Instances are cached per TokenizerType for performance.
        /// </para>
        /// </remarks>
        public async Task<ITokenCounter> GetCounterForModelAsync(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                _logger.LogWarning("Empty model name provided, using fallback counter");
                return GetOrCreateFallbackCounter();
            }

            try
            {
                // Get tokenizer type from capability service
                TokenizerType tokenizerType;
                if (_capabilityService != null)
                {
                    var tokenizerTypeStr = await _capabilityService.GetTokenizerTypeAsync(modelName);
                    if (string.IsNullOrEmpty(tokenizerTypeStr) ||
                        !Enum.TryParse<TokenizerType>(tokenizerTypeStr, true, out tokenizerType))
                    {
                        _logger.LogWarning(
                            "Could not determine tokenizer type for model {ModelName}, using fallback",
                            modelName);
                        tokenizerType = TokenizerType.Cl100KBase; // Safe default
                    }
                }
                else
                {
                    _logger.LogDebug("No capability service, inferring tokenizer type from model name {ModelName}",
                        modelName);
                    tokenizerType = InferTokenizerTypeFromModelName(modelName);
                }

                return GetCounterForTokenizerType(tokenizerType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining counter for model {ModelName}, using fallback", modelName);
                return GetOrCreateFallbackCounter();
            }
        }

        /// <summary>
        /// Gets or creates a token counter for a specific tokenizer type.
        /// </summary>
        /// <param name="tokenizerType">The tokenizer type.</param>
        /// <returns>An appropriate ITokenCounter implementation.</returns>
        /// <remarks>
        /// <para>
        /// This method implements a caching strategy where counter instances are reused
        /// for the same tokenizer type, improving performance by avoiding repeated
        /// initialization of tokenizer resources.
        /// </para>
        /// </remarks>
        public ITokenCounter GetCounterForTokenizerType(TokenizerType tokenizerType)
        {
            lock (_lock)
            {
                // Check cache first
                if (_counterCache.TryGetValue(tokenizerType, out var cachedCounter))
                {
                    return cachedCounter;
                }

                // Create appropriate counter based on tokenizer type
                ITokenCounter counter = CreateCounterForType(tokenizerType);

                // Cache the counter
                _counterCache[tokenizerType] = counter;

                _logger.LogInformation("Created and cached {CounterType} for tokenizer type {TokenizerType}",
                    counter.GetType().Name, tokenizerType);

                return counter;
            }
        }

        /// <summary>
        /// Creates a token counter instance for a specific tokenizer type.
        /// </summary>
        /// <param name="tokenizerType">The tokenizer type.</param>
        /// <returns>A new ITokenCounter implementation.</returns>
        private ITokenCounter CreateCounterForType(TokenizerType tokenizerType)
        {
            // Determine if this is an OpenAI tokenizer that TiktokenSharp can handle
            if (IsOpenAITokenizer(tokenizerType))
            {
                _logger.LogDebug("Using TiktokenCounter for tokenizer type {TokenizerType}", tokenizerType);
                return new TiktokenCounter(
                    _loggerFactory.CreateLogger<TiktokenCounter>(),
                    _capabilityService);
            }

            // For all other tokenizers, use the improved fallback counter
            _logger.LogDebug("Using FallbackTokenCounter for tokenizer type {TokenizerType}", tokenizerType);
            return new FallbackTokenCounter(
                _loggerFactory.CreateLogger<FallbackTokenCounter>(),
                _capabilityService);
        }

        /// <summary>
        /// Determines if a tokenizer type is an OpenAI tokenizer supported by TiktokenSharp.
        /// </summary>
        /// <param name="tokenizerType">The tokenizer type to check.</param>
        /// <returns>True if it's an OpenAI tokenizer, false otherwise.</returns>
        private bool IsOpenAITokenizer(TokenizerType tokenizerType)
        {
            return tokenizerType switch
            {
                TokenizerType.Cl100KBase => true,
                TokenizerType.P50KBase => true,
                TokenizerType.P50KEdit => true,
                TokenizerType.R50KBase => true,
                TokenizerType.O200KBase => true,
                TokenizerType.Tiktoken => true,
                _ => false
            };
        }

        /// <summary>
        /// Infers the tokenizer type from a model name when capability service is unavailable.
        /// </summary>
        /// <param name="modelName">The model name.</param>
        /// <returns>The inferred TokenizerType.</returns>
        /// <remarks>
        /// <para>
        /// This method provides a best-effort inference of tokenizer type based on
        /// common model naming patterns. It's used as a fallback when the capability
        /// service is unavailable.
        /// </para>
        /// </remarks>
        private TokenizerType InferTokenizerTypeFromModelName(string modelName)
        {
            string lowerModel = modelName.ToLowerInvariant();

            // OpenAI models
            if (lowerModel.Contains("gpt-4o") || lowerModel.Contains("gpt-4-turbo"))
                return TokenizerType.O200KBase;
            if (lowerModel.Contains("gpt-4") || lowerModel.Contains("gpt-3.5"))
                return TokenizerType.Cl100KBase;
            if (lowerModel.Contains("text-davinci") || lowerModel.Contains("davinci"))
                return TokenizerType.P50KBase;
            if (lowerModel.Contains("codex") || lowerModel.Contains("code-"))
                return TokenizerType.R50KBase;

            // Anthropic models
            if (lowerModel.Contains("claude-3"))
                return TokenizerType.Claude3;
            if (lowerModel.Contains("claude"))
                return TokenizerType.Claude;

            // Google models
            if (lowerModel.Contains("gemini"))
                return TokenizerType.Gemini;
            if (lowerModel.Contains("palm"))
                return TokenizerType.PaLM;

            // Meta LLaMA models
            if (lowerModel.Contains("llama-3") || lowerModel.Contains("llama3"))
                return TokenizerType.LLaMA3;
            if (lowerModel.Contains("llama-2") || lowerModel.Contains("llama2"))
                return TokenizerType.LLaMA2;
            if (lowerModel.Contains("llama"))
                return TokenizerType.LLaMA;

            // Provider-specific (often use LLaMA tokenizers)
            if (lowerModel.Contains("groq"))
                return TokenizerType.LLaMA3; // Groq typically uses LLaMA models
            if (lowerModel.Contains("cerebras"))
                return TokenizerType.LLaMA3; // Cerebras often uses LLaMA models

            // Mistral models
            if (lowerModel.Contains("mistral") || lowerModel.Contains("mixtral"))
                return TokenizerType.Mistral;

            // Default to modern OpenAI tokenizer as safest fallback
            _logger.LogDebug("Could not infer tokenizer type from model name {ModelName}, defaulting to Cl100KBase",
                modelName);
            return TokenizerType.Cl100KBase;
        }

        /// <summary>
        /// Gets or creates the fallback counter instance.
        /// </summary>
        /// <returns>The fallback counter instance.</returns>
        private ITokenCounter GetOrCreateFallbackCounter()
        {
            return GetCounterForTokenizerType(TokenizerType.Cl100KBase);
        }
    }
}
