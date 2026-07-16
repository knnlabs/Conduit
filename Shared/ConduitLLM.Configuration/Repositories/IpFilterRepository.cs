using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for IP filter management using Entity Framework Core.
/// Inherits common CRUD operations from RepositoryBase.
/// </summary>
public class IpFilterRepository : RepositoryBase<IpFilterEntity, int>, IIpFilterRepository
{
    /// <summary>
    /// Creates a new instance of the repository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public IpFilterRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<IpFilterRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<IpFilterEntity> GetDbSet(ConduitDbContext context) => context.IpFilters;

    /// <inheritdoc/>
    protected override IQueryable<IpFilterEntity> ApplyDefaultOrdering(IQueryable<IpFilterEntity> query)
    {
        return query
            .OrderBy(f => f.FilterType)
            .ThenBy(f => f.IpAddressOrCidr);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IpFilterEntity>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            return await GetDbSet(context)
                .AsNoTracking()
                .Where(f => f.IsEnabled)
                .OrderBy(f => f.FilterType)
                .ThenBy(f => f.IpAddressOrCidr)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "getting enabled filters");
    }

    /// <inheritdoc/>
    public async Task<IpFilterEntity> AddAsync(IpFilterEntity filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // Base CreateAsync already handles error logging
        await CreateAsync(filter, cancellationToken);

        Logger.LogInformation("Added new IP filter: {FilterType} {IpAddressOrCidr}",
            LoggingSanitizer.S(filter.FilterType),
            LoggingSanitizer.S(filter.IpAddressOrCidr));

        return filter;
    }
}
