using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for ModelSeries entity operations.
/// Inherits common CRUD operations from RepositoryBase.
/// </summary>
public class ModelSeriesRepository : RepositoryBase<ModelSeries, int>, IModelSeriesRepository
{
    /// <summary>
    /// Creates a new instance of the repository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public ModelSeriesRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<ModelSeriesRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<ModelSeries> GetDbSet(ConduitDbContext context) => context.ModelSeries;

    /// <inheritdoc/>
    protected override IQueryable<ModelSeries> ApplyDefaultOrdering(IQueryable<ModelSeries> query)
    {
        return query.OrderBy(s => s.Name);
    }

    /// <inheritdoc/>
    public async Task<ModelSeries?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .Include(s => s.Author)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting {EntityType} with author for ID {Id}", EntityTypeName, id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<ModelSeries>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .OrderBy(s => s.Name)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting all {EntityType} entities", EntityTypeName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<ModelSeries>> GetAllWithAuthorAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .Include(s => s.Author)
                    .AsNoTracking()
                    .OrderBy(s => s.Name)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting all {EntityType} entities with author", EntityTypeName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ModelSeries?> GetByNameAndAuthorAsync(string name, int authorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Name == name && s.AuthorId == authorId, cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting {EntityType} by name {Name} and author ID {AuthorId}", EntityTypeName, name, authorId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Model>?> GetModelsInSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                var exists = await GetDbSet(context)
                    .AnyAsync(s => s.Id == seriesId, cancellationToken);

                if (!exists)
                {
                    return null;
                }

                return await context.Models
                    .AsNoTracking()
                    .Where(m => m.ModelSeriesId == seriesId)
                    .OrderBy(m => m.Name)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting models for {EntityType} with ID {SeriesId}", EntityTypeName, seriesId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ModelSeries> CreateSeriesAsync(ModelSeries series, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        try
        {
            return await ExecuteAsync(async context =>
            {
                OnBeforeCreate(series);
                GetDbSet(context).Add(series);
                await context.SaveChangesAsync(cancellationToken);
                return series;
            }, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            Logger.LogError(ex, "Database error creating {EntityType}", EntityTypeName);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating {EntityType}", EntityTypeName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ModelSeries> UpdateSeriesAsync(ModelSeries series, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        try
        {
            return await ExecuteAsync(async context =>
            {
                OnBeforeUpdate(series);
                GetDbSet(context).Update(series);
                await context.SaveChangesAsync(cancellationToken);
                return series;
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogError(ex, "Concurrency error updating {EntityType} with ID {Id}", EntityTypeName, series.Id);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating {EntityType} with ID {Id}", EntityTypeName, series.Id);
            throw;
        }
    }
}
