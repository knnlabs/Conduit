using System;
using System.Linq;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces; // Use the original interface location

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
    
    // Removed compatibility flag as migration is now complete

    /// <summary>
    /// Initializes a new instance of the <see cref="LLMClientFactory"/> class.
    /// </summary>
    /// <param name="settingsOptions">The configuration settings.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public LLMClientFactory(
        IOptions<ConduitSettings> settingsOptions, 
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settingsOptions.Value ?? throw new ArgumentNullException(nameof(settingsOptions), "Conduit settings cannot be null.");
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <inheritdoc />
    public ILLMClient GetClient(string modelAlias)
    {
        if (string.IsNullOrWhiteSpace(modelAlias))
        {
            throw new ArgumentException("Model alias cannot be null or whitespace.", nameof(modelAlias));
        }

        // 1. Find the model mapping
        var mapping = _settings.ModelMappings?.FirstOrDefault(m =>
            string.Equals(m.ModelAlias, modelAlias, StringComparison.OrdinalIgnoreCase));

        if (mapping == null)
        {
            throw new ConfigurationException($"No model mapping found for alias '{modelAlias}'. Please check your Conduit configuration.");
        }

        // 2. Find the provider credentials using the ProviderName from the mapping
        var credentials = _settings.ProviderCredentials?.FirstOrDefault(p =>
            string.Equals(p.ProviderName, mapping.ProviderName, StringComparison.OrdinalIgnoreCase));

        if (credentials == null)
        {
            throw new ConfigurationException($"No provider credentials found for provider '{mapping.ProviderName}' (required by model alias '{modelAlias}'). Please check your Conduit configuration.");
        }

        // 3. Instantiate the appropriate client based on ProviderName
        return CreateClientForProvider(mapping.ProviderName, credentials, mapping.ProviderModelId);
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
            string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (credentials == null)
        {
            throw new ConfigurationException($"No provider credentials found for provider '{providerName}'. Please check your Conduit configuration.");
        }

        // Use a default model ID for operations that don't require a specific model
        // This is mainly for operations like listing available models
        return CreateClientForProvider(providerName, credentials, "default-model-id");
    }

    private ILLMClient CreateClientForProvider(string providerName, ProviderCredentials credentials, string modelId)
    {
        // Get normalized provider name
        string normalizedProviderName = providerName.ToLowerInvariant();
        
        // Get default models configuration
        var defaultModels = _settings.DefaultModels;
        
        // Create clients using the new class hierarchy
        switch (normalizedProviderName)
        {
            // OpenAI-compatible clients
            case "openai":
                var openAiLogger = _loggerFactory.CreateLogger<OpenAIClient>();
                return new OpenAIClient(credentials, modelId, openAiLogger, _httpClientFactory, defaultModels);
            
            case "azure":
                var azureLogger = _loggerFactory.CreateLogger<AzureOpenAIClient>();
                return new AzureOpenAIClient(credentials, modelId, azureLogger, _httpClientFactory);
                
            case "mistral":
            case "mistralai":
                var mistralLogger = _loggerFactory.CreateLogger<MistralClient>();
                return new MistralClient(credentials, modelId, mistralLogger, _httpClientFactory);
            
            case "groq":
                var groqLogger = _loggerFactory.CreateLogger<GroqClient>();
                return new GroqClient(credentials, modelId, groqLogger, _httpClientFactory);
                
            // Custom provider clients
            case "anthropic":
                var anthropicLogger = _loggerFactory.CreateLogger<AnthropicClient>();
                return new AnthropicClient(credentials, modelId, anthropicLogger, _httpClientFactory);
                
            case "cohere":
                var cohereLogger = _loggerFactory.CreateLogger<CohereClient>();
                return new CohereClient(credentials, modelId, cohereLogger, _httpClientFactory);
                
            case "google":
            case "gemini": // Alias
                var geminiLogger = _loggerFactory.CreateLogger<GeminiClient>();
                return new GeminiClient(credentials, modelId, geminiLogger, _httpClientFactory);
                
            case "vertexai":
                var vertexAiLogger = _loggerFactory.CreateLogger<VertexAIClient>();
                return new VertexAIClient(credentials, modelId, vertexAiLogger, _httpClientFactory);
                
            case "ollama":
                var ollamaLogger = _loggerFactory.CreateLogger<OllamaClient>();
                return new OllamaClient(credentials, modelId, ollamaLogger, _httpClientFactory);
                
            case "replicate":
                var replicateLogger = _loggerFactory.CreateLogger<ReplicateClient>();
                return new ReplicateClient(credentials, modelId, replicateLogger, _httpClientFactory);
                
            case "fireworks":
            case "fireworksai":
                var fireworksLogger = _loggerFactory.CreateLogger<FireworksClient>();
                return new FireworksClient(credentials, modelId, fireworksLogger, _httpClientFactory);
            
            case "bedrock":
                var bedrockLogger = _loggerFactory.CreateLogger<BedrockClient>();
                return new BedrockClient(credentials, modelId, bedrockLogger, _httpClientFactory);
            
            case "huggingface":
                var huggingFaceLogger = _loggerFactory.CreateLogger<HuggingFaceClient>();
                return new HuggingFaceClient(credentials, modelId, huggingFaceLogger, _httpClientFactory);
            
            case "sagemaker":
                var sageMakerLogger = _loggerFactory.CreateLogger<SageMakerClient>();
                return new SageMakerClient(credentials, modelId, sageMakerLogger, _httpClientFactory);
            
            case "openrouter":
                var openRouterLogger = _loggerFactory.CreateLogger<OpenRouterClient>();
                return new OpenRouterClient(credentials, modelId, openRouterLogger, _httpClientFactory);
            
            case "openai-compatible":
            case "openaicompatible":
                var openAiCompatibleLogger = _loggerFactory.CreateLogger<OpenAICompatibleGenericClient>();
                return new OpenAICompatibleGenericClient(credentials, modelId, openAiCompatibleLogger, _httpClientFactory);
            
            case "ultravox":
                var ultravoxLogger = _loggerFactory.CreateLogger<UltravoxClient>();
                return new UltravoxClient(credentials, modelId, ultravoxLogger, _httpClientFactory);
            
            case "elevenlabs":
            case "eleven-labs":
                var elevenLabsLogger = _loggerFactory.CreateLogger<ElevenLabsClient>();
                return new ElevenLabsClient(credentials, modelId, elevenLabsLogger, _httpClientFactory, defaultModels);
            
            default:
                throw new UnsupportedProviderException($"Provider '{normalizedProviderName}' is not currently supported by ConduitLLM.");
        }
    }
    // Legacy client creation method has been removed as part of the client migration
}