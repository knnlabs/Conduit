using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Decorators;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Providers;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Database-aware implementation of ILLMClientFactory that uses provider credentials from the database.
    /// </summary>
    /// <remarks>
    /// This factory creates LLM client instances using credentials dynamically loaded from the database.
    /// It supports all configured providers and applies decorators like performance tracking when enabled.
    /// 
    /// Use this factory when:
    /// - Credentials are stored in the database
    /// - Multiple providers of the same type are configured
    /// - Dynamic credential management is required
    /// </remarks>
    public class DatabaseAwareLLMClientFactory : ILLMClientFactory
    {
        private readonly IProviderService _credentialService;
        private readonly IModelProviderMappingService _mappingService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DatabaseAwareLLMClientFactory> _logger;
        private readonly IPerformanceMetricsService? _performanceMetricsService;
        private readonly IModelCapabilityService? _capabilityService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseAwareLLMClientFactory"/> class.
        /// </summary>
        public DatabaseAwareLLMClientFactory(
            IProviderService credentialService,
            IModelProviderMappingService mappingService,
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            ILogger<DatabaseAwareLLMClientFactory> logger,
            IPerformanceMetricsService? performanceMetricsService = null,
            IModelCapabilityService? capabilityService = null)
        {
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceMetricsService = performanceMetricsService;
            _capabilityService = capabilityService;
        }

        /// <inheritdoc />
        public ILLMClient GetClient(string modelName)
        {
            _logger.LogDebug("DatabaseAwareLLMClientFactory.GetClient called for model: {ModelName}", modelName);
            
            // Get model mapping from database
            var mapping = Task.Run(async () => 
                await _mappingService.GetMappingByModelAliasAsync(modelName)).Result;
            
            if (mapping == null)
            {
                _logger.LogWarning("No model mapping found in database for alias: {ModelAlias}", modelName);
                throw new ConfigurationException($"No model mapping found for alias '{modelName}'. Please check your Conduit configuration.");
            }
            
            _logger.LogDebug("Found mapping in database: {ModelAlias} -> ProviderId:{ProviderId}/{ProviderModelId}", 
                mapping.ModelAlias, mapping.ProviderId, mapping.ProviderModelId);
            
            // Get the provider from database
            var provider = Task.Run(async () => 
                await _credentialService.GetProviderByIdAsync(mapping.ProviderId)).Result;
            
            if (provider == null || !provider.IsEnabled)
            {
                _logger.LogWarning("Provider {ProviderId} not found or disabled", mapping.ProviderId);
                throw new ConfigurationException($"Provider for model '{modelName}' is not available.");
            }
            
            // Get key credentials for this provider
            var keyCredentials = Task.Run(async () => 
                await _credentialService.GetKeyCredentialsByProviderIdAsync(provider.Id)).Result;
            
            // Find the primary key or use the first enabled one
            var primaryKey = keyCredentials.FirstOrDefault(k => k.IsPrimary && k.IsEnabled) 
                ?? keyCredentials.FirstOrDefault(k => k.IsEnabled);
            
            if (primaryKey == null)
            {
                _logger.LogWarning("No enabled API key found for provider {ProviderId}", provider.Id);
                throw new ConfigurationException($"No API key configured for provider '{provider.ProviderName}'.");
            }
            
            // Create the appropriate client based on provider type
            return CreateClientForProvider(provider, primaryKey, mapping.ProviderModelId);
        }

        
        /// <inheritdoc />
        public ILLMClient GetClientByProviderId(int providerId)
        {
            _logger.LogDebug("Getting client for provider ID {ProviderId} using database credentials", providerId);

            // Get provider from database
            var provider = Task.Run(async () => 
                await _credentialService.GetProviderByIdAsync(providerId)).Result;

            if (provider == null || !provider.IsEnabled)
            {
                _logger.LogWarning("No enabled provider found for provider ID {ProviderId} in database", providerId);
                throw new ConfigurationException($"No provider found for provider ID '{providerId}'.");
            }
            
            // Get key credentials for this provider
            var keyCredentials = Task.Run(async () => 
                await _credentialService.GetKeyCredentialsByProviderIdAsync(provider.Id)).Result;
            
            // Find the primary key or use the first enabled one
            var primaryKey = keyCredentials.FirstOrDefault(k => k.IsPrimary && k.IsEnabled) 
                ?? keyCredentials.FirstOrDefault(k => k.IsEnabled);
            
            if (primaryKey == null)
            {
                _logger.LogWarning("No enabled API key found for provider {ProviderId}", provider.Id);
                throw new ConfigurationException($"No API key configured for provider '{provider.ProviderName}'.");
            }

            // Use a default model ID for operations that don't require a specific model
            return CreateClientForProvider(provider, primaryKey, "default-model-id");
        }

        /// <inheritdoc />
        public IProviderMetadata? GetProviderMetadata(ProviderType providerType)
        {
            // This factory doesn't have access to provider metadata
            // Return null to indicate metadata is not available through this factory
            return null;
        }

        /// <inheritdoc />
        public ILLMClient GetClientByProviderType(ProviderType providerType)
        {
            _logger.LogDebug("Getting client for provider type {ProviderType} using database credentials", providerType);

            // Get first enabled provider of this type from database
            var provider = Task.Run(async () => 
            {
                var allProviders = await _credentialService.GetAllProvidersAsync();
                return allProviders.FirstOrDefault(p => p.ProviderType == providerType);
            }).Result;

            if (provider == null || !provider.IsEnabled)
            {
                _logger.LogWarning("No enabled provider found for provider type {ProviderType} in database", providerType);
                throw new ConfigurationException($"No provider found for provider type '{providerType}'.");
            }
            
            // Get key credentials for this provider
            var keyCredentials = Task.Run(async () => 
                await _credentialService.GetKeyCredentialsByProviderIdAsync(provider.Id)).Result;
            
            // Find the primary key or use the first enabled one
            var primaryKey = keyCredentials.FirstOrDefault(k => k.IsPrimary && k.IsEnabled) 
                ?? keyCredentials.FirstOrDefault(k => k.IsEnabled);
            
            if (primaryKey == null)
            {
                _logger.LogWarning("No enabled API key found for provider {ProviderId}", provider.Id);
                throw new ConfigurationException($"No API key configured for provider '{provider.ProviderName}'.");
            }

            // Use a default model ID for operations that don't require a specific model
            return CreateClientForProvider(provider, primaryKey, "default-model-id");
        }

        /// <inheritdoc />
        public ILLMClient CreateTestClient(Provider provider, ProviderKeyCredential keyCredential)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (keyCredential == null)
            {
                throw new ArgumentNullException(nameof(keyCredential));
            }

            if (string.IsNullOrWhiteSpace(keyCredential.ApiKey))
            {
                throw new ArgumentException("API key is required for testing credentials", nameof(keyCredential));
            }

            _logger.LogDebug("Creating test client for provider type: {ProviderType}", provider.ProviderType);

            // Use a minimal model ID for testing - providers should accept this for auth verification
            const string testModelId = "test-model";

            return CreateClientForProvider(provider, keyCredential, testModelId);
        }

        private ILLMClient CreateClientForProvider(Provider provider, ProviderKeyCredential keyCredential, string modelId)
        {
            var providerName = provider.ProviderType.ToString().ToLowerInvariant();
            
            _logger.LogDebug("Creating client for provider type: {ProviderType}, model: {ModelId}", 
                provider.ProviderType, modelId);

            // TODO: Get default models configuration from somewhere (database?)
            ProviderDefaultModels? defaultModels = null;

            // Create the base client
            ILLMClient client;
            
            // Create clients using the provider type
            switch (provider.ProviderType)
            {
                case ProviderType.OpenAI:
                    var openAiLogger = _loggerFactory.CreateLogger<OpenAIClient>();
                    client = new OpenAIClient(provider, keyCredential, modelId, openAiLogger, 
                        _httpClientFactory, _capabilityService, defaultModels);
                    break;

                case ProviderType.AzureOpenAI:
                    var azureLogger = _loggerFactory.CreateLogger<AzureOpenAIClient>();
                    client = new AzureOpenAIClient(provider, keyCredential, modelId, azureLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Anthropic:
                    var anthropicLogger = _loggerFactory.CreateLogger<AnthropicClient>();
                    client = new AnthropicClient(provider, keyCredential, modelId, anthropicLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Mistral:
                    var mistralLogger = _loggerFactory.CreateLogger<MistralClient>();
                    client = new MistralClient(provider, keyCredential, modelId, mistralLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Groq:
                    var groqLogger = _loggerFactory.CreateLogger<GroqClient>();
                    client = new GroqClient(provider, keyCredential, modelId, groqLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Cohere:
                    var cohereLogger = _loggerFactory.CreateLogger<CohereClient>();
                    client = new CohereClient(provider, keyCredential, modelId, cohereLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Gemini:
                    var geminiLogger = _loggerFactory.CreateLogger<GeminiClient>();
                    client = new GeminiClient(provider, keyCredential, modelId, geminiLogger, 
                        _httpClientFactory, null, defaultModels);
                    break;

                case ProviderType.VertexAI:
                    var vertexLogger = _loggerFactory.CreateLogger<VertexAIClient>();
                    client = new VertexAIClient(provider, keyCredential, modelId, vertexLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Ollama:
                    var ollamaLogger = _loggerFactory.CreateLogger<OllamaClient>();
                    client = new OllamaClient(provider, keyCredential, modelId, ollamaLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Bedrock:
                    var bedrockLogger = _loggerFactory.CreateLogger<BedrockClient>();
                    client = new BedrockClient(provider, keyCredential, modelId, bedrockLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.HuggingFace:
                    var hfLogger = _loggerFactory.CreateLogger<HuggingFaceClient>();
                    client = new HuggingFaceClient(provider, keyCredential, modelId, hfLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Replicate:
                    var replicateLogger = _loggerFactory.CreateLogger<ReplicateClient>();
                    client = new ReplicateClient(provider, keyCredential, modelId, replicateLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Fireworks:
                    var fireworksLogger = _loggerFactory.CreateLogger<FireworksClient>();
                    client = new FireworksClient(provider, keyCredential, modelId, fireworksLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.SageMaker:
                    var sageMakerLogger = _loggerFactory.CreateLogger<SageMakerClient>();
                    // SageMaker needs endpoint name - use model ID as endpoint name
                    client = new SageMakerClient(provider, keyCredential, modelId, sageMakerLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.OpenRouter:
                    var openRouterLogger = _loggerFactory.CreateLogger<OpenRouterClient>();
                    client = new OpenRouterClient(provider, keyCredential, modelId, openRouterLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.OpenAICompatible:
                    var compatibleLogger = _loggerFactory.CreateLogger<OpenAICompatibleGenericClient>();
                    client = new OpenAICompatibleGenericClient(provider, keyCredential, modelId, compatibleLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.MiniMax:
                    var miniMaxLogger = _loggerFactory.CreateLogger<MiniMaxClient>();
                    client = new MiniMaxClient(provider, keyCredential, modelId, miniMaxLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Ultravox:
                    var ultravoxLogger = _loggerFactory.CreateLogger<UltravoxClient>();
                    client = new UltravoxClient(provider, keyCredential, modelId, ultravoxLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.ElevenLabs:
                    var elevenLabsLogger = _loggerFactory.CreateLogger<ElevenLabsClient>();
                    client = new ElevenLabsClient(provider, keyCredential, modelId, elevenLabsLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.GoogleCloud:
                    var gcpLogger = _loggerFactory.CreateLogger<GoogleCloudAudioClient>();
                    client = new GoogleCloudAudioClient(provider, keyCredential, modelId, gcpLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                case ProviderType.Cerebras:
                    var cerebrasLogger = _loggerFactory.CreateLogger<CerebrasClient>();
                    client = new CerebrasClient(provider, keyCredential, modelId, cerebrasLogger, 
                        _httpClientFactory, defaultModels);
                    break;

                default:
                    throw new ConfigurationException($"Unsupported provider type: {provider.ProviderType}");
            }

            // Apply decorators if configured
            if (_performanceMetricsService != null)
            {
                _logger.LogDebug("Applying performance tracking decorator to client");
                var perfLogger = _loggerFactory.CreateLogger<PerformanceTrackingLLMClient>();
                client = new PerformanceTrackingLLMClient(client, _performanceMetricsService, perfLogger, providerName, true);
            }

            return client;
        }
    }
}