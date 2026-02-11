using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for model authors using Entity Framework Core.
/// Inherits common CRUD operations from RepositoryBase.
/// </summary>
public class ModelAuthorRepository : RepositoryBase<ModelAuthor, int>, IModelAuthorRepository
{
    /// <summary>
    /// Creates a new instance of the repository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public ModelAuthorRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<ModelAuthorRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<ModelAuthor> GetDbSet(ConduitDbContext context) => context.ModelAuthors;

    /// <inheritdoc/>
    protected override IQueryable<ModelAuthor> ApplyDefaultOrdering(IQueryable<ModelAuthor> query)
    {
        return query.OrderBy(a => a.Name);
    }

    /// <inheritdoc/>
    [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
    public async Task<List<ModelAuthor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            return await GetDbSet(context)
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "getting all");
    }

    /// <inheritdoc/>
    public async Task<ModelAuthor?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            return await GetDbSet(context)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Name == name, cancellationToken);
        }, cancellationToken, $"getting by name {name}");
    }

    /// <inheritdoc/>
    public async Task<List<ModelSeries>?> GetSeriesByAuthorAsync(int authorId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            var exists = await GetDbSet(context)
                .AnyAsync(a => a.Id == authorId, cancellationToken);

            if (!exists)
                return null;

            return await context.ModelSeries
                .AsNoTracking()
                .Where(s => s.AuthorId == authorId)
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken);
        }, cancellationToken, $"getting series for author ID {authorId}");
    }
}
