using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.Utils;
using ConduitLLM.CoreClient.Exceptions;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.CoreClient.Services;

/// <summary>
/// Service for managing models using the Core API.
/// </summary>
public class ModelsService
{
    private readonly BaseClient _client;
    private readonly ILogger<ModelsService>? _logger;
    private const string BaseEndpoint = "/v1/models";

    /// <summary>
    /// Initializes a new instance of the ModelsService class.
    /// </summary>
    /// <param name="client">The base client to use for HTTP requests.</param>
    /// <param name="logger">Optional logger instance.</param>
    public ModelsService(BaseClient client, ILogger<ModelsService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a list of all available models.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available models.</returns>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<ModelsResponse> ListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Retrieving list of available models");

            var response = await _client.GetForServiceAsync<ModelsResponse>(BaseEndpoint, cancellationToken: cancellationToken);

            _logger?.LogDebug("Retrieved {ModelCount} models", response.Data?.Count() ?? 0);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Retrieves information about a specific model.
    /// </summary>
    /// <param name="modelId">The ID of the model to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Information about the specified model.</returns>
    /// <exception cref="ValidationException">Thrown when the model ID is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<Model> GetAsync(string modelId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ValidationException("Model ID is required", "model_id");

            _logger?.LogDebug("Retrieving information for model {ModelId}", modelId);

            var endpoint = $"{BaseEndpoint}/{Uri.EscapeDataString(modelId)}";
            var response = await _client.GetForServiceAsync<Model>(endpoint, cancellationToken: cancellationToken);

            _logger?.LogDebug("Retrieved information for model {ModelId}", response.Id);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Checks if a specific model is available.
    /// </summary>
    /// <param name="modelId">The ID of the model to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the model is available, false otherwise.</returns>
    public async Task<bool> IsAvailableAsync(string modelId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelId))
                return false;

            await GetAsync(modelId, cancellationToken);
            return true;
        }
        catch (ConduitCoreException ex) when (ex.StatusCode == 404)
        {
            return false;
        }
        catch (ValidationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Retrieves models that match the specified criteria.
    /// </summary>
    /// <param name="ownedBy">Filter models by owner (optional).</param>
    /// <param name="modelType">Filter models by type (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of models matching the criteria.</returns>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<Model>> FindAsync(
        string? ownedBy = null,
        string? modelType = null,
        CancellationToken cancellationToken = default)
    {
        var allModels = await ListAsync(cancellationToken);
        var models = allModels.Data;

        // Apply filters
        if (!string.IsNullOrWhiteSpace(ownedBy))
        {
            models = models.Where(m => m.OwnedBy.Equals(ownedBy, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(modelType))
        {
            // Filter by model type if the model ID contains the type
            models = models.Where(m => m.Id.Contains(modelType, StringComparison.OrdinalIgnoreCase));
        }

        return models;
    }

    /// <summary>
    /// Searches for models by name pattern.
    /// </summary>
    /// <param name="pattern">The search pattern to match against model names.</param>
    /// <param name="caseSensitive">Whether the search should be case-sensitive (default: false).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of models matching the search pattern.</returns>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<Model>> SearchAsync(
        string pattern,
        bool caseSensitive = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return Enumerable.Empty<Model>();

        var allModels = await ListAsync(cancellationToken);
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return allModels.Data.Where(m => m.Id.Contains(pattern, comparison));
    }

    /// <summary>
    /// Gets models grouped by owner.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary with owners as keys and their models as values.</returns>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<Dictionary<string, IEnumerable<Model>>> GetModelsByOwnerAsync(
        CancellationToken cancellationToken = default)
    {
        var allModels = await ListAsync(cancellationToken);
        
        return allModels.Data
            .GroupBy(m => m.OwnedBy)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());
    }

    /// <summary>
    /// Gets the most recently created models.
    /// </summary>
    /// <param name="count">The number of recent models to retrieve (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recently created models.</returns>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<Model>> GetRecentModelsAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            return Enumerable.Empty<Model>();

        var allModels = await ListAsync(cancellationToken);
        
        return allModels.Data
            .OrderByDescending(m => m.Created)
            .Take(count);
    }

    /// <summary>
    /// Validates that a model ID is in the correct format.
    /// </summary>
    /// <param name="modelId">The model ID to validate.</param>
    /// <returns>True if the model ID format is valid, false otherwise.</returns>
    public static bool IsValidModelId(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return false;

        // Basic validation: model ID should not contain invalid characters
        var invalidChars = new[] { ' ', '\t', '\n', '\r', '/', '\\', '?', '#', '[', ']', '@' };
        return !modelId.Any(c => invalidChars.Contains(c));
    }

    /// <summary>
    /// Determines if a model supports a specific capability based on its name.
    /// </summary>
    /// <param name="modelId">The model ID to check.</param>
    /// <param name="capability">The capability to check for.</param>
    /// <returns>True if the model likely supports the capability, false otherwise.</returns>
    /// <remarks>
    /// This is a heuristic-based check and may not be 100% accurate.
    /// For definitive capability information, use the discovery API.
    /// </remarks>
    public static bool SupportsCapability(string modelId, string capability)
    {
        if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(capability))
            return false;

        var lowerModelId = modelId.ToLowerInvariant();
        var lowerCapability = capability.ToLowerInvariant();

        return lowerCapability switch
        {
            "chat" or "completion" => IsChatModel(lowerModelId),
            "image" or "image_generation" => IsImageModel(lowerModelId),
            "vision" => IsVisionModel(lowerModelId),
            "embedding" or "embeddings" => IsEmbeddingModel(lowerModelId),
            "audio" or "speech" => IsAudioModel(lowerModelId),
            "code" or "code_generation" => IsCodeModel(lowerModelId),
            _ => false
        };
    }

    private static bool IsChatModel(string modelId)
    {
        var chatIndicators = new[] { "chat", "gpt", "claude", "llama", "mistral", "gemini" };
        return chatIndicators.Any(indicator => modelId.Contains(indicator));
    }

    private static bool IsImageModel(string modelId)
    {
        var imageIndicators = new[] { "dall", "image", "stable-diffusion", "midjourney" };
        return imageIndicators.Any(indicator => modelId.Contains(indicator));
    }

    private static bool IsVisionModel(string modelId)
    {
        var visionIndicators = new[] { "vision", "gpt-4-vision", "gpt-4v", "claude-3" };
        return visionIndicators.Any(indicator => modelId.Contains(indicator));
    }

    private static bool IsEmbeddingModel(string modelId)
    {
        var embeddingIndicators = new[] { "embedding", "ada", "text-embedding" };
        return embeddingIndicators.Any(indicator => modelId.Contains(indicator));
    }

    private static bool IsAudioModel(string modelId)
    {
        var audioIndicators = new[] { "whisper", "tts", "speech", "audio" };
        return audioIndicators.Any(indicator => modelId.Contains(indicator));
    }

    private static bool IsCodeModel(string modelId)
    {
        var codeIndicators = new[] { "code", "codex", "github", "copilot" };
        return codeIndicators.Any(indicator => modelId.Contains(indicator));
    }
}