using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository interface for Model entity operations.
/// Models must be pre-created through seed data or admin operations.
/// Inherits standard CRUD operations from IRepositoryBase.
/// </summary>
public interface IModelRepository : IRepositoryBase<Model, int>
{
    /// <summary>
    /// Gets a model by its ID, including related entities (Series, Author, Identifiers).
    /// </summary>
    /// <param name="id">The model ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model with details or null if not found</returns>
    Task<Model?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all models with their details (series, author, identifiers).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all models with details</returns>
    Task<List<Model>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a model by its primary identifier.
    /// Searches ModelProviderTypeAssociation first, then falls back to model name.
    /// </summary>
    /// <param name="identifier">The model identifier to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model or null if not found</returns>
    Task<Model?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets models by series.
    /// </summary>
    /// <param name="seriesId">The series ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of models in the series</returns>
    Task<List<Model>> GetBySeriesAsync(int seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a model by its name.
    /// </summary>
    /// <param name="name">The model name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model or null if not found</returns>
    Task<Model?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for active models by name (case-insensitive partial match).
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching models</returns>
    Task<List<Model>> SearchByNameAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a model has any mapping references.
    /// </summary>
    /// <param name="modelId">The model ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the model has mapping references</returns>
    Task<bool> HasMappingReferencesAsync(int modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets models available from a specific provider.
    /// Filters based on ModelProviderTypeAssociation entries with matching provider.
    /// </summary>
    /// <param name="providerType">The provider type (e.g., OpenAI, Anthropic)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of models for the provider</returns>
    Task<List<Model>> GetByProviderAsync(ProviderType providerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a model identifier by ID.
    /// </summary>
    /// <param name="modelId">The model ID</param>
    /// <param name="identifierId">The identifier ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteIdentifierAsync(int modelId, int identifierId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all models with details, supporting optional pagination, search, and capability filtering.
    /// When page/pageSize are provided, returns paginated results.
    /// </summary>
    /// <param name="page">Page number (1-based), or null for all results</param>
    /// <param name="pageSize">Items per page, or null for all results</param>
    /// <param name="search">Optional search term for model name (case-insensitive partial match)</param>
    /// <param name="capability">Optional capability filter (chat, vision, image, video, embeddings)</param>
    /// <param name="hasProviders">Optional filter for models with/without provider identifiers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of models list and total count</returns>
    Task<(List<Model> Items, int TotalCount)> GetPaginatedWithFilterAsync(
        int? page = null,
        int? pageSize = null,
        string? search = null,
        string? capability = null,
        bool? hasProviders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new model and returns the created entity.
    /// Use this when you need the full entity back after creation.
    /// </summary>
    /// <param name="model">The model to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created model with its assigned ID</returns>
    Task<Model> CreateModelAsync(Model model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing model and returns the updated entity.
    /// Use this when you need the full entity back after update.
    /// </summary>
    /// <param name="model">The model to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated model</returns>
    Task<Model> UpdateModelAsync(Model model, CancellationToken cancellationToken = default);
}
