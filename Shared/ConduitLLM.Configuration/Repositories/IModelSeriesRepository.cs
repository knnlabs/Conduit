using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository interface for ModelSeries entity operations.
/// Inherits standard CRUD operations from IRepositoryBase.
/// </summary>
public interface IModelSeriesRepository : IRepositoryBase<ModelSeries, int>
{
    /// <summary>
    /// Gets a model series by its ID with author information.
    /// </summary>
    /// <param name="id">The series ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model series with author or null if not found</returns>
    Task<ModelSeries?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all model series with author information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all model series with author</returns>
    Task<List<ModelSeries>> GetAllWithAuthorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a model series by name and author.
    /// </summary>
    /// <param name="name">The series name</param>
    /// <param name="authorId">The author ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model series or null if not found</returns>
    Task<ModelSeries?> GetByNameAndAuthorAsync(string name, int authorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets models in a series.
    /// </summary>
    /// <param name="seriesId">The series ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of models in the series or null if series not found</returns>
    Task<List<Model>?> GetModelsInSeriesAsync(int seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new model series and returns the created entity.
    /// Use this when you need the full entity back after creation.
    /// </summary>
    /// <param name="series">The series to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created model series with its assigned ID</returns>
    Task<ModelSeries> CreateSeriesAsync(ModelSeries series, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing model series and returns the updated entity.
    /// Use this when you need the full entity back after update.
    /// </summary>
    /// <param name="series">The series to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated model series</returns>
    Task<ModelSeries> UpdateSeriesAsync(ModelSeries series, CancellationToken cancellationToken = default);
}
