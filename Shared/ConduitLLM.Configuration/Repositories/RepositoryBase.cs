using ConduitLLM.Configuration.Entities.Interfaces;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Functions.Entities.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Abstract base class providing common repository functionality for CRUD operations.
/// Derived classes only need to implement GetDbSet() and can override other methods as needed.
/// Constrains on IIdentifiableEntity to support both configuration entities (IEntity)
/// and function entities (IIdentifiableEntity) without duplication.
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TKey">The primary key type (must implement IEquatable)</typeparam>
public abstract class RepositoryBase<TEntity, TKey> : IRepositoryBase<TEntity, TKey>
    where TEntity : class, IIdentifiableEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// The database context factory for creating short-lived contexts.
    /// </summary>
    protected readonly IDbContextFactory<ConduitDbContext> DbContextFactory;

    /// <summary>
    /// The logger instance for this repository.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Maximum page size for paginated queries. Override in derived class if needed.
    /// </summary>
    protected virtual int MaxPageSize => 100;

    /// <summary>
    /// Default page size when page size is not specified or invalid.
    /// </summary>
    protected virtual int DefaultPageSize => 20;

    /// <summary>
    /// Gets the entity type name for logging purposes.
    /// </summary>
    protected virtual string EntityTypeName => typeof(TEntity).Name;

    /// <summary>
    /// Creates a new instance of the repository base.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    protected RepositoryBase(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger logger)
    {
        DbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the DbSet for the entity type. Must be implemented by derived classes.
    /// </summary>
    /// <param name="context">The database context</param>
    /// <returns>The DbSet for the entity type</returns>
    protected abstract DbSet<TEntity> GetDbSet(ConduitDbContext context);

    /// <summary>
    /// Applies default includes for navigation properties. Override to include related entities.
    /// </summary>
    /// <param name="query">The queryable to extend</param>
    /// <returns>The query with includes applied</returns>
    protected virtual IQueryable<TEntity> ApplyDefaultIncludes(IQueryable<TEntity> query)
    {
        return query;
    }

    /// <summary>
    /// Applies default ordering to a query. Override to customize sort order.
    /// Default implementation orders by Id descending (newest first).
    /// </summary>
    /// <param name="query">The queryable to order</param>
    /// <returns>The ordered query</returns>
    protected virtual IQueryable<TEntity> ApplyDefaultOrdering(IQueryable<TEntity> query)
    {
        return query.OrderByDescending(e => e.Id);
    }

    /// <summary>
    /// Called before creating an entity. Override to set default values.
    /// Default implementation sets CreatedAt and UpdatedAt for IAuditableEntity.
    /// </summary>
    /// <param name="entity">The entity being created</param>
    protected virtual void OnBeforeCreate(TEntity entity)
    {
        if (entity is IAuditableEntity auditable)
        {
            var now = DateTime.UtcNow;
            if (auditable.CreatedAt == default)
            {
                auditable.CreatedAt = now;
            }
            auditable.UpdatedAt = now;
        }
    }

    /// <summary>
    /// Called before updating an entity. Override to set default values.
    /// Default implementation sets UpdatedAt for IAuditableEntity.
    /// </summary>
    /// <param name="entity">The entity being updated</param>
    protected virtual void OnBeforeUpdate(TEntity entity)
    {
        if (entity is IAuditableEntity auditable)
        {
            auditable.UpdatedAt = DateTime.UtcNow;
        }
    }

    #region ExecuteAsync helpers

    /// <summary>
    /// Executes a database operation. When <paramref name="operationName"/> is provided,
    /// exceptions are logged with the entity type before re-throwing.
    /// </summary>
    protected async Task<TResult> ExecuteAsync<TResult>(
        Func<ConduitDbContext, Task<TResult>> operation,
        CancellationToken cancellationToken = default,
        string? operationName = null)
    {
        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
            return await operation(context);
        }
        catch (Exception ex) when (operationName != null)
        {
            Logger.LogError(ex, "Error {OperationName} {EntityType}", operationName, EntityTypeName);
            throw;
        }
    }

    /// <summary>
    /// Executes a void database operation. When <paramref name="operationName"/> is provided,
    /// exceptions are logged with the entity type before re-throwing.
    /// </summary>
    protected async Task ExecuteAsync(
        Func<ConduitDbContext, Task> operation,
        CancellationToken cancellationToken = default,
        string? operationName = null)
    {
        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
            await operation(context);
        }
        catch (Exception ex) when (operationName != null)
        {
            Logger.LogError(ex, "Error {OperationName} {EntityType}", operationName, EntityTypeName);
            throw;
        }
    }

    #endregion

    #region Standard CRUD operations

    /// <inheritdoc/>
    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            var query = GetDbSet(context).AsNoTracking();
            query = ApplyDefaultIncludes(query);
            return await query.FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken);
        }, cancellationToken, $"getting by ID {id}");
    }

    /// <inheritdoc/>
    public virtual async Task<TKey> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);

            OnBeforeCreate(entity);

            GetDbSet(context).Add(entity);
            await context.SaveChangesAsync(cancellationToken);

            return entity.Id;
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
    public virtual async Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);

            OnBeforeUpdate(entity);

            GetDbSet(context).Update(entity);
            int rowsAffected = await context.SaveChangesAsync(cancellationToken);

            return rowsAffected > 0;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogError(ex, "Concurrency error updating {EntityType} with ID {Id}", EntityTypeName, entity.Id);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating {EntityType} with ID {Id}", EntityTypeName, entity.Id);
            throw;
        }
    }

    /// <inheritdoc/>
    public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            var dbSet = GetDbSet(context);

            var entity = await dbSet.FindAsync(new object[] { id! }, cancellationToken);
            if (entity == null)
            {
                return false;
            }

            // Check if entity supports soft delete
            if (entity is ISoftDeletable softDeletable)
            {
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = DateTime.UtcNow;
                dbSet.Update(entity);
            }
            else
            {
                dbSet.Remove(entity);
            }

            int rowsAffected = await context.SaveChangesAsync(cancellationToken);
            return rowsAffected > 0;
        }, cancellationToken, $"deleting by ID {id}");
    }

    /// <inheritdoc/>
    public virtual async Task<(List<TEntity> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Validate and normalize pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        return await ExecuteAsync(async context =>
        {
            var query = GetDbSet(context).AsNoTracking();
            query = ApplyDefaultIncludes(query);

            var totalCount = await query.CountAsync(cancellationToken);

            query = ApplyDefaultOrdering(query);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }, cancellationToken, $"getting paginated (page {page}, size {pageSize})");
    }

    /// <inheritdoc/>
    public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .AsNoTracking()
                .AnyAsync(e => e.Id.Equals(id), cancellationToken),
            cancellationToken, $"checking existence of ID {id}");
    }

    /// <inheritdoc/>
    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context).CountAsync(cancellationToken),
            cancellationToken, "counting entities");
    }

    /// <inheritdoc/>
    public virtual async Task<List<TEntity>> GetAllUnboundedAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogWarning(
            "Unbounded query executed on {EntityType} via GetAllUnboundedAsync(). " +
            "Ensure this is intentional (cache warming, export, migration).",
            EntityTypeName);

        return await ExecuteAsync(async context =>
        {
            var query = GetDbSet(context).AsNoTracking();
            query = ApplyDefaultIncludes(query);
            query = ApplyDefaultOrdering(query);
            return await query.ToListAsync(cancellationToken);
        }, cancellationToken, "getting all (unbounded)");
    }

    #endregion
}
