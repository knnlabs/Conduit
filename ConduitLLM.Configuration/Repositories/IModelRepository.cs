using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for Model entity operations.
    /// Models must be pre-created through seed data or admin operations.
    /// </summary>
    public interface IModelRepository
    {
        /// <summary>
        /// Gets a model by its ID.
        /// </summary>
        Task<Model?> GetByIdAsync(int id);

        /// <summary>
        /// Gets a model by its ID, including related entities.
        /// </summary>
        Task<Model?> GetByIdWithDetailsAsync(int id);

        /// <summary>
        /// Gets all models.
        /// </summary>
        Task<List<Model>> GetAllAsync();

        /// <summary>
        /// Gets all models with their details (capabilities, series, etc.).
        /// </summary>
        Task<List<Model>> GetAllWithDetailsAsync();

        /// <summary>
        /// Finds a model by its primary identifier.
        /// </summary>
        Task<Model?> GetByIdentifierAsync(string identifier);

        /// <summary>
        /// Gets models by type (Text, Image, Video, etc.).
        /// </summary>
        Task<List<Model>> GetByTypeAsync(ModelType modelType);

        /// <summary>
        /// Gets models by series.
        /// </summary>
        Task<List<Model>> GetBySeriesAsync(int seriesId);

        /// <summary>
        /// Creates a new model.
        /// </summary>
        Task<Model> CreateAsync(Model model);

        /// <summary>
        /// Updates an existing model.
        /// </summary>
        Task<Model> UpdateAsync(Model model);

        /// <summary>
        /// Checks if a model exists.
        /// </summary>
        Task<bool> ExistsAsync(int id);

        /// <summary>
        /// Gets a model by its name.
        /// </summary>
        Task<Model?> GetByNameAsync(string name);

        /// <summary>
        /// Searches for models by name.
        /// </summary>
        Task<List<Model>> SearchByNameAsync(string query);

        /// <summary>
        /// Checks if a model has any mapping references.
        /// </summary>
        Task<bool> HasMappingReferencesAsync(int modelId);

        /// <summary>
        /// Deletes a model by ID.
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Gets models available from a specific provider.
        /// Filters based on ModelIdentifier entries with matching provider.
        /// </summary>
        /// <param name="providerName">The provider name (e.g., "groq", "openai", "anthropic")</param>
        Task<List<Model>> GetByProviderAsync(string providerName);
    }
}