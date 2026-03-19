using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for function credentials using RepositoryBase.
/// Overrides Create/Update to implement auto-primary credential logic.
/// </summary>
public class FunctionCredentialRepository : RepositoryBase<FunctionCredential, int>, IFunctionCredentialRepository
{
    public FunctionCredentialRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<FunctionCredentialRepository> logger)
        : base(dbContextFactory, logger) { }

    protected override DbSet<FunctionCredential> GetDbSet(ConduitDbContext context)
        => context.FunctionCredentials;

    protected override IQueryable<FunctionCredential> ApplyDefaultOrdering(IQueryable<FunctionCredential> query)
        => query.OrderBy(c => c.ProviderType).ThenByDescending(c => c.IsPrimary).ThenBy(c => c.KeyName);

    public async Task<List<FunctionCredential>> GetByProviderTypeAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db =>
        {
            return await GetDbSet(db).AsNoTracking()
                .Where(c => c.ProviderType == providerType)
                .OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.KeyName)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "GetByProviderType");
    }

    public async Task<List<FunctionCredential>> GetEnabledByProviderTypeAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db =>
        {
            return await GetDbSet(db).AsNoTracking()
                .Where(c => c.ProviderType == providerType && c.IsEnabled)
                .OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.KeyName)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "GetEnabledByProviderType");
    }

    public async Task<FunctionCredential?> GetPrimaryCredentialAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db =>
        {
            return await GetDbSet(db).AsNoTracking()
                .Where(c => c.ProviderType == providerType && c.IsPrimary && c.IsEnabled)
                .FirstOrDefaultAsync(cancellationToken);
        }, cancellationToken, "GetPrimaryCredential");
    }

    public async Task<List<FunctionCredential>> GetByCredentialGroupAsync(FunctionProviderType providerType, short functionAccountGroup, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db =>
        {
            return await GetDbSet(db).AsNoTracking()
                .Where(c => c.ProviderType == providerType && c.FunctionAccountGroup == functionAccountGroup)
                .OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.KeyName)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "GetByCredentialGroup");
    }

    /// <summary>
    /// Overrides base CreateAsync to implement auto-primary credential logic.
    /// If this is the first enabled credential for a provider type, it's automatically set as primary.
    /// If this credential is primary, existing primary credentials are unset.
    /// </summary>
    public override async Task<int> CreateAsync(FunctionCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return await ExecuteAsync(async db =>
        {
            credential.CreatedAt = DateTime.UtcNow;
            credential.UpdatedAt = DateTime.UtcNow;

            // Auto-primary: If first enabled credential, set as primary
            if (credential.IsEnabled && !credential.IsPrimary)
            {
                var enabledCount = await GetDbSet(db)
                    .CountAsync(c => c.ProviderType == credential.ProviderType && c.IsEnabled, cancellationToken);

                if (enabledCount == 0)
                {
                    credential.IsPrimary = true;
                    Logger.LogInformation("Automatically setting credential as primary since it's the only enabled credential for provider type {ProviderType}",
                        LoggingSanitizer.S(credential.ProviderType));
                }
            }

            // If setting as primary, unset existing primary
            if (credential.IsPrimary)
            {
                var existingPrimary = await GetDbSet(db)
                    .Where(c => c.ProviderType == credential.ProviderType && c.IsPrimary)
                    .ToListAsync(cancellationToken);

                foreach (var existing in existingPrimary)
                {
                    existing.IsPrimary = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            GetDbSet(db).Add(credential);
            await db.SaveChangesAsync(cancellationToken);
            return credential.Id;
        }, cancellationToken, "CreateAsync");
    }

    /// <summary>
    /// Overrides base UpdateAsync to implement auto-primary credential logic.
    /// </summary>
    public override async Task<bool> UpdateAsync(FunctionCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return await ExecuteAsync(async db =>
        {
            var existingCredential = await GetDbSet(db)
                .FirstOrDefaultAsync(c => c.Id == credential.Id, cancellationToken);

            if (existingCredential == null)
            {
                throw new InvalidOperationException($"Credential {credential.Id} not found");
            }

            bool wasEnabled = existingCredential.IsEnabled;
            bool willBeEnabled = credential.IsEnabled;

            // Update the existing tracked entity with new values
            existingCredential.KeyName = credential.KeyName;
            existingCredential.ApiKey = credential.ApiKey;
            existingCredential.BaseUrl = credential.BaseUrl;
            existingCredential.Organization = credential.Organization;
            existingCredential.FunctionAccountGroup = credential.FunctionAccountGroup;
            existingCredential.IsPrimary = credential.IsPrimary;
            existingCredential.IsEnabled = credential.IsEnabled;
            existingCredential.UpdatedAt = DateTime.UtcNow;

            // Auto-primary: If being enabled and will be the only enabled credential
            if (!wasEnabled && willBeEnabled && !existingCredential.IsPrimary)
            {
                var enabledCount = await GetDbSet(db)
                    .CountAsync(c => c.ProviderType == existingCredential.ProviderType
                        && c.IsEnabled
                        && c.Id != existingCredential.Id, cancellationToken);

                if (enabledCount == 0)
                {
                    existingCredential.IsPrimary = true;
                    Logger.LogInformation("Automatically setting credential {CredentialId} as primary since it's the only enabled credential for provider type {ProviderType}",
                        LoggingSanitizer.S(existingCredential.Id), LoggingSanitizer.S(existingCredential.ProviderType));
                }
            }

            // If setting as primary, unset existing primary
            if (existingCredential.IsPrimary)
            {
                var existingPrimary = await GetDbSet(db)
                    .Where(c => c.ProviderType == existingCredential.ProviderType
                        && c.IsPrimary
                        && c.Id != existingCredential.Id)
                    .ToListAsync(cancellationToken);

                foreach (var existing in existingPrimary)
                {
                    existing.IsPrimary = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            return await db.SaveChangesAsync(cancellationToken) > 0;
        }, cancellationToken, "UpdateAsync");
    }

    public async Task SetAsPrimaryAsync(int credentialId, FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async db =>
        {
            // Unset all existing primary credentials for this provider type
            var existingPrimary = await GetDbSet(db)
                .Where(c => c.ProviderType == providerType && c.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingPrimary)
            {
                existing.IsPrimary = false;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            // Set the specified credential as primary
            var credential = await GetDbSet(db)
                .FirstOrDefaultAsync(c => c.Id == credentialId && c.ProviderType == providerType,
                    cancellationToken);

            if (credential == null)
            {
                throw new InvalidOperationException($"Credential {credentialId} not found for provider type {providerType}");
            }

            credential.IsPrimary = true;
            credential.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken, "SetAsPrimary");
    }
}
