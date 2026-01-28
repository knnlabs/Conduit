using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository for managing virtual key groups
/// </summary>
public class VirtualKeyGroupRepository : IVirtualKeyGroupRepository
{
    private readonly ConduitDbContext _context;
    private readonly ILogger<VirtualKeyGroupRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the VirtualKeyGroupRepository
    /// </summary>
    public VirtualKeyGroupRepository(ConduitDbContext context, ILogger<VirtualKeyGroupRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VirtualKeyGroup?> GetByIdAsync(int id)
    {
        return await _context.VirtualKeyGroups
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    /// <inheritdoc />
    public async Task<VirtualKeyGroup?> GetByIdWithKeysAsync(int id)
    {
        return await _context.VirtualKeyGroups
            .Include(g => g.VirtualKeys)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    /// <inheritdoc />
    public async Task<VirtualKeyGroup?> GetByKeyIdAsync(int virtualKeyId)
    {
        var key = await _context.VirtualKeys
            .Include(k => k.VirtualKeyGroup)
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == virtualKeyId);

        return key?.VirtualKeyGroup;
    }

    /// <inheritdoc />
    [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
    public async Task<List<VirtualKeyGroup>> GetAllAsync()
    {
        return await _context.VirtualKeyGroups
            .Include(g => g.VirtualKeys)
            .AsNoTracking()
            .OrderBy(g => g.GroupName)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<(List<VirtualKeyGroup> Items, int TotalCount)> GetPaginatedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentException("Page number must be greater than or equal to 1", nameof(pageNumber));
        }

        if (pageSize < 1)
        {
            throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
        }

        const int maxPageSize = 100;
        if (pageSize > maxPageSize)
        {
            _logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                pageSize, maxPageSize);
            pageSize = maxPageSize;
        }

        var query = _context.VirtualKeyGroups
            .Include(g => g.VirtualKeys)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(g => g.GroupName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(VirtualKeyGroup group)
    {
        group.CreatedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;
        
        _context.VirtualKeyGroups.Add(group);
        await _context.SaveChangesAsync();

        // If group was created with initial balance, create a transaction record
        if (group.Balance > 0)
        {
            var transaction = CreateTransaction(
                group.Id,
                group.Balance,
                group.Balance,
                TransactionType.Credit,
                ReferenceType.Initial,
                "Initial balance"
            );

            _context.VirtualKeyGroupTransactions.Add(transaction);
            await _context.SaveChangesAsync();
        }
        
        _logger.LogInformation("Created virtual key group {GroupId} with name {GroupName}", 
            group.Id, group.GroupName);
        
        return group.Id;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(VirtualKeyGroup group)
    {
        group.UpdatedAt = DateTime.UtcNow;
        
        _context.VirtualKeyGroups.Update(group);
        var result = await _context.SaveChangesAsync();
        
        if (result > 0)
        {
            _logger.LogInformation("Updated virtual key group {GroupId}", group.Id);
        }
        
        return result > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        var group = await GetByIdAsync(id);
        if (group == null)
        {
            return false;
        }
        
        _context.VirtualKeyGroups.Remove(group);
        var result = await _context.SaveChangesAsync();
        
        if (result > 0)
        {
            _logger.LogInformation("Deleted virtual key group {GroupId}", id);
        }
        
        return result > 0;
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
        var group = await GetByIdAsync(groupId);
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

        _context.VirtualKeyGroupTransactions.Add(transaction);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Adjusted balance for group {GroupId} by {Amount}. Previous: {PreviousBalance}, New: {Balance}, ReferenceType: {ReferenceType}",
            groupId, amount, previousBalance, group.Balance, referenceType);

        return group.Balance;
    }

    /// <inheritdoc />
    [Obsolete("Use GetLowBalanceGroupsPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
    public async Task<List<VirtualKeyGroup>> GetLowBalanceGroupsAsync(decimal threshold)
    {
        return await _context.VirtualKeyGroups
            .AsNoTracking()
            .Where(g => g.Balance < threshold)
            .OrderBy(g => g.Balance)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<(List<VirtualKeyGroup> Items, int TotalCount)> GetLowBalanceGroupsPaginatedAsync(
        decimal threshold,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentException("Page number must be greater than or equal to 1", nameof(pageNumber));
        }

        if (pageSize < 1)
        {
            throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
        }

        const int maxPageSize = 100;
        if (pageSize > maxPageSize)
        {
            _logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                pageSize, maxPageSize);
            pageSize = maxPageSize;
        }

        var query = _context.VirtualKeyGroups
            .AsNoTracking()
            .Where(g => g.Balance < threshold);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(g => g.Balance)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Creates a transaction record for a virtual key group
    /// </summary>
    private VirtualKeyGroupTransaction CreateTransaction(
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