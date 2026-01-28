using ConduitLLM.Functions.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Abstract base class providing common repository functionality for function-related entities.
/// This mirrors RepositoryBase but uses IFunctionEntity to avoid circular project dependencies.
/// Derived classes only need to implement GetDbSet() and can override other methods as needed.
/// </summary>
/// <typeparam name="TEntity">The entity type (must implement IFunctionEntity)</typeparam>
/// <typeparam name="TKey">The primary key type (must implement IEquatable)</typeparam>
public abstract class FunctionRepositoryBase<TEntity, TKey>
    where TEntity : class, IFunctionEntity<TKey>
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
    protected FunctionRepositoryBase(
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
    /// Executes a custom query using the database context.
    /// Use this for complex queries that don't fit the standard CRUD pattern.
    /// </summary>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the operation</returns>
    protected async Task<TResult> ExecuteAsync<TResult>(
        Func<ConduitDbContext, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
        return await operation(context);
    }

    /// <summary>
    /// Executes a custom operation using the database context with no return value.
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected async Task ExecuteAsync(
        Func<ConduitDbContext, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
        await operation(context);
    }

    /// <summary>
    /// Gets an entity by its primary key.
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
            var query = GetDbSet(context).AsNoTracking();
            query = ApplyDefaultIncludes(query);
            return await query.FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting {EntityType} with ID {Id}", EntityTypeName, id);
            throw;
        }
    }

    /// <summary>
    /// Gets a paginated list of entities.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple containing the items and total count</returns>
    public virtual async Task<(List<TEntity> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Validate and normalize pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
            var query = GetDbSet(context).AsNoTracking();
            query = ApplyDefaultIncludes(query);

            var totalCount = await query.CountAsync(cancellationToken);

            query = ApplyDefaultOrdering(query);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting paginated {EntityType} (page {Page}, size {PageSize})",
                EntityTypeName, page, pageSize);
            throw;
        }
    }

    /// <summary>
    /// Checks if an entity with the given ID exists.
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity exists, false otherwise</returns>
    public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
            return await GetDbSet(context)
                .AsNoTracking()
                .AnyAsync(e => e.Id.Equals(id), cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking existence of {EntityType} with ID {Id}", EntityTypeName, id);
            throw;
        }
    }

    /// <summary>
    /// Gets the total count of entities.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The total count of entities</returns>
    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
            return await GetDbSet(context).CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error counting {EntityType} entities", EntityTypeName);
            throw;
        }
    }

    /// <summary>
    /// Deletes an entity by its primary key.
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the deletion was successful, false otherwise</returns>
    public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
            var dbSet = GetDbSet(context);

            var entity = await dbSet.FindAsync(new object[] { id! }, cancellationToken);
            if (entity == null)
            {
                return false;
            }

            dbSet.Remove(entity);
            int rowsAffected = await context.SaveChangesAsync(cancellationToken);
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting {EntityType} with ID {Id}", EntityTypeName, id);
            throw;
        }
    }
}
