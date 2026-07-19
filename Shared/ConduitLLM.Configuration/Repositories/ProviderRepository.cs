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
    public async Task<Dictionary<int, string>> GetProviderNameMapAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            return await GetDbSet(context)
                .AsNoTracking()
                .ToDictionaryAsync(p => p.Id, p => p.ProviderName ?? p.ProviderType.ToString(), cancellationToken);
        }, cancellationToken, "getting provider name map");
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(bool? enabledOnly, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            var query = GetDbSet(context).AsNoTracking();

            if (enabledOnly.HasValue)
            {
                query = query.Where(p => p.IsEnabled == enabledOnly.Value);
            }

            return await query.CountAsync(cancellationToken);
        }, cancellationToken, $"counting (enabledOnly: {enabledOnly})");
    }
}
