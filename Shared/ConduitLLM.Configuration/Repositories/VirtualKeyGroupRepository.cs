using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for managing virtual key groups.
/// Extends RepositoryBase for standard CRUD operations and implements domain-specific methods.
/// </summary>
public class VirtualKeyGroupRepository : RepositoryBase<VirtualKeyGroup, int>, IVirtualKeyGroupRepository
{
    /// <summary>
    /// Creates a new instance of the VirtualKeyGroupRepository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public VirtualKeyGroupRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<VirtualKeyGroupRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<VirtualKeyGroup> GetDbSet(ConduitDbContext context)
        => context.VirtualKeyGroups;

    /// <inheritdoc/>
    protected override IQueryable<VirtualKeyGroup> ApplyDefaultIncludes(IQueryable<VirtualKeyGroup> query)
    {
        return query.Include(g => g.VirtualKeys);
    }

    /// <inheritdoc/>
    protected override IQueryable<VirtualKeyGroup> ApplyDefaultOrdering(IQueryable<VirtualKeyGroup> query)
    {
        return query.OrderBy(g => g.GroupName);
    }

    /// <summary>
    /// Overrides CreateAsync to handle initial balance transaction creation.
    /// </summary>
    public override async Task<int> CreateAsync(VirtualKeyGroup entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            return await ExecuteAsync(async context =>
            {
                OnBeforeCreate(entity);

                GetDbSet(context).Add(entity);
                await context.SaveChangesAsync(cancellationToken);

                // If group was created with initial balance, create a transaction record
                if (entity.Balance > 0)
                {
                    var transaction = CreateTransaction(
                        entity.Id,
                        entity.Balance,
                        entity.Balance,
                        TransactionType.Credit,
                        ReferenceType.Initial,
                        "Initial balance"
                    );

                    context.VirtualKeyGroupTransactions.Add(transaction);
                    await context.SaveChangesAsync(cancellationToken);
                }

                Logger.LogInformation("Created virtual key group {GroupId} with name {GroupName}",
                    entity.Id, entity.GroupName);

                return entity.Id;
            }, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            Logger.LogError(ex, "Database error creating virtual key group");
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating virtual key group");
            throw;
        }
    }

    /// <summary>
    /// Overrides UpdateAsync to provide logging.
    /// </summary>
    public override async Task<bool> UpdateAsync(VirtualKeyGroup entity, CancellationToken cancellationToken = default)
    {
        var result = await base.UpdateAsync(entity, cancellationToken);

        if (result)
        {
            Logger.LogInformation("Updated virtual key group {GroupId}", entity.Id);
        }

        return result;
    }

    /// <summary>
    /// Overrides DeleteAsync to provide logging.
    /// </summary>
    public override async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await base.DeleteAsync(id, cancellationToken);

        if (result)
        {
            Logger.LogInformation("Deleted virtual key group {GroupId}", id);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<VirtualKeyGroup?> GetByIdWithKeysAsync(int id)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .Include(g => g.VirtualKeys)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == id));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting virtual key group {GroupId} with keys", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<VirtualKeyGroup?> GetByKeyIdAsync(int virtualKeyId)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                var key = await context.VirtualKeys
                    .Include(k => k.VirtualKeyGroup)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => k.Id == virtualKeyId);

                return key?.VirtualKeyGroup;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting virtual key group by key ID {VirtualKeyId}", virtualKeyId);
            throw;
        }
    }

    /// <inheritdoc />
    [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
    public async Task<List<VirtualKeyGroup>> GetAllAsync()
    {
        return await ExecuteAsync(async context =>
        {
            var query = GetDbSet(context).AsNoTracking();
            query = ApplyDefaultIncludes(query);
            query = ApplyDefaultOrdering(query);
            return await query.ToListAsync();
        });
    }

    /// <inheritdoc />
    public async Task<decimal> AdjustBalanceAsync(int groupId, decimal amount)
    {
        return await AdjustBalanceAsync(groupId, amount, null, null, ReferenceType.Manual, null);
    }

    /// <inheritdoc />
    public async Task<decimal> AdjustBalanceAsync(int groupId, decimal amount, string? description, string? initiatedBy)
    {
        return await AdjustBalanceAsync(groupId, amount, description, initiatedBy, ReferenceType.Manual, null);
    }

    /// <inheritdoc />
    public async Task<decimal> AdjustBalanceAsync(int groupId, decimal amount, string? description, string? initiatedBy, ReferenceType referenceType, string? referenceId = null)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                var group = await GetDbSet(context).FirstOrDefaultAsync(g => g.Id == groupId);
                if (group == null)
                {
                    throw new InvalidOperationException($"Virtual key group {groupId} not found");
                }

                var previousBalance = group.Balance;
                group.Balance += amount;

                if (amount > 0)
                {
                    group.LifetimeCreditsAdded += amount;
                }
                else
                {
                    group.LifetimeSpent += Math.Abs(amount);
                }

                group.UpdatedAt = DateTime.UtcNow;

                // Create transaction record
                var transaction = CreateTransaction(
                    groupId,
                    amount,
                    group.Balance,
                    amount > 0 ? TransactionType.Credit : TransactionType.Debit,
                    referenceType,
                    description ?? (amount > 0 ? "Credits added" : "Usage deducted"),
                    referenceId,
                    initiatedBy ?? "System"
                );

                context.VirtualKeyGroupTransactions.Add(transaction);

                await context.SaveChangesAsync();

                Logger.LogInformation("Adjusted balance for group {GroupId} by {Amount}. Previous: {PreviousBalance}, New: {Balance}, ReferenceType: {ReferenceType}",
                    groupId, amount, previousBalance, group.Balance, referenceType);

                return group.Balance;
            });
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adjusting balance for virtual key group {GroupId}", groupId);
            throw;
        }
    }

    /// <inheritdoc />
    [Obsolete("Use GetLowBalanceGroupsPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
    public async Task<List<VirtualKeyGroup>> GetLowBalanceGroupsAsync(decimal threshold)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .AsNoTracking()
                .Where(g => g.Balance < threshold)
                .OrderBy(g => g.Balance)
                .ToListAsync());
    }

    /// <inheritdoc />
    public async Task<(List<VirtualKeyGroup> Items, int TotalCount)> GetLowBalanceGroupsPaginatedAsync(
        decimal threshold,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        try
        {
            return await ExecuteAsync(async context =>
            {
                var query = GetDbSet(context)
                    .AsNoTracking()
                    .Where(g => g.Balance < threshold);

                var totalCount = await query.CountAsync(cancellationToken);

                var items = await query
                    .OrderBy(g => g.Balance)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (items, totalCount);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting low balance groups with threshold {Threshold}", threshold);
            throw;
        }
    }

    /// <summary>
    /// Creates a transaction record for a virtual key group
    /// </summary>
    private static VirtualKeyGroupTransaction CreateTransaction(
        int groupId,
        decimal amount,
        decimal balanceAfter,
        TransactionType transactionType,
        ReferenceType referenceType,
        string? description = null,
        string? referenceId = null,
        string initiatedBy = "System",
        string? initiatedByUserId = null)
    {
        return new VirtualKeyGroupTransaction
        {
            VirtualKeyGroupId = groupId,
            TransactionType = transactionType,
            Amount = Math.Abs(amount), // Always store positive
            BalanceAfter = balanceAfter,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Description = description,
            InitiatedBy = initiatedBy,
            InitiatedByUserId = initiatedByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
