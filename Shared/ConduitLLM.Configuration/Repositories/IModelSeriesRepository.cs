using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for ModelSeries entity operations.
    /// </summary>
    public interface IModelSeriesRepository
    {
        /// <summary>
        /// Gets a model series by its ID.
        /// </summary>
        Task<ModelSeries?> GetByIdAsync(int id);

        /// <summary>
        /// Gets a model series by its ID with author.
        /// </summary>
        Task<ModelSeries?> GetByIdWithAuthorAsync(int id);

        /// <summary>
        /// Gets all model series.
        /// </summary>
        Task<List<ModelSeries>> GetAllAsync();

        /// <summary>
        /// Gets all model series with author information.
        /// </summary>
        Task<List<ModelSeries>> GetAllWithAuthorAsync();

        /// <summary>
        /// Gets a model series by name and author.
        /// </summary>
        Task<ModelSeries?> GetByNameAndAuthorAsync(string name, int authorId);

        /// <summary>
        /// Gets models in a series.
        /// </summary>
        Task<List<Model>?> GetModelsInSeriesAsync(int seriesId);

        /// <summary>
        /// Creates a new model series.
        /// </summary>
        Task<ModelSeries> CreateAsync(ModelSeries series);

        /// <summary>
        /// Updates an existing model series.
        /// </summary>
        Task<ModelSeries> UpdateAsync(ModelSeries series);

        /// <summary>
        /// Deletes a model series by ID.
        /// </summary>
        Task<bool> DeleteAsync(int id);
    }
}