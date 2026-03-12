using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for global settings using Entity Framework Core.
/// Inherits common CRUD operations from RepositoryBase.
/// </summary>
public class GlobalSettingRepository : RepositoryBase<GlobalSetting, int>, IGlobalSettingRepository
{
    /// <summary>
    /// Creates a new instance of the repository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public GlobalSettingRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<GlobalSettingRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<GlobalSetting> GetDbSet(ConduitDbContext context) => context.GlobalSettings;

    /// <inheritdoc/>
    protected override IQueryable<GlobalSetting> ApplyDefaultOrdering(IQueryable<GlobalSetting> query)
    {
        return query.OrderBy(gs => gs.Key);
    }

    /// <inheritdoc/>
    public async Task<GlobalSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        return await ExecuteAsync(async context =>
        {
            return await GetDbSet(context)
                .AsNoTracking()
                .FirstOrDefaultAsync(gs => gs.Key == key, cancellationToken);
        }, cancellationToken, $"getting by key {LoggingSanitizer.S(key)}");
    }

    /// <inheritdoc/>
    public async Task<bool> UpsertAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        ArgumentNullException.ThrowIfNull(value);

        return await ExecuteAsync(async context =>
        {
            var dbSet = GetDbSet(context);

            // Try to find existing setting
            var existingSetting = await dbSet
                .FirstOrDefaultAsync(gs => gs.Key == key, cancellationToken);

            if (existingSetting == null)
            {
                // Create new setting
                var newSetting = new GlobalSetting
                {
                    Key = key,
                    Value = value,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dbSet.Add(newSetting);
            }
            else
            {
                // Update existing setting
                existingSetting.Value = value;
                existingSetting.UpdatedAt = DateTime.UtcNow;

                // Only update description if provided
                if (description != null)
                {
                    existingSetting.Description = description;
                }
            }

            int rowsAffected = await context.SaveChangesAsync(cancellationToken);
            return rowsAffected > 0;
        }, cancellationToken, $"upserting by key {LoggingSanitizer.S(key)}");
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        return await ExecuteAsync(async context =>
        {
            var dbSet = GetDbSet(context);
            var globalSetting = await dbSet
                .FirstOrDefaultAsync(gs => gs.Key == key, cancellationToken);

            if (globalSetting == null)
            {
                return false;
            }

            dbSet.Remove(globalSetting);
            int rowsAffected = await context.SaveChangesAsync(cancellationToken);
            return rowsAffected > 0;
        }, cancellationToken, $"deleting by key {LoggingSanitizer.S(key)}");
    }
}
