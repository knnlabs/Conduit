using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for virtual key spend history using Entity Framework Core.
/// Extends RepositoryBase for standard CRUD operations and implements domain-specific methods.
/// </summary>
public class VirtualKeySpendHistoryRepository : RepositoryBase<VirtualKeySpendHistory, int>, IVirtualKeySpendHistoryRepository
{
    /// <summary>
    /// Creates a new instance of the repository
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public VirtualKeySpendHistoryRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<VirtualKeySpendHistoryRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<VirtualKeySpendHistory> GetDbSet(ConduitDbContext context)
        => context.VirtualKeySpendHistory;

    /// <inheritdoc/>
    protected override IQueryable<VirtualKeySpendHistory> ApplyDefaultIncludes(IQueryable<VirtualKeySpendHistory> query)
    {
        return query.Include(h => h.VirtualKey);
    }

    /// <inheritdoc/>
    protected override IQueryable<VirtualKeySpendHistory> ApplyDefaultOrdering(IQueryable<VirtualKeySpendHistory> query)
    {
        return query.OrderByDescending(h => h.Timestamp);
    }

    /// <inheritdoc/>
    protected override void OnBeforeCreate(VirtualKeySpendHistory entity)
    {
        base.OnBeforeCreate(entity);

        // Set timestamp if not provided
        if (entity.Timestamp == default)
        {
            entity.Timestamp = DateTime.UtcNow;
        }
    }

    /// <inheritdoc/>
    public async Task<List<VirtualKeySpendHistory>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AsNoTracking()
                    .Where(h => h.VirtualKeyId == virtualKeyId)
                    .OrderByDescending(h => h.Timestamp)
                    .ToListAsync(cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting spend history for virtual key with ID {VirtualKeyId}", virtualKeyId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<VirtualKeySpendHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AsNoTracking()
                    .Include(h => h.VirtualKey)
                    .Where(h => h.Timestamp >= startDate && h.Timestamp <= endDate)
                    .OrderByDescending(h => h.Timestamp)
                    .ToListAsync(cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting spend history for date range {StartDate} to {EndDate}", startDate, endDate);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<VirtualKeySpendHistory>> GetByVirtualKeyAndDateRangeAsync(
        int virtualKeyId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AsNoTracking()
                    .Where(h => h.VirtualKeyId == virtualKeyId && h.Timestamp >= startDate && h.Timestamp <= endDate)
                    .OrderByDescending(h => h.Timestamp)
                    .ToListAsync(cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting spend history for virtual key {VirtualKeyId} and date range {StartDate} to {EndDate}",
                virtualKeyId, startDate, endDate);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<decimal> GetTotalSpendAsync(int virtualKeyId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .Where(h => h.VirtualKeyId == virtualKeyId)
                    .SumAsync(h => h.Amount, cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting total spend for virtual key {VirtualKeyId}", virtualKeyId);
            throw;
        }
    }
}
