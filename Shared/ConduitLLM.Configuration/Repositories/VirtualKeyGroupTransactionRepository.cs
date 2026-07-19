using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for virtual key group transactions using Entity Framework Core.
/// Extends RepositoryBase for standard CRUD operations and implements domain-specific methods.
/// </summary>
public class VirtualKeyGroupTransactionRepository : RepositoryBase<VirtualKeyGroupTransaction, long>, IVirtualKeyGroupTransactionRepository
{
    /// <summary>
    /// Creates a new instance of the repository
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public VirtualKeyGroupTransactionRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<VirtualKeyGroupTransactionRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<VirtualKeyGroupTransaction> GetDbSet(ConduitDbContext context)
        => context.VirtualKeyGroupTransactions;

    /// <inheritdoc/>
    protected override IQueryable<VirtualKeyGroupTransaction> ApplyDefaultIncludes(IQueryable<VirtualKeyGroupTransaction> query)
    {
        return query.Include(t => t.VirtualKeyGroup);
    }

    /// <inheritdoc/>
    protected override IQueryable<VirtualKeyGroupTransaction> ApplyDefaultOrdering(IQueryable<VirtualKeyGroupTransaction> query)
    {
        return query.OrderByDescending(t => t.CreatedAt);
    }

    /// <inheritdoc/>
    protected override void OnBeforeCreate(VirtualKeyGroupTransaction entity)
    {
        base.OnBeforeCreate(entity);

        // Set CreatedAt if not provided
        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTime.UtcNow;
        }
    }

    /// <inheritdoc/>
    public async Task<List<VirtualKeyGroupTransaction>> GetByGroupIdAsync(int groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AsNoTracking()
                    .Where(t => t.VirtualKeyGroupId == groupId && !t.IsDeleted)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync(cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting transactions for virtual key group with ID {GroupId}", groupId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<VirtualKeyGroupTransaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AsNoTracking()
                    .Include(t => t.VirtualKeyGroup)
                    .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate && !t.IsDeleted)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync(cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting transactions for date range {StartDate} to {EndDate}", startDate, endDate);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<VirtualKeyGroupTransaction>> GetByGroupIdAndDateRangeAsync(
        int groupId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AsNoTracking()
                    .Where(t => t.VirtualKeyGroupId == groupId && t.CreatedAt >= startDate && t.CreatedAt <= endDate && !t.IsDeleted)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync(cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting transactions for virtual key group {GroupId} and date range {StartDate} to {EndDate}",
                groupId, startDate, endDate);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<decimal> GetTotalCreditsAsync(int groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .Where(t => t.VirtualKeyGroupId == groupId && t.TransactionType == TransactionType.Credit && !t.IsDeleted)
                    .SumAsync(t => t.Amount, cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting total credits for virtual key group {GroupId}", groupId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<decimal> GetTotalDebitsAsync(int groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .Where(t => t.VirtualKeyGroupId == groupId && t.TransactionType == TransactionType.Debit && !t.IsDeleted)
                    .SumAsync(t => t.Amount, cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting total debits for virtual key group {GroupId}", groupId);
            throw;
        }
    }
}
