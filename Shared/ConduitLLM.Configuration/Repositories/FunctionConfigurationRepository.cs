using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for function configurations using Entity Framework Core.
/// </summary>
public class FunctionConfigurationRepository : IFunctionConfigurationRepository
{
    private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
    private readonly ILogger<FunctionConfigurationRepository> _logger;

    public FunctionConfigurationRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<FunctionConfigurationRepository> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FunctionConfiguration?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionConfigurations
                .AsNoTracking()
                .Include(f => f.Credentials)
                .Include(f => f.CostMappings)
                    .ThenInclude(cm => cm.FunctionCost)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configuration with ID {ConfigId}", LogSanitizer.SanitizeObject(id));
            throw;
        }
    }

    public async Task<FunctionConfiguration?> GetByNameAsync(string configurationName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configurationName))
        {
            throw new ArgumentException("Configuration name cannot be null or empty", nameof(configurationName));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionConfigurations
                .AsNoTracking()
                .Include(f => f.Credentials)
                .Include(f => f.CostMappings)
                    .ThenInclude(cm => cm.FunctionCost)
                .FirstOrDefaultAsync(f => f.ConfigurationName == configurationName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configuration with name {ConfigName}",
                LogSanitizer.SanitizeObject(configurationName));
            throw;
        }
    }

    public async Task<List<FunctionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionConfigurations
                .AsNoTracking()
                .Include(f => f.Credentials)
                .Include(f => f.CostMappings)
                    .ThenInclude(cm => cm.FunctionCost)
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all function configurations");
            throw;
        }
    }

    public async Task<List<FunctionConfiguration>> GetAllEnabledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionConfigurations
                .AsNoTracking()
                .Include(f => f.Credentials)
                .Include(f => f.CostMappings)
                    .ThenInclude(cm => cm.FunctionCost)
                .Where(f => f.IsEnabled)
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all enabled function configurations");
            throw;
        }
    }

    public async Task<List<FunctionConfiguration>> GetByProviderTypeAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionConfigurations
                .AsNoTracking()
                .Include(f => f.Credentials)
                .Include(f => f.CostMappings)
                    .ThenInclude(cm => cm.FunctionCost)
                .Where(f => f.ProviderType == providerType)
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configurations for provider type {ProviderType}",
                LogSanitizer.SanitizeObject(providerType));
            throw;
        }
    }

    public async Task<List<FunctionConfiguration>> GetByPurposeAsync(FunctionPurpose purpose, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionConfigurations
                .AsNoTracking()
                .Include(f => f.Credentials)
                .Include(f => f.CostMappings)
                    .ThenInclude(cm => cm.FunctionCost)
                .Where(f => f.Purpose == purpose)
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configurations for purpose {Purpose}",
                LogSanitizer.SanitizeObject(purpose));
            throw;
        }
    }

    public async Task<int> CreateAsync(FunctionConfiguration functionConfiguration, CancellationToken cancellationToken = default)
    {
        if (functionConfiguration == null)
        {
            throw new ArgumentNullException(nameof(functionConfiguration));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                functionConfiguration.CreatedAt = DateTime.UtcNow;
                functionConfiguration.UpdatedAt = DateTime.UtcNow;

                dbContext.FunctionConfigurations.Add(functionConfiguration);
                await dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return functionConfiguration.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while creating function configuration '{ConfigName}'",
                    LogSanitizer.SanitizeObject(LoggingSanitizer.S(functionConfiguration.ConfigurationName)));
                throw;
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating function configuration '{ConfigName}'",
                LogSanitizer.SanitizeObject(LoggingSanitizer.S(functionConfiguration.ConfigurationName)));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating function configuration '{ConfigName}'",
                LogSanitizer.SanitizeObject(LoggingSanitizer.S(functionConfiguration.ConfigurationName)));
            throw;
        }
    }

    public async Task UpdateAsync(FunctionConfiguration functionConfiguration, CancellationToken cancellationToken = default)
    {
        if (functionConfiguration == null)
        {
            throw new ArgumentNullException(nameof(functionConfiguration));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                functionConfiguration.UpdatedAt = DateTime.UtcNow;

                dbContext.FunctionConfigurations.Update(functionConfiguration);
                await dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Concurrency error updating function configuration with ID {ConfigId}",
                    LogSanitizer.SanitizeObject(functionConfiguration.Id));

                // Retry logic
                try
                {
                    using var retryDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                    await using var retryTransaction = await retryDbContext.Database.BeginTransactionAsync(cancellationToken);

                    var existingEntity = await retryDbContext.FunctionConfigurations
                        .FindAsync(new object[] { functionConfiguration.Id }, cancellationToken);

                    if (existingEntity != null)
                    {
                        retryDbContext.Entry(existingEntity).CurrentValues.SetValues(functionConfiguration);
                        existingEntity.UpdatedAt = DateTime.UtcNow;

                        await retryDbContext.SaveChangesAsync(cancellationToken);
                        await retryTransaction.CommitAsync(cancellationToken);
                    }
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Error during retry of function configuration update with ID {ConfigId}",
                        LogSanitizer.SanitizeObject(functionConfiguration.Id));
                    throw;
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while updating function configuration with ID {ConfigId}",
                    LogSanitizer.SanitizeObject(functionConfiguration.Id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating function configuration with ID {ConfigId}",
                LogSanitizer.SanitizeObject(functionConfiguration.Id));
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
                var functionConfiguration = await dbContext.FunctionConfigurations
                    .FindAsync(new object[] { id }, cancellationToken);

                if (functionConfiguration != null)
                {
                    dbContext.FunctionConfigurations.Remove(functionConfiguration);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while deleting function configuration with ID {ConfigId}",
                    LogSanitizer.SanitizeObject(id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function configuration with ID {ConfigId}",
                LogSanitizer.SanitizeObject(id));
            throw;
        }
    }

    public async Task<bool> NameExistsAsync(string configurationName, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configurationName))
        {
            throw new ArgumentException("Configuration name cannot be null or empty", nameof(configurationName));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var query = dbContext.FunctionConfigurations
                .AsNoTracking()
                .Where(f => f.ConfigurationName == configurationName);

            if (excludeId.HasValue)
            {
                query = query.Where(f => f.Id != excludeId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if function configuration name exists: {ConfigName}",
                LogSanitizer.SanitizeObject(configurationName));
            throw;
        }
    }
}
