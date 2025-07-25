using System;
using System.Linq;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Decorators;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces; // Use the original interface location
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging; // Added for ILoggerFactory
using Microsoft.Extensions.Options;

namespace ConduitLLM.Providers;

/// <summary>
/// Factory responsible for creating ILLMClient instances based on configuration.
/// </summary>
public class LLMClientFactory : ILLMClientFactory
{
    private readonly ConduitSettings _settings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPerformanceMetricsService? _performanceMetricsService;
    private readonly IModelCapabilityService? _capabilityService;

    // Removed compatibility flag as migration is now complete

    /// <summary>
    /// Initializes a new instance of the <see cref="LLMClientFactory"/> class.
    /// </summary>
    /// <param name="settingsOptions">The configuration settings.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="performanceMetricsService">Optional performance metrics service.</param>
    /// <param name="capabilityService">Optional model capability service.</param>
    public LLMClientFactory(
        IOptions<ConduitSettings> settingsOptions,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IPerformanceMetricsService? performanceMetricsService = null,
        IModelCapabilityService? capabilityService = null)
    {
        _settings = settingsOptions.Value ?? throw new ArgumentNullException(nameof(settingsOptions), "Conduit settings cannot be null.");
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _performanceMetricsService = performanceMetricsService;
        _capabilityService = capabilityService;
    }

    /// <inheritdoc />
    public ILLMClient GetClient(string modelAlias)
    {
        if (string.IsNullOrWhiteSpace(modelAlias))
        {
            throw new ArgumentException("Model alias cannot be null or whitespace.", nameof(modelAlias));
        }

        // Log available mappings for debugging
        var logger = _loggerFactory.CreateLogger<LLMClientFactory>();
        logger.LogDebug("GetClient called for model alias: {ModelAlias}", modelAlias);
        logger.LogDebug("Total model mappings available: {Count}", _settings.ModelMappings?.Count ?? 0);
        
        if (_settings.ModelMappings != null && _settings.ModelMappings.Any())
        {
            foreach (var m in _settings.ModelMappings)
            {
                logger.LogDebug("Available mapping: {ModelAlias} -> {ProviderType}/{ProviderModelId}", 
                    m.ModelAlias, m.ProviderType, m.ProviderModelId);
            }
        }

        // 1. Find the model mapping
        var mapping = _settings.ModelMappings?.FirstOrDefault(m =>
            string.Equals(m.ModelAlias, modelAlias, StringComparison.OrdinalIgnoreCase));

        if (mapping == null)
        {
            logger.LogError("No model mapping found for alias '{ModelAlias}'. Available aliases: {AvailableAliases}", 
                modelAlias, 
                string.Join(", ", _settings.ModelMappings?.Select(m => m.ModelAlias) ?? Enumerable.Empty<string>()));
            throw new ConfigurationException($"No model mapping found for alias '{modelAlias}'. Please check your Conduit configuration.");
        }

        // 2. Find the provider credentials using the ProviderType from the mapping
        var credentials = _settings.ProviderCredentials?.FirstOrDefault(p =>
            p.ProviderType == mapping.ProviderType);

        if (credentials == null)
        {
            throw new ConfigurationException($"No provider credentials found for provider '{mapping.ProviderType}' (required by model alias '{modelAlias}'). Please check your Conduit configuration.");
        }

        // 3. Instantiate the appropriate client based on ProviderType
        return CreateClientForProvider(mapping.ProviderType.ToString(), credentials, mapping.ProviderModelId);
    }

    /// <inheritdoc />
    public ILLMClient GetClientByProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
        }

        // Find the provider credentials
        var credentials = _settings.ProviderCredentials?.FirstOrDefault(p =>
            string.Equals(p.ProviderType.ToString(), providerName, StringComparison.OrdinalIgnoreCase));

        if (credentials == null)
        {
            throw new ConfigurationException($"No provider credentials found for provider '{providerName}'. Please check your Conduit configuration.");
        }

        // Use a default model ID for operations that don't require a specific model
        // This is mainly for operations like listing available models
        return CreateClientForProvider(credentials.ProviderType.ToString(), credentials, "default-model-id");
    }
    
    /// <inheritdoc />
    public ILLMClient GetClientByProviderId(int providerId)
    {
        // This factory works with configuration-based settings which don't have provider IDs yet
        // For now, we'll throw a NotSupportedException until the configuration system is updated to use IDs
        throw new NotSupportedException("Provider ID-based lookups are not yet supported in the configuration-based factory. Please use GetClientByProvider with a provider name instead.");
    }

    private ILLMClient CreateClientForProvider(string providerName, ProviderCredentials credentials, string modelId)
    {
        // Get normalized provider name
        string normalizedProviderName = NormalizeProviderName(providerName, credentials.ProviderType).ToLowerInvariant();

        // Get default models configuration
        var defaultModels = _settings.DefaultModels;

        // Create the base client
        ILLMClient client;
        
        // Create clients using the new class hierarchy
        switch (normalizedProviderName)
        {
            // OpenAI-compatible clients
            case "openai":
                var openAiLogger = _loggerFactory.CreateLogger<OpenAIClient>();
                client = new OpenAIClient(credentials, modelId, openAiLogger, _httpClientFactory, _capabilityService, defaultModels);
                break;

            case "azure":
                var azureLogger = _loggerFactory.CreateLogger<AzureOpenAIClient>();
                client = new AzureOpenAIClient(credentials, modelId, azureLogger, _httpClientFactory, defaultModels);
                break;

            case "mistral":
            case "mistralai":
                var mistralLogger = _loggerFactory.CreateLogger<MistralClient>();
                client = new MistralClient(credentials, modelId, mistralLogger, _httpClientFactory, defaultModels);
                break;

            case "groq":
                var groqLogger = _loggerFactory.CreateLogger<GroqClient>();
                client = new GroqClient(credentials, modelId, groqLogger, _httpClientFactory, defaultModels);
                break;

            // Custom provider clients
            case "anthropic":
                var anthropicLogger = _loggerFactory.CreateLogger<AnthropicClient>();
                client = new AnthropicClient(credentials, modelId, anthropicLogger, _httpClientFactory, defaultModels);
                break;

            case "cohere":
                var cohereLogger = _loggerFactory.CreateLogger<CohereClient>();
                client = new CohereClient(credentials, modelId, cohereLogger, _httpClientFactory, defaultModels);
                break;

            case "google":
            case "gemini": // Alias
                var geminiLogger = _loggerFactory.CreateLogger<GeminiClient>();
                client = new GeminiClient(credentials, modelId, geminiLogger, _httpClientFactory, null, defaultModels);
                break;

            case "vertexai":
                var vertexAiLogger = _loggerFactory.CreateLogger<VertexAIClient>();
                client = new VertexAIClient(credentials, modelId, vertexAiLogger, _httpClientFactory, defaultModels);
                break;

            case "ollama":
                var ollamaLogger = _loggerFactory.CreateLogger<OllamaClient>();
                client = new OllamaClient(credentials, modelId, ollamaLogger, _httpClientFactory, defaultModels);
                break;

            case "replicate":
                var replicateLogger = _loggerFactory.CreateLogger<ReplicateClient>();
                client = new ReplicateClient(credentials, modelId, replicateLogger, _httpClientFactory, defaultModels);
                break;

            case "fireworks":
            case "fireworksai":
                var fireworksLogger = _loggerFactory.CreateLogger<FireworksClient>();
                client = new FireworksClient(credentials, modelId, fireworksLogger, _httpClientFactory, defaultModels);
                break;

            case "bedrock":
                var bedrockLogger = _loggerFactory.CreateLogger<BedrockClient>();
                client = new BedrockClient(credentials, modelId, bedrockLogger, _httpClientFactory, defaultModels);
                break;

            case "huggingface":
                var huggingFaceLogger = _loggerFactory.CreateLogger<HuggingFaceClient>();
                client = new HuggingFaceClient(credentials, modelId, huggingFaceLogger, _httpClientFactory, defaultModels);
                break;

            case "sagemaker":
                var sageMakerLogger = _loggerFactory.CreateLogger<SageMakerClient>();
                client = new SageMakerClient(credentials, modelId, sageMakerLogger, _httpClientFactory, defaultModels);
                break;

            case "openrouter":
                var openRouterLogger = _loggerFactory.CreateLogger<OpenRouterClient>();
                client = new OpenRouterClient(credentials, modelId, openRouterLogger, _httpClientFactory, defaultModels);
                break;

            case "openai-compatible":
            case "openaicompatible":
                var openAiCompatibleLogger = _loggerFactory.CreateLogger<OpenAICompatibleGenericClient>();
                client = new OpenAICompatibleGenericClient(credentials, modelId, openAiCompatibleLogger, _httpClientFactory, defaultModels);
                break;

            case "minimax":
                var miniMaxLogger = _loggerFactory.CreateLogger<MiniMaxClient>();
                client = new MiniMaxClient(credentials, modelId, miniMaxLogger, _httpClientFactory, defaultModels);
                break;

            case "ultravox":
                var ultravoxLogger = _loggerFactory.CreateLogger<UltravoxClient>();
                client = new UltravoxClient(credentials, modelId, ultravoxLogger, _httpClientFactory, defaultModels);
                break;

            case "elevenlabs":
            case "eleven-labs":
                var elevenLabsLogger = _loggerFactory.CreateLogger<ElevenLabsClient>();
                client = new ElevenLabsClient(credentials, modelId, elevenLabsLogger, _httpClientFactory, defaultModels);
                break;

            case "googlecloud":
            case "google-cloud":
            case "gcp":
                var googleCloudLogger = _loggerFactory.CreateLogger<GoogleCloudAudioClient>();
                client = new GoogleCloudAudioClient(credentials, modelId, googleCloudLogger, _httpClientFactory, defaultModels);
                break;

            case "cerebras":
                var cerebrasLogger = _loggerFactory.CreateLogger<CerebrasClient>();
                client = new CerebrasClient(credentials, modelId, cerebrasLogger, _httpClientFactory, defaultModels);
                break;

            case "aws":
            case "awstranscribe":
            case "aws-transcribe":
                var awsTranscribeLogger = _loggerFactory.CreateLogger<AWSTranscribeClient>();
                client = new AWSTranscribeClient(credentials, modelId, awsTranscribeLogger, _httpClientFactory, defaultModels);
                break;

            default:
                throw new UnsupportedProviderException($"Provider '{normalizedProviderName}' is not currently supported by ConduitLLM.");
        }

        // Apply performance tracking decorator if enabled
        if (_settings.PerformanceTracking?.Enabled == true)
        {
            // Check if provider or model is excluded
            var isProviderExcluded = _settings.PerformanceTracking.ExcludedProviders?.Contains(
                normalizedProviderName, StringComparer.OrdinalIgnoreCase) ?? false;
            var isModelExcluded = _settings.PerformanceTracking.ExcludedModels?.Contains(
                modelId, StringComparer.OrdinalIgnoreCase) ?? false;

            if (!isProviderExcluded && !isModelExcluded)
            {
                var performanceLogger = _loggerFactory.CreateLogger<PerformanceTrackingLLMClient>();
                var metricsService = _performanceMetricsService ?? new PerformanceMetricsService();
                
                client = new PerformanceTrackingLLMClient(
                    client,
                    metricsService,
                    performanceLogger,
                    normalizedProviderName,
                    _settings.PerformanceTracking.IncludeInResponse);
            }
        }

        return client;
    }

    /// <summary>
    /// Normalizes the provider name to handle both string names and ProviderType enum values.
    /// </summary>
    private static string NormalizeProviderName(string providerName, ProviderType providerType)
    {
        // If we have a valid provider name, use it
        if (!string.IsNullOrWhiteSpace(providerName) && !providerName.Equals(providerType.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return providerName;
        }

        // Otherwise, map the ProviderType enum to the expected provider name
        return providerType switch
        {
            ProviderType.OpenAI => "openai",
            ProviderType.AzureOpenAI => "azure",
            ProviderType.Anthropic => "anthropic",
            ProviderType.Gemini => "gemini",
            ProviderType.VertexAI => "vertexai",
            ProviderType.Cohere => "cohere",
            ProviderType.Mistral => "mistral",
            ProviderType.Groq => "groq",
            ProviderType.Ollama => "ollama",
            ProviderType.Replicate => "replicate",
            ProviderType.Fireworks => "fireworks",
            ProviderType.Bedrock => "bedrock",
            ProviderType.HuggingFace => "huggingface",
            ProviderType.SageMaker => "sagemaker",
            ProviderType.OpenRouter => "openrouter",
            ProviderType.OpenAICompatible => "openai-compatible",
            ProviderType.MiniMax => "minimax",
            ProviderType.Ultravox => "ultravox",
            ProviderType.ElevenLabs => "elevenlabs",
            ProviderType.GoogleCloud => "googlecloud",
            ProviderType.Cerebras => "cerebras",
            _ => providerType.ToString().ToLowerInvariant()
        };
    }
    // Legacy client creation method has been removed as part of the client migration
}
