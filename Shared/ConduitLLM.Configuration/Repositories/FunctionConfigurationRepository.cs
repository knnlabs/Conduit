using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for function configurations using RepositoryBase.
/// </summary>
public class FunctionConfigurationRepository : RepositoryBase<FunctionConfiguration, int>, IFunctionConfigurationRepository
{
    public FunctionConfigurationRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<FunctionConfigurationRepository> logger)
        : base(dbContextFactory, logger) { }

    protected override DbSet<FunctionConfiguration> GetDbSet(ConduitDbContext context)
        => context.FunctionConfigurations;

    protected override IQueryable<FunctionConfiguration> ApplyDefaultIncludes(IQueryable<FunctionConfiguration> query)
        => query.Include(f => f.CostMappings).ThenInclude(cm => cm.FunctionCost);

    protected override IQueryable<FunctionConfiguration> ApplyDefaultOrdering(IQueryable<FunctionConfiguration> query)
        => query.OrderBy(f => f.ConfigurationName);

    public async Task<List<FunctionConfiguration>> GetByIdsAsync(List<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
        {
            return new List<FunctionConfiguration>();
        }

        return await ExecuteAsync(async db =>
        {
            return await ApplyDefaultIncludes(GetDbSet(db).AsNoTracking())
                .Where(f => ids.Contains(f.Id))
                .ToListAsync(cancellationToken);
        }, cancellationToken, "GetByIds");
    }

    public async Task<FunctionConfiguration?> GetByNameAsync(string configurationName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configurationName))
        {
            throw new ArgumentException("Configuration name cannot be null or empty", nameof(configurationName));
        }

        return await ExecuteAsync(async db =>
        {
            return await ApplyDefaultIncludes(GetDbSet(db).AsNoTracking())
                .FirstOrDefaultAsync(f => f.ConfigurationName == configurationName, cancellationToken);
        }, cancellationToken, "GetByName");
    }

    public async Task<List<FunctionConfiguration>> GetAllEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db =>
        {
            return await ApplyDefaultIncludes(GetDbSet(db).AsNoTracking())
                .Where(f => f.IsEnabled)
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "GetAllEnabled");
    }

    public async Task<List<FunctionConfiguration>> GetByProviderTypeAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db =>
        {
            return await ApplyDefaultIncludes(GetDbSet(db).AsNoTracking())
                .Where(f => f.ProviderType == providerType)
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "GetByProviderType");
    }

    public async Task<List<FunctionConfiguration>> GetByPurposeAsync(FunctionPurpose purpose, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db =>
        {
            return await ApplyDefaultIncludes(GetDbSet(db).AsNoTracking())
                .Where(f => f.Purpose == purpose)
                .OrderBy(f => f.ConfigurationName)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "GetByPurpose");
    }

    /// <summary>
    /// Overrides base UpdateAsync to add concurrency retry logic.
    /// </summary>
    public override async Task<bool> UpdateAsync(FunctionConfiguration entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            return await base.UpdateAsync(entity, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogError(ex, "Concurrency error updating function configuration with ID {ConfigId}",
                LoggingSanitizer.S(entity.Id));

            // Retry with fresh context
            return await ExecuteAsync(async db =>
            {
                var existingEntity = await GetDbSet(db)
                    .FindAsync(new object[] { entity.Id }, cancellationToken);

                if (existingEntity != null)
                {
                    db.Entry(existingEntity).CurrentValues.SetValues(entity);
                    existingEntity.UpdatedAt = DateTime.UtcNow;
                    return await db.SaveChangesAsync(cancellationToken) > 0;
                }

                return false;
            }, cancellationToken, "UpdateAsync-Retry");
        }
    }

    public async Task<bool> NameExistsAsync(string configurationName, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configurationName))
        {
            throw new ArgumentException("Configuration name cannot be null or empty", nameof(configurationName));
        }

        return await ExecuteAsync(async db =>
        {
            var query = GetDbSet(db).AsNoTracking()
                .Where(f => f.ConfigurationName == configurationName);

            if (excludeId.HasValue)
            {
                query = query.Where(f => f.Id != excludeId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }, cancellationToken, "NameExists");
    }
}
