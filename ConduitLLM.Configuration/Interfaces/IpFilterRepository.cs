using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Configuration.Utilities.LogSanitizer;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository implementation for IP filter management
/// </summary>
public class IpFilterRepository : IIpFilterRepository
{
    private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
    private readonly ILogger<IpFilterRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IpFilterRepository"/> class
    /// </summary>
    /// <param name="dbContextFactory">Database context factory</param>
    /// <param name="logger">Logger</param>
    public IpFilterRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<IpFilterRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IpFilterEntity>> GetAllAsync()
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.IpFilters
                .OrderBy(f => f.FilterType)
                .ThenBy(f => f.IpAddressOrCidr)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting all IP filters");
            return Enumerable.Empty<IpFilterEntity>();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IpFilterEntity>> GetEnabledAsync()
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.IpFilters
                .Where(f => f.IsEnabled)
                .OrderBy(f => f.FilterType)
                .ThenBy(f => f.IpAddressOrCidr)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting enabled IP filters");
            return Enumerable.Empty<IpFilterEntity>();
        }
    }

    /// <inheritdoc/>
    public async Task<IpFilterEntity?> GetByIdAsync(int id)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.IpFilters.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting IP filter with ID {Id}",
                id);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IpFilterEntity> AddAsync(IpFilterEntity filter)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Set default dates
            filter.CreatedAt = DateTime.UtcNow;
            filter.UpdatedAt = DateTime.UtcNow;

            dbContext.IpFilters.Add(filter);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Added new IP filter: {FilterType} {IpAddressOrCidr}",
                filter.FilterType.Replace(Environment.NewLine, ""),
                filter.IpAddressOrCidr.Replace(Environment.NewLine, ""));

            return filter;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding IP filter for {IpAddressOrCidr}", filter.IpAddressOrCidr.Replace(Environment.NewLine, ""));
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAsync(IpFilterEntity filter)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var existingFilter = await dbContext.IpFilters.FindAsync(filter.Id);
            if (existingFilter == null)
            {
                _logger.LogWarning("IP filter with ID {Id} not found for update",
                filter.Id);
                return false;
            }

            // Update properties
            existingFilter.FilterType = filter.FilterType;
            existingFilter.IpAddressOrCidr = filter.IpAddressOrCidr;
            existingFilter.Description = filter.Description;
            existingFilter.IsEnabled = filter.IsEnabled;
            existingFilter.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated IP filter ID {Id}: {FilterType} {IpAddressOrCidr}",
                filter.Id,
                filter.FilterType.Replace(Environment.NewLine, ""),
                filter.IpAddressOrCidr.Replace(Environment.NewLine, ""));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating IP filter with ID {Id}",
                filter.Id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var filter = await dbContext.IpFilters.FindAsync(id);
            if (filter == null)
            {
                _logger.LogWarning("IP filter with ID {Id} not found for deletion",
                id);
                return false;
            }

            dbContext.IpFilters.Remove(filter);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted IP filter ID {Id}: {FilterType} {IpAddressOrCidr}",
                id,
                filter.FilterType.Replace(Environment.NewLine, ""),
                filter.IpAddressOrCidr.Replace(Environment.NewLine, ""));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deleting IP filter with ID {Id}",
                id);
            throw;
        }
    }
}
