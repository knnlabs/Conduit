using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository for managing virtual key groups
/// </summary>
public class VirtualKeyGroupRepository : IVirtualKeyGroupRepository
{
    private readonly IConfigurationDbContext _context;
    private readonly ILogger<VirtualKeyGroupRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the VirtualKeyGroupRepository
    /// </summary>
    public VirtualKeyGroupRepository(IConfigurationDbContext context, ILogger<VirtualKeyGroupRepository> logger)
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
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    /// <inheritdoc />
    public async Task<VirtualKeyGroup?> GetByKeyIdAsync(int virtualKeyId)
    {
        var key = await _context.VirtualKeys
            .Include(k => k.VirtualKeyGroup)
            .FirstOrDefaultAsync(k => k.Id == virtualKeyId);
        
        return key?.VirtualKeyGroup;
    }

    /// <inheritdoc />
    public async Task<List<VirtualKeyGroup>> GetAllAsync()
    {
        return await _context.VirtualKeyGroups
            .OrderBy(g => g.GroupName)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(VirtualKeyGroup group)
    {
        group.CreatedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;
        
        _context.VirtualKeyGroups.Add(group);
        await _context.SaveChangesAsync();
        
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
        var group = await GetByIdAsync(groupId);
        if (group == null)
        {
            throw new InvalidOperationException($"Virtual key group {groupId} not found");
        }
        
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
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Adjusted balance for group {GroupId} by {Amount}. New balance: {Balance}", 
            groupId, amount, group.Balance);
        
        return group.Balance;
    }

    /// <inheritdoc />
    public async Task<List<VirtualKeyGroup>> GetLowBalanceGroupsAsync(decimal threshold)
    {
        return await _context.VirtualKeyGroups
            .Where(g => g.Balance < threshold)
            .OrderBy(g => g.Balance)
            .ToListAsync();
    }
}