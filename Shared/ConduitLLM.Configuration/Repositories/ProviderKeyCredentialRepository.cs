using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for ProviderKeyCredential operations.
/// Extends RepositoryBase for standard CRUD operations and implements domain-specific methods.
/// </summary>
public class ProviderKeyCredentialRepository : RepositoryBase<ProviderKeyCredential, int>, IProviderKeyCredentialRepository
{
    /// <summary>
    /// Creates a new instance of the repository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public ProviderKeyCredentialRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<ProviderKeyCredentialRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<ProviderKeyCredential> GetDbSet(ConduitDbContext context)
        => context.ProviderKeyCredentials;

    /// <inheritdoc/>
    protected override IQueryable<ProviderKeyCredential> ApplyDefaultIncludes(IQueryable<ProviderKeyCredential> query)
    {
        return query.Include(c => c.Provider);
    }

    /// <inheritdoc/>
    protected override IQueryable<ProviderKeyCredential> ApplyDefaultOrdering(IQueryable<ProviderKeyCredential> query)
    {
        return query
            .OrderBy(k => k.ProviderId)
            .ThenByDescending(k => k.IsPrimary)
            .ThenBy(k => k.ProviderAccountGroup);
    }

    /// <inheritdoc/>
    public async Task<(List<ProviderKeyCredential> Items, int TotalCount)> GetByProviderIdPaginatedAsync(
        int providerId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await GetFilteredPaginatedAsync(
            k => k.ProviderId == providerId,
            pageNumber,
            pageSize,
            q => q.OrderByDescending(k => k.IsPrimary).ThenBy(k => k.ProviderAccountGroup),
            cancellationToken,
            $"getting paginated credentials for provider {providerId}");
    }

    /// <inheritdoc/>
    public async Task<ProviderKeyCredential?> GetPrimaryKeyAsync(int providerId)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => k.ProviderId == providerId
                        && k.IsPrimary
                        && k.IsEnabled));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting primary key for provider {ProviderId}", providerId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<ProviderKeyCredential>> GetEnabledKeysByProviderIdAsync(int providerId)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AsNoTracking()
                    .Where(k => k.ProviderId == providerId && k.IsEnabled)
                    .OrderByDescending(k => k.IsPrimary)
                    .ThenBy(k => k.ProviderAccountGroup)
                    .ToListAsync());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting enabled keys for provider {ProviderId}", providerId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new key credential with automatic primary key assignment.
    /// If this is the only enabled key for the provider, it will be automatically set as primary.
    /// </summary>
    public override async Task<int> CreateAsync(ProviderKeyCredential entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return await ExecuteWriteAsync(async context =>
        {
            OnBeforeCreate(entity);

            if (entity.IsEnabled && entity.IsPrimary)
            {
                // Demote existing primary key so the new one can take over
                var existingPrimary = await GetDbSet(context)
                    .FirstOrDefaultAsync(k => k.ProviderId == entity.ProviderId && k.IsPrimary, cancellationToken);

                if (existingPrimary != null)
                {
                    existingPrimary.IsPrimary = false;
                    existingPrimary.UpdatedAt = DateTime.UtcNow;
                    Logger.LogInformation("Demoted existing primary key {KeyId} for provider {ProviderId}",
                        existingPrimary.Id, entity.ProviderId);
                }
            }
            else if (entity.IsEnabled && !entity.IsPrimary)
            {
                // Check if this should be automatically set as primary
                var enabledKeysCount = await GetDbSet(context)
                    .CountAsync(k => k.ProviderId == entity.ProviderId && k.IsEnabled, cancellationToken);

                // If this will be the only enabled key, set it as primary
                if (enabledKeysCount == 0)
                {
                    entity.IsPrimary = true;
                    Logger.LogInformation("Automatically setting key as primary since it's the only enabled key for provider {ProviderId}",
                        entity.ProviderId);
                }
            }

            GetDbSet(context).Add(entity);
            await context.SaveChangesAsync(cancellationToken);

            Logger.LogInformation("Created key credential {KeyId} for provider {ProviderId} (IsPrimary: {IsPrimary})",
                entity.Id, entity.ProviderId, entity.IsPrimary);

            return entity.Id;
        }, $"creating for provider {entity.ProviderId}", cancellationToken);
    }

    /// <summary>
    /// Updates an existing key credential with automatic primary key assignment.
    /// If this becomes the only enabled key when being enabled, it will be automatically set as primary.
    /// </summary>
    public override async Task<bool> UpdateAsync(ProviderKeyCredential entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return await ExecuteWriteAsync(async context =>
        {
            var existingKey = await GetDbSet(context)
                .FirstOrDefaultAsync(k => k.Id == entity.Id, cancellationToken);

            if (existingKey == null)
                return false;

            bool wasEnabled = existingKey.IsEnabled;
            bool willBeEnabled = entity.IsEnabled;

            // Update properties
            existingKey.ProviderAccountGroup = entity.ProviderAccountGroup;
            existingKey.ApiKey = entity.ApiKey;
            existingKey.BaseUrl = entity.BaseUrl;
            existingKey.IsPrimary = entity.IsPrimary;
            existingKey.IsEnabled = entity.IsEnabled;
            existingKey.UpdatedAt = DateTime.UtcNow;

            if (entity.IsPrimary && entity.IsEnabled)
            {
                // Demote existing primary key so this one can become primary
                var otherPrimary = await GetDbSet(context)
                    .FirstOrDefaultAsync(k => k.ProviderId == existingKey.ProviderId && k.IsPrimary && k.Id != existingKey.Id, cancellationToken);

                if (otherPrimary != null)
                {
                    otherPrimary.IsPrimary = false;
                    otherPrimary.UpdatedAt = DateTime.UtcNow;
                    Logger.LogInformation("Demoted existing primary key {KeyId} for provider {ProviderId}",
                        otherPrimary.Id, existingKey.ProviderId);
                }
            }
            else if (!wasEnabled && willBeEnabled && !entity.IsPrimary)
            {
                // Check if this should be automatically set as primary when being enabled
                var enabledKeysCount = await GetDbSet(context)
                    .CountAsync(k => k.ProviderId == existingKey.ProviderId && k.IsEnabled && k.Id != existingKey.Id, cancellationToken);

                // If this will be the only enabled key, set it as primary
                if (enabledKeysCount == 0)
                {
                    existingKey.IsPrimary = true;
                    Logger.LogInformation("Automatically setting key {KeyId} as primary since it's the only enabled key for provider {ProviderId}",
                        existingKey.Id, existingKey.ProviderId);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            Logger.LogInformation("Updated key credential {KeyId} for provider {ProviderId} (IsPrimary: {IsPrimary})",
                entity.Id, entity.ProviderId, existingKey.IsPrimary);

            return true;
        }, $"updating ID {entity.Id}", cancellationToken);
    }

    /// <summary>
    /// Deletes a key credential by ID.
    /// </summary>
    public override async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                var keyCredential = await GetDbSet(context)
                    .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

                if (keyCredential == null)
                    return false;

                GetDbSet(context).Remove(keyCredential);
                await context.SaveChangesAsync(cancellationToken);

                Logger.LogInformation("Deleted key credential {KeyId} for provider {ProviderId}",
                    id, keyCredential.ProviderId);

                return true;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting key credential {KeyId}", id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SetPrimaryKeyAsync(int providerId, int keyId)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                // Runs through the execution strategy (EnableRetryOnFailure): the whole
                // delegate re-runs on transient failure, so it re-reads before writing.
                return await context.ExecuteInTransactionAsync(async _ =>
                {
                    // Validate the target before changing the current primary. A missing
                    // or wrong-provider key is a no-op and must not clear a valid primary.
                    var newPrimaryKey = await GetDbSet(context)
                        .FirstOrDefaultAsync(k => k.Id == keyId && k.ProviderId == providerId);

                    if (newPrimaryKey == null)
                        return false;

                    // First, unset any existing primary keys
                    var existingPrimaryKeys = await GetDbSet(context)
                        .Where(k => k.ProviderId == providerId && k.IsPrimary)
                        .ToListAsync();

                    foreach (var key in existingPrimaryKeys)
                    {
                        key.IsPrimary = false;
                        key.UpdatedAt = DateTime.UtcNow;
                    }

                    // Save changes to unset primary keys first to avoid constraint violation
                    if (existingPrimaryKeys.Count > 0)
                    {
                        await context.SaveChangesAsync();
                    }

                    newPrimaryKey.IsPrimary = true;
                    newPrimaryKey.UpdatedAt = DateTime.UtcNow;

                    await context.SaveChangesAsync();

                    Logger.LogInformation("Set key {KeyId} as primary for provider {ProviderId}",
                        keyId, providerId);

                    return true;
                });
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to set primary key {KeyId} for provider {ProviderId}",
                keyId, providerId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasKeyCredentialsAsync(int providerId)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AnyAsync(k => k.ProviderId == providerId));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking if provider {ProviderId} has key credentials", providerId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> CountByProviderIdAsync(int providerId)
    {
        try
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .CountAsync(k => k.ProviderId == providerId));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error counting key credentials for provider {ProviderId}", providerId);
            throw;
        }
    }
}
