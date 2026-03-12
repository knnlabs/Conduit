using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository interface for managing model authors.
/// Inherits standard CRUD operations from IRepositoryBase.
/// </summary>
public interface IModelAuthorRepository : IRepositoryBase<ModelAuthor, int>
{
    /// <summary>
    /// Gets a model author by name.
    /// </summary>
    /// <param name="name">The name of the model author</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model author if found, null otherwise</returns>
    Task<ModelAuthor?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all model series by a specific author.
    /// </summary>
    /// <param name="authorId">The ID of the author</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of model series if author exists, null if author not found</returns>
    Task<List<ModelSeries>?> GetSeriesByAuthorAsync(int authorId, CancellationToken cancellationToken = default);
}
