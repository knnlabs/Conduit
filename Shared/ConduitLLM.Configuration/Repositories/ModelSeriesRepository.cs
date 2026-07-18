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
        return await ExecuteAsync(async context =>
        {
            return await GetDbSet(context)
                .Include(s => s.Author)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        }, cancellationToken, $"getting with author for ID {id}");
    }

    /// <inheritdoc/>
    public async Task<List<ModelSeries>> GetAllWithAuthorAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            return await GetDbSet(context)
                .Include(s => s.Author)
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "getting all with author");
    }

    /// <inheritdoc/>
    public async Task<ModelSeries?> GetByNameAndAuthorAsync(string name, int authorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return await ExecuteAsync(async context =>
        {
            return await GetDbSet(context)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Name == name && s.AuthorId == authorId, cancellationToken);
        }, cancellationToken, $"getting by name {name} and author ID {authorId}");
    }

    /// <inheritdoc/>
    public async Task<List<Model>?> GetModelsInSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
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
        }, cancellationToken, $"getting models for series ID {seriesId}");
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
