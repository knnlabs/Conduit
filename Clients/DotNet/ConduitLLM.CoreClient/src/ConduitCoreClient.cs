using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Services;
using ConduitLLM.CoreClient.Exceptions;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.CoreClient;

/// <summary>
/// Main client for accessing the Conduit Core API.
/// Provides OpenAI-compatible endpoints for chat completions, image generation, and model management.
/// </summary>
public class ConduitCoreClient : BaseClient
{
    /// <summary>
    /// Gets the chat service for creating completions.
    /// </summary>
    public ChatService Chat { get; }

    /// <summary>
    /// Gets the images service for generating, editing, and creating variations of images.
    /// </summary>
    public ImagesService Images { get; }

    /// <summary>
    /// Gets the models service for listing and retrieving model information.
    /// </summary>
    public ModelsService Models { get; }

    /// <summary>
    /// Initializes a new instance of the ConduitCoreClient class.
    /// </summary>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="httpClient">Optional HTTP client instance. If not provided, a new one will be created.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="ValidationException">Thrown when configuration is invalid.</exception>
    public ConduitCoreClient(
        ConduitCoreClientConfiguration configuration,
        HttpClient? httpClient = null,
        ILogger<ConduitCoreClient>? logger = null)
        : base(httpClient ?? new HttpClient(), configuration, logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        // Create child loggers for services
        ILogger<ChatService>? chatLogger = null;
        ILogger<ImagesService>? imagesLogger = null;
        ILogger<ModelsService>? modelsLogger = null;
        
        if (logger != null)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new LoggerProvider(logger)));
            chatLogger = loggerFactory.CreateLogger<ChatService>();
            imagesLogger = loggerFactory.CreateLogger<ImagesService>();
            modelsLogger = loggerFactory.CreateLogger<ModelsService>();
        }

        Chat = new ChatService(this, chatLogger);
        Images = new ImagesService(this, imagesLogger);
        Models = new ModelsService(this, modelsLogger);
    }

    /// <summary>
    /// Creates a new ConduitCoreClient instance from an API key.
    /// </summary>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="baseUrl">Optional base URL override.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A new ConduitCoreClient instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the API key is null or empty.</exception>
    public static ConduitCoreClient FromApiKey(
        string apiKey,
        string? baseUrl = null,
        ILogger<ConduitCoreClient>? logger = null)
    {
        var configuration = ConduitCoreClientConfiguration.FromApiKey(apiKey, baseUrl);
        return new ConduitCoreClient(configuration, logger: logger);
    }

    /// <summary>
    /// Creates a new ConduitCoreClient instance from environment variables.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A new ConduitCoreClient instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are missing.</exception>
    public static ConduitCoreClient FromEnvironment(ILogger<ConduitCoreClient>? logger = null)
    {
        var configuration = ConduitCoreClientConfiguration.FromEnvironment();
        return new ConduitCoreClient(configuration, logger: logger);
    }

    /// <summary>
    /// Tests the connection to the Conduit Core API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the connection is successful, false otherwise.</returns>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await Models.ListAsync(cancellationToken);
            return models.Data?.Any() == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets information about the client configuration.
    /// </summary>
    /// <returns>A readonly copy of the client configuration.</returns>
    public ConduitCoreClientConfiguration GetConfiguration()
    {
        // Return a copy to prevent external modification
        return new ConduitCoreClientConfiguration
        {
            ApiKey = "***REDACTED***", // Don't expose the actual API key
            BaseUrl = _configuration.BaseUrl,
            TimeoutSeconds = _configuration.TimeoutSeconds,
            MaxRetries = _configuration.MaxRetries,
            RetryDelayMs = _configuration.RetryDelayMs,
            DefaultHeaders = new Dictionary<string, string>(_configuration.DefaultHeaders),
            OrganizationId = _configuration.OrganizationId
        };
    }

    private readonly ConduitCoreClientConfiguration _configuration;

    /// <summary>
    /// Simple logger provider for dependency injection compatibility.
    /// </summary>
    private class LoggerProvider : ILoggerProvider
    {
        private readonly ILogger _logger;

        public LoggerProvider(ILogger logger)
        {
            _logger = logger;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _logger;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}

/// <summary>
/// Extension methods for ConduitCoreClient.
/// </summary>
public static class ConduitCoreClientExtensions
{
    /// <summary>
    /// Creates a simple chat completion with just a user message.
    /// </summary>
    /// <param name="client">The Conduit Core client.</param>
    /// <param name="model">The model to use.</param>
    /// <param name="message">The user message.</param>
    /// <param name="maxTokens">Optional maximum tokens to generate.</param>
    /// <param name="temperature">Optional temperature for randomness.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assistant's response message.</returns>
    public static async Task<string> ChatAsync(
        this ConduitCoreClient client,
        string model,
        string message,
        int? maxTokens = null,
        double? temperature = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ConduitLLM.CoreClient.Models.ChatCompletionRequest
        {
            Model = model,
            Messages = new[]
            {
                new ConduitLLM.CoreClient.Models.ChatCompletionMessage
                {
                    Role = "user",
                    Content = message
                }
            },
            MaxTokens = maxTokens,
            Temperature = temperature
        };

        var response = await client.Chat.CreateCompletionAsync(request, cancellationToken);
        return response.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
    }

    /// <summary>
    /// Generates a single image from a text prompt.
    /// </summary>
    /// <param name="client">The Conduit Core client.</param>
    /// <param name="prompt">The text prompt for image generation.</param>
    /// <param name="model">Optional model to use (defaults to DALL-E 3).</param>
    /// <param name="size">Optional image size.</param>
    /// <param name="quality">Optional image quality.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URL of the generated image.</returns>
    public static async Task<string?> GenerateImageAsync(
        this ConduitCoreClient client,
        string prompt,
        string? model = null,
        ConduitLLM.CoreClient.Models.ImageSize? size = null,
        ConduitLLM.CoreClient.Models.ImageQuality? quality = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ConduitLLM.CoreClient.Models.ImageGenerationRequest
        {
            Prompt = prompt,
            Model = model ?? ConduitLLM.CoreClient.Models.ImageModels.DallE3,
            N = 1,
            Size = size,
            Quality = quality
        };

        var response = await client.Images.GenerateAsync(request, cancellationToken);
        return response.Data.FirstOrDefault()?.Url;
    }

    /// <summary>
    /// Checks if a specific model is available and supported.
    /// </summary>
    /// <param name="client">The Conduit Core client.</param>
    /// <param name="modelId">The model ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the model is available, false otherwise.</returns>
    public static async Task<bool> IsModelAvailableAsync(
        this ConduitCoreClient client,
        string modelId,
        CancellationToken cancellationToken = default)
    {
        return await client.Models.IsAvailableAsync(modelId, cancellationToken);
    }

    /// <summary>
    /// Gets all models that support chat completions.
    /// </summary>
    /// <param name="client">The Conduit Core client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of models that support chat completions.</returns>
    public static async Task<IEnumerable<ConduitLLM.CoreClient.Models.Model>> GetChatModelsAsync(
        this ConduitCoreClient client,
        CancellationToken cancellationToken = default)
    {
        var allModels = await client.Models.ListAsync(cancellationToken);
        return allModels.Data.Where(m => ConduitLLM.CoreClient.Services.ModelsService.SupportsCapability(m.Id, "chat"));
    }

    /// <summary>
    /// Gets all models that support image generation.
    /// </summary>
    /// <param name="client">The Conduit Core client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of models that support image generation.</returns>
    public static async Task<IEnumerable<ConduitLLM.CoreClient.Models.Model>> GetImageModelsAsync(
        this ConduitCoreClient client,
        CancellationToken cancellationToken = default)
    {
        var allModels = await client.Models.ListAsync(cancellationToken);
        return allModels.Data.Where(m => ConduitLLM.CoreClient.Services.ModelsService.SupportsCapability(m.Id, "image"));
    }
}