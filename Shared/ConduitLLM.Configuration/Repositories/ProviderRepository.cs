using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for providers using Entity Framework Core.
/// Inherits common CRUD operations from RepositoryBase.
/// </summary>
public class ProviderRepository : RepositoryBase<Provider, int>, IProviderRepository
{
    /// <summary>
    /// Creates a new instance of the repository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public ProviderRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<ProviderRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<Provider> GetDbSet(ConduitDbContext context) => context.Providers;

    /// <inheritdoc/>
    protected override IQueryable<Provider> ApplyDefaultIncludes(IQueryable<Provider> query)
    {
        return query.Include(p => p.ProviderKeyCredentials);
    }

    /// <inheritdoc/>
    protected override IQueryable<Provider> ApplyDefaultOrdering(IQueryable<Provider> query)
    {
        return query.OrderBy(p => p.ProviderType);
    }

    /// <inheritdoc/>
    [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
    public async Task<List<Provider>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .Include(p => p.ProviderKeyCredentials)
                    .AsNoTracking()
                    .OrderBy(p => p.ProviderType)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting all providers");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<int, string>> GetProviderNameMapAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .ToDictionaryAsync(p => p.Id, p => p.ProviderName ?? p.ProviderType.ToString(), cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting provider name map");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(bool? enabledOnly, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                var query = GetDbSet(context).AsNoTracking();

                if (enabledOnly.HasValue)
                {
                    query = query.Where(p => p.IsEnabled == enabledOnly.Value);
                }

                return await query.CountAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error counting providers (enabledOnly: {EnabledOnly})", enabledOnly);
            throw;
        }
    }
}
