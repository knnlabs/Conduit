using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for ModelAuthor entity operations.
    /// </summary>
    public interface IModelAuthorRepository
    {
        /// <summary>
        /// Gets a model author by its ID.
        /// </summary>
        Task<ModelAuthor?> GetByIdAsync(int id);

        /// <summary>
        /// Gets all model authors.
        /// </summary>
        Task<List<ModelAuthor>> GetAllAsync();

        /// <summary>
        /// Gets a model author by name.
        /// </summary>
        Task<ModelAuthor?> GetByNameAsync(string name);

        /// <summary>
        /// Gets series by author.
        /// </summary>
        Task<List<ModelSeries>?> GetSeriesByAuthorAsync(int authorId);

        /// <summary>
        /// Creates a new model author.
        /// </summary>
        Task<ModelAuthor> CreateAsync(ModelAuthor author);

        /// <summary>
        /// Updates an existing model author.
        /// </summary>
        Task<ModelAuthor> UpdateAsync(ModelAuthor author);

        /// <summary>
        /// Deletes a model author by ID.
        /// </summary>
        Task<bool> DeleteAsync(int id);
    }
}