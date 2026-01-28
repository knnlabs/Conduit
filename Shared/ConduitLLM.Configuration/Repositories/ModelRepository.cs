using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for Model entity operations.
/// Inherits common CRUD operations from RepositoryBase.
/// </summary>
public class ModelRepository : RepositoryBase<Model, int>, IModelRepository
{
    /// <summary>
    /// Creates a new instance of the repository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public ModelRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<ModelRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<Model> GetDbSet(ConduitDbContext context) => context.Models;

    /// <inheritdoc/>
    protected override IQueryable<Model> ApplyDefaultIncludes(IQueryable<Model> query)
    {
        return query
            .Include(m => m.Series)
            .Include(m => m.Identifiers);
    }

    /// <inheritdoc/>
    protected override IQueryable<Model> ApplyDefaultOrdering(IQueryable<Model> query)
    {
        return query.OrderBy(m => m.Name);
    }

    /// <inheritdoc/>
    public async Task<Model?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .Include(m => m.Series)
                        .ThenInclude(s => s.Author)
                    .Include(m => m.Identifiers)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting {EntityType} with details for ID {Id}", EntityTypeName, id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Model>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .OrderBy(m => m.Name)
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
    public async Task<List<Model>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .Include(m => m.Series)
                        .ThenInclude(s => s.Author)
                    .AsNoTracking()
                    .OrderBy(m => m.Name)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting all {EntityType} entities with details", EntityTypeName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Model?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }

        try
        {
            return await ExecuteAsync(async context =>
            {
                // First check ModelProviderTypeAssociation table
                var modelIdentifier = await context.Set<ModelProviderTypeAssociation>()
                    .Include(mi => mi.Model)
                        .ThenInclude(m => m.Series)
                    .AsNoTracking()
                    .Where(mi => mi.Identifier == identifier)
                    .OrderBy(mi => mi.IsPrimary ? 0 : 1) // Prefer primary identifier
                    .FirstOrDefaultAsync(cancellationToken);

                if (modelIdentifier != null)
                {
                    return modelIdentifier.Model;
                }

                // Fallback: Check by model name
                return await GetDbSet(context)
                    .Include(m => m.Series)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Name == identifier, cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting {EntityType} by identifier {Identifier}", EntityTypeName, identifier);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Model>> GetBySeriesAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .Where(m => m.ModelSeriesId == seriesId)
                    .OrderBy(m => m.Name)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting {EntityType} entities by series ID {SeriesId}", EntityTypeName, seriesId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Model?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
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
                    .FirstOrDefaultAsync(m => m.Name == name, cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting {EntityType} by name {Name}", EntityTypeName, name);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Model>> SearchByNameAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            return new List<Model>();
        }

        try
        {
            var lowerQuery = query.ToLower();
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .Where(m => m.Name.ToLower().Contains(lowerQuery) && m.IsActive)
                    .OrderBy(m => m.Name)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching {EntityType} by name query {Query}", EntityTypeName, query);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasMappingReferencesAsync(int modelId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await context.Set<ModelProviderMapping>()
                    .Include(m => m.ModelProviderTypeAssociation)
                    .AnyAsync(m => m.ModelProviderTypeAssociation != null && m.ModelProviderTypeAssociation.ModelId == modelId, cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking mapping references for {EntityType} with ID {Id}", EntityTypeName, modelId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Model>> GetByProviderAsync(ProviderType providerType, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                // Get model IDs that have identifiers for this provider
                var modelIds = await context.Set<ModelProviderTypeAssociation>()
                    .AsNoTracking()
                    .Where(mi => mi.Provider == providerType)
                    .Select(mi => mi.ModelId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                // Return models with those IDs, including series, author, and identifiers
                return await GetDbSet(context)
                    .Include(m => m.Series)
                        .ThenInclude(s => s.Author)
                    .Include(m => m.Identifiers)
                    .AsNoTracking()
                    .Where(m => modelIds.Contains(m.Id))
                    .OrderBy(m => m.Name)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting {EntityType} entities by provider {ProviderType}", EntityTypeName, providerType);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteIdentifierAsync(int modelId, int identifierId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                var identifier = await context.Set<ModelProviderTypeAssociation>()
                    .FirstOrDefaultAsync(i => i.Id == identifierId && i.ModelId == modelId, cancellationToken);

                if (identifier == null)
                {
                    return false;
                }

                context.Set<ModelProviderTypeAssociation>().Remove(identifier);
                int rowsAffected = await context.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting identifier {IdentifierId} for {EntityType} with ID {ModelId}", identifierId, EntityTypeName, modelId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Model> CreateModelAsync(Model model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            return await ExecuteAsync(async context =>
            {
                OnBeforeCreate(model);
                GetDbSet(context).Add(model);
                await context.SaveChangesAsync(cancellationToken);
                return model;
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
    public async Task<Model> UpdateModelAsync(Model model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            return await ExecuteAsync(async context =>
            {
                OnBeforeUpdate(model);
                GetDbSet(context).Update(model);
                await context.SaveChangesAsync(cancellationToken);
                return model;
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogError(ex, "Concurrency error updating {EntityType} with ID {Id}", EntityTypeName, model.Id);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating {EntityType} with ID {Id}", EntityTypeName, model.Id);
            throw;
        }
    }
}
