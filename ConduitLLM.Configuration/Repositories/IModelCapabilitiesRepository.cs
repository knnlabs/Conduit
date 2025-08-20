using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for ModelCapabilities entity operations.
    /// </summary>
    public interface IModelCapabilitiesRepository
    {
        /// <summary>
        /// Gets model capabilities by its ID.
        /// </summary>
        Task<ModelCapabilities?> GetByIdAsync(int id);

        /// <summary>
        /// Gets all model capabilities.
        /// </summary>
        Task<List<ModelCapabilities>> GetAllAsync();

        /// <summary>
        /// Gets models using specific capabilities.
        /// </summary>
        Task<List<Model>?> GetModelsUsingCapabilitiesAsync(int capabilitiesId);

        /// <summary>
        /// Creates new model capabilities.
        /// </summary>
        Task<ModelCapabilities> CreateAsync(ModelCapabilities capabilities);

        /// <summary>
        /// Updates existing model capabilities.
        /// </summary>
        Task<ModelCapabilities> UpdateAsync(ModelCapabilities capabilities);

        /// <summary>
        /// Deletes model capabilities by ID.
        /// </summary>
        Task<bool> DeleteAsync(int id);
    }
}