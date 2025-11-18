using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for function costs using Entity Framework Core.
/// </summary>
public class FunctionCostRepository : IFunctionCostRepository
{
    private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
    private readonly ILogger<FunctionCostRepository> _logger;

    public FunctionCostRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<FunctionCostRepository> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FunctionCost?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCosts
                .AsNoTracking()
                .Include(c => c.FunctionMappings)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function cost with ID {CostId}", LogSanitizer.SanitizeObject(id));
            throw;
        }
    }

    public async Task<FunctionCost?> GetByCostNameAsync(string costName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(costName))
        {
            throw new ArgumentException("Cost name cannot be null or empty", nameof(costName));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCosts
                .AsNoTracking()
                .Include(c => c.FunctionMappings)
                .FirstOrDefaultAsync(c => c.CostName == costName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function cost with name {CostName}",
                LogSanitizer.SanitizeObject(costName));
            throw;
        }
    }

    public async Task<List<FunctionCost>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCosts
                .AsNoTracking()
                .Include(c => c.FunctionMappings)
                .OrderBy(c => c.CostName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all function costs");
            throw;
        }
    }

    public async Task<List<FunctionCost>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTime.UtcNow;

            return await dbContext.FunctionCosts
                .AsNoTracking()
                .Include(c => c.FunctionMappings)
                .Where(c => c.IsActive
                    && c.EffectiveDate <= now
                    && (c.ExpiryDate == null || c.ExpiryDate > now))
                .OrderBy(c => c.CostName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all active function costs");
            throw;
        }
    }

    public async Task<FunctionCost?> GetActiveCostForFunctionAsync(int functionConfigurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTime.UtcNow;

            // Get the active mapping for this function
            var mapping = await dbContext.FunctionCostMappings
                .AsNoTracking()
                .Include(m => m.FunctionCost)
                .Where(m => m.FunctionConfigurationId == functionConfigurationId && m.IsActive)
                .OrderByDescending(m => m.FunctionCost!.Priority)
                .ThenByDescending(m => m.FunctionCost!.EffectiveDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (mapping?.FunctionCost == null)
            {
                return null;
            }

            // Verify the cost is currently active and effective
            if (mapping.FunctionCost.IsActive
                && mapping.FunctionCost.EffectiveDate <= now
                && (mapping.FunctionCost.ExpiryDate == null || mapping.FunctionCost.ExpiryDate > now))
            {
                return mapping.FunctionCost;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active cost for function configuration {ConfigId}",
                LogSanitizer.SanitizeObject(functionConfigurationId));
            throw;
        }
    }

    public async Task<int> CreateAsync(FunctionCost functionCost, CancellationToken cancellationToken = default)
    {
        if (functionCost == null)
        {
            throw new ArgumentNullException(nameof(functionCost));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                functionCost.CreatedAt = DateTime.UtcNow;
                functionCost.UpdatedAt = DateTime.UtcNow;

                dbContext.FunctionCosts.Add(functionCost);
                await dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return functionCost.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while creating function cost '{CostName}'",
                    LogSanitizer.SanitizeObject(LoggingSanitizer.S(functionCost.CostName)));
                throw;
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating function cost '{CostName}'",
                LogSanitizer.SanitizeObject(LoggingSanitizer.S(functionCost.CostName)));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating function cost '{CostName}'",
                LogSanitizer.SanitizeObject(LoggingSanitizer.S(functionCost.CostName)));
            throw;
        }
    }

    public async Task UpdateAsync(FunctionCost functionCost, CancellationToken cancellationToken = default)
    {
        if (functionCost == null)
        {
            throw new ArgumentNullException(nameof(functionCost));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                functionCost.UpdatedAt = DateTime.UtcNow;

                dbContext.FunctionCosts.Update(functionCost);
                await dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while updating function cost with ID {CostId}",
                    LogSanitizer.SanitizeObject(functionCost.Id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating function cost with ID {CostId}",
                LogSanitizer.SanitizeObject(functionCost.Id));
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var functionCost = await dbContext.FunctionCosts
                    .FindAsync(new object[] { id }, cancellationToken);

                if (functionCost != null)
                {
                    dbContext.FunctionCosts.Remove(functionCost);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while deleting function cost with ID {CostId}",
                    LogSanitizer.SanitizeObject(id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function cost with ID {CostId}",
                LogSanitizer.SanitizeObject(id));
            throw;
        }
    }
}
