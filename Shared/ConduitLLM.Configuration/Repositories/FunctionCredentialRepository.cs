using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for function credentials using Entity Framework Core.
/// </summary>
public class FunctionCredentialRepository : IFunctionCredentialRepository
{
    private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
    private readonly ILogger<FunctionCredentialRepository> _logger;

    public FunctionCredentialRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<FunctionCredentialRepository> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Obsolete("Use GetAllUnboundedAsync() for cache warming/exports, or GetPaginatedAsync() for bounded queries.")]
    public async Task<List<FunctionCredential>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Delegate to GetAllUnboundedAsync to avoid code duplication
        return await GetAllUnboundedAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<FunctionCredential>> GetAllUnboundedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Unbounded query executed on FunctionCredential via GetAllUnboundedAsync(). " +
            "Ensure this is intentional (cache warming, export, migration).");

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCredentials
                .AsNoTracking()
                .OrderBy(c => c.ProviderType)
                .ThenByDescending(c => c.IsPrimary)
                .ThenBy(c => c.KeyName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all function credentials (unbounded)");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<(List<FunctionCredential> Items, int TotalCount)> GetPaginatedAsync(
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
            var query = dbContext.FunctionCredentials.AsNoTracking();

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(c => c.ProviderType)
                .ThenByDescending(c => c.IsPrimary)
                .ThenBy(c => c.KeyName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paginated function credentials (page {Page}, size {PageSize})", page, pageSize);
            throw;
        }
    }

    public async Task<FunctionCredential?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function credential with ID {CredentialId}", LoggingSanitizer.S(id));
            throw;
        }
    }

    public async Task<List<FunctionCredential>> GetByProviderTypeAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCredentials
                .AsNoTracking()
                .Where(c => c.ProviderType == providerType)
                .OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.KeyName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting credentials for provider type {ProviderType}",
                LoggingSanitizer.S(providerType));
            throw;
        }
    }

    public async Task<List<FunctionCredential>> GetEnabledByProviderTypeAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCredentials
                .AsNoTracking()
                .Where(c => c.ProviderType == providerType && c.IsEnabled)
                .OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.KeyName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting enabled credentials for provider type {ProviderType}",
                LoggingSanitizer.S(providerType));
            throw;
        }
    }

    public async Task<FunctionCredential?> GetPrimaryCredentialAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCredentials
                .AsNoTracking()
                .Where(c => c.ProviderType == providerType && c.IsPrimary && c.IsEnabled)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting primary credential for provider type {ProviderType}",
                LoggingSanitizer.S(providerType));
            throw;
        }
    }

    public async Task<List<FunctionCredential>> GetByCredentialGroupAsync(FunctionProviderType providerType, short functionAccountGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.FunctionCredentials
                .AsNoTracking()
                .Where(c => c.ProviderType == providerType && c.FunctionAccountGroup == functionAccountGroup)
                .OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.KeyName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting credentials for group {Group} in provider type {ProviderType}",
                LoggingSanitizer.S(functionAccountGroup), LoggingSanitizer.S(providerType));
            throw;
        }
    }

    public async Task<int> CreateAsync(FunctionCredential credential, CancellationToken cancellationToken = default)
    {
        if (credential == null)
        {
            throw new ArgumentNullException(nameof(credential));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                credential.CreatedAt = DateTime.UtcNow;
                credential.UpdatedAt = DateTime.UtcNow;

                // Auto-primary logic: If this is the first enabled credential, automatically set it as primary
                // This mirrors the ProviderKeyCredentialRepository pattern
                if (credential.IsEnabled && !credential.IsPrimary)
                {
                    var enabledCredentialsCount = await dbContext.FunctionCredentials
                        .CountAsync(c => c.ProviderType == credential.ProviderType && c.IsEnabled, cancellationToken);

                    // If this will be the only enabled credential, set it as primary
                    if (enabledCredentialsCount == 0)
                    {
                        credential.IsPrimary = true;
                        _logger.LogInformation("Automatically setting credential as primary since it's the only enabled credential for provider type {ProviderType}",
                            LoggingSanitizer.S(credential.ProviderType));
                    }
                }

                // If this credential is being set as primary, unset any existing primary
                if (credential.IsPrimary)
                {
                    var existingPrimary = await dbContext.FunctionCredentials
                        .Where(c => c.ProviderType == credential.ProviderType && c.IsPrimary)
                        .ToListAsync(cancellationToken);

                    foreach (var existing in existingPrimary)
                    {
                        existing.IsPrimary = false;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                }

                dbContext.FunctionCredentials.Add(credential);
                await dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return credential.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while creating function credential '{KeyName}'",
                    LoggingSanitizer.S(credential.KeyName));
                throw;
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating function credential '{KeyName}'",
                LoggingSanitizer.S(credential.KeyName));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating function credential '{KeyName}'",
                LoggingSanitizer.S(credential.KeyName));
            throw;
        }
    }

    public async Task UpdateAsync(FunctionCredential credential, CancellationToken cancellationToken = default)
    {
        if (credential == null)
        {
            throw new ArgumentNullException(nameof(credential));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var existingCredential = await dbContext.FunctionCredentials
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

                // Auto-primary logic: If being enabled and this will be the only enabled credential, set it as primary
                // This mirrors the ProviderKeyCredentialRepository pattern
                if (!wasEnabled && willBeEnabled && !existingCredential.IsPrimary)
                {
                    var enabledCredentialsCount = await dbContext.FunctionCredentials
                        .CountAsync(c => c.ProviderType == existingCredential.ProviderType
                            && c.IsEnabled
                            && c.Id != existingCredential.Id, cancellationToken);

                    // If this will be the only enabled credential, set it as primary
                    if (enabledCredentialsCount == 0)
                    {
                        existingCredential.IsPrimary = true;
                        _logger.LogInformation("Automatically setting credential {CredentialId} as primary since it's the only enabled credential for provider type {ProviderType}",
                            LoggingSanitizer.S(existingCredential.Id), LoggingSanitizer.S(existingCredential.ProviderType));
                    }
                }

                // If this credential is being set as primary, unset any existing primary
                if (existingCredential.IsPrimary)
                {
                    var existingPrimary = await dbContext.FunctionCredentials
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

                // No need to call Update() since we're modifying a tracked entity
                await dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while updating function credential with ID {CredentialId}",
                    LoggingSanitizer.S(credential.Id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating function credential with ID {CredentialId}",
                LoggingSanitizer.S(credential.Id));
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
                var credential = await dbContext.FunctionCredentials
                    .FindAsync(new object[] { id }, cancellationToken);

                if (credential != null)
                {
                    dbContext.FunctionCredentials.Remove(credential);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while deleting function credential with ID {CredentialId}",
                    LoggingSanitizer.S(id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function credential with ID {CredentialId}",
                LoggingSanitizer.S(id));
            throw;
        }
    }

    public async Task SetAsPrimaryAsync(int credentialId, FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Unset all existing primary credentials for this provider type
                var existingPrimary = await dbContext.FunctionCredentials
                    .Where(c => c.ProviderType == providerType && c.IsPrimary)
                    .ToListAsync(cancellationToken);

                foreach (var existing in existingPrimary)
                {
                    existing.IsPrimary = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                }

                // Set the specified credential as primary
                var credential = await dbContext.FunctionCredentials
                    .FirstOrDefaultAsync(c => c.Id == credentialId && c.ProviderType == providerType,
                        cancellationToken);

                if (credential == null)
                {
                    throw new InvalidOperationException($"Credential {credentialId} not found for provider type {providerType}");
                }

                credential.IsPrimary = true;
                credential.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Transaction rolled back while setting credential {CredentialId} as primary",
                    LoggingSanitizer.S(credentialId));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting credential {CredentialId} as primary",
                LoggingSanitizer.S(credentialId));
            throw;
        }
    }
}
