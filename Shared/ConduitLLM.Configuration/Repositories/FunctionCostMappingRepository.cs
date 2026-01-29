using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for function cost mappings using Entity Framework Core.
/// </summary>
public class FunctionCostMappingRepository : IFunctionCostMappingRepository
{
    private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
    private readonly ILogger<FunctionCostMappingRepository> _logger;

    public FunctionCostMappingRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<FunctionCostMappingRepository> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FunctionCostMapping?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCostMappings
                .AsNoTracking()
                .Include(m => m.FunctionConfiguration)
                .Include(m => m.FunctionCost)
                .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function cost mapping with ID {MappingId}", LoggingSanitizer.S(id));
            throw;
        }
    }

    public async Task<List<FunctionCostMapping>> GetByFunctionConfigurationIdAsync(int functionConfigurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCostMappings
                .AsNoTracking()
                .Include(m => m.FunctionCost)
                .Where(m => m.FunctionConfigurationId == functionConfigurationId)
                .OrderByDescending(m => m.IsActive)
                .ThenByDescending(m => m.FunctionCost!.Priority)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost mappings for function configuration {ConfigId}",
                LoggingSanitizer.S(functionConfigurationId));
            throw;
        }
    }

    public async Task<FunctionCostMapping?> GetActiveMappingAsync(int functionConfigurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCostMappings
                .AsNoTracking()
                .Include(m => m.FunctionCost)
                .Where(m => m.FunctionConfigurationId == functionConfigurationId && m.IsActive)
                .OrderByDescending(m => m.FunctionCost!.Priority)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active mapping for function configuration {ConfigId}",
                LoggingSanitizer.S(functionConfigurationId));
            throw;
        }
    }

    public async Task<int> CreateAsync(FunctionCostMapping mapping, CancellationToken cancellationToken = default)
    {
        if (mapping == null)
        {
            throw new ArgumentNullException(nameof(mapping));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                mapping.CreatedAt = DateTime.UtcNow;

                dbContext.FunctionCostMappings.Add(mapping);
                await dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return mapping.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while creating function cost mapping");
                throw;
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating function cost mapping");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating function cost mapping");
            throw;
        }
    }

    public async Task UpdateAsync(FunctionCostMapping mapping, CancellationToken cancellationToken = default)
    {
        if (mapping == null)
        {
            throw new ArgumentNullException(nameof(mapping));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                dbContext.FunctionCostMappings.Update(mapping);
                await dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while updating function cost mapping with ID {MappingId}",
                    LoggingSanitizer.S(mapping.Id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating function cost mapping with ID {MappingId}",
                LoggingSanitizer.S(mapping.Id));
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
                var mapping = await dbContext.FunctionCostMappings
                    .FindAsync(new object[] { id }, cancellationToken);

                if (mapping != null)
                {
                    dbContext.FunctionCostMappings.Remove(mapping);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while deleting function cost mapping with ID {MappingId}",
                    LoggingSanitizer.S(id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function cost mapping with ID {MappingId}",
                LoggingSanitizer.S(id));
            throw;
        }
    }

    public async Task DeactivateAllForFunctionAsync(int functionConfigurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var mappings = await dbContext.FunctionCostMappings
                    .Where(m => m.FunctionConfigurationId == functionConfigurationId && m.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var mapping in mappings)
                {
                    mapping.IsActive = false;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while deactivating mappings for function {ConfigId}",
                    LoggingSanitizer.S(functionConfigurationId));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating mappings for function configuration {ConfigId}",
                LoggingSanitizer.S(functionConfigurationId));
            throw;
        }
    }
}
