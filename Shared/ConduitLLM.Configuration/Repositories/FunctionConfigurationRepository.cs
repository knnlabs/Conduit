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

    /// <summary>
    /// Applies the default includes for function configurations (CostMappings → FunctionCost).
    /// Centralizes the include chain to avoid duplication across query methods.
    /// </summary>
    private static IQueryable<FunctionConfiguration> ApplyDefaultIncludes(IQueryable<FunctionConfiguration> query)
    {
        return query
            .Include(f => f.CostMappings)
                .ThenInclude(cm => cm.FunctionCost);
    }

    public async Task<FunctionConfiguration?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await ApplyDefaultIncludes(dbContext.FunctionConfigurations.AsNoTracking())
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configuration with ID {ConfigId}", LoggingSanitizer.S(id));
            throw;
        }
    }

    public async Task<List<FunctionConfiguration>> GetByIdsAsync(List<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
        {
            return new List<FunctionConfiguration>();
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await ApplyDefaultIncludes(dbContext.FunctionConfigurations.AsNoTracking())
                .Where(f => ids.Contains(f.Id))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configurations with IDs {ConfigIds}", LoggingSanitizer.S(ids));
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
            return await ApplyDefaultIncludes(dbContext.FunctionConfigurations.AsNoTracking())
                .FirstOrDefaultAsync(f => f.ConfigurationName == configurationName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configuration with name {ConfigName}",
                LoggingSanitizer.S(configurationName));
            throw;
        }
    }

    [Obsolete("Use GetAllUnboundedAsync() for cache warming/exports, or GetPaginatedAsync() for bounded queries.")]
    public async Task<List<FunctionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Delegate to GetAllUnboundedAsync to avoid code duplication
        return await GetAllUnboundedAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<FunctionConfiguration>> GetAllUnboundedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Unbounded query executed on FunctionConfiguration via GetAllUnboundedAsync(). " +
            "Ensure this is intentional (cache warming, export, migration).");

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await ApplyDefaultIncludes(dbContext.FunctionConfigurations.AsNoTracking())
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all function configurations (unbounded)");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<(List<FunctionConfiguration> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Validate and normalize pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var query = ApplyDefaultIncludes(dbContext.FunctionConfigurations.AsNoTracking());

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(f => f.ConfigurationName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paginated function configurations (page {Page}, size {PageSize})", page, pageSize);
            throw;
        }
    }

    public async Task<List<FunctionConfiguration>> GetAllEnabledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await ApplyDefaultIncludes(dbContext.FunctionConfigurations.AsNoTracking())
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
            return await ApplyDefaultIncludes(dbContext.FunctionConfigurations.AsNoTracking())
                .Where(f => f.ProviderType == providerType)
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configurations for provider type {ProviderType}",
                LoggingSanitizer.S(providerType));
            throw;
        }
    }

    public async Task<List<FunctionConfiguration>> GetByPurposeAsync(FunctionPurpose purpose, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await ApplyDefaultIncludes(dbContext.FunctionConfigurations.AsNoTracking())
                .Where(f => f.Purpose == purpose)
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configurations for purpose {Purpose}",
                LoggingSanitizer.S(purpose));
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
                    LoggingSanitizer.S(functionConfiguration.ConfigurationName));
                throw;
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating function configuration '{ConfigName}'",
                LoggingSanitizer.S(functionConfiguration.ConfigurationName));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating function configuration '{ConfigName}'",
                LoggingSanitizer.S(functionConfiguration.ConfigurationName));
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
                    LoggingSanitizer.S(functionConfiguration.Id));

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
                        LoggingSanitizer.S(functionConfiguration.Id));
                    throw;
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while updating function configuration with ID {ConfigId}",
                    LoggingSanitizer.S(functionConfiguration.Id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating function configuration with ID {ConfigId}",
                LoggingSanitizer.S(functionConfiguration.Id));
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
                    LoggingSanitizer.S(id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function configuration with ID {ConfigId}",
                LoggingSanitizer.S(id));
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
                LoggingSanitizer.S(configurationName));
            throw;
        }
    }
}
