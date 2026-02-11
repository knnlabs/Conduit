using ConduitLLM.Functions.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Validates business rules for FunctionCredential operations.
/// Mirrors the validation patterns used in ProviderKeyCredentialValidator.
/// </summary>
public class FunctionCredentialValidator
{
    private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
    private const int MaxCredentialsPerProviderType = 32;

    public FunctionCredentialValidator(IDbContextFactory<ConduitDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// Validates if a new credential can be added to a provider type
    /// </summary>
    public async Task<ValidationResult> ValidateAddCredentialAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var currentCredentialCount = await dbContext.FunctionCredentials
            .CountAsync(c => c.ProviderType == providerType, cancellationToken);

        if (currentCredentialCount >= MaxCredentialsPerProviderType)
        {
            return ValidationResult.Failure($"Provider type already has the maximum of {MaxCredentialsPerProviderType} credentials");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates if a credential can be set as primary
    /// </summary>
    public async Task<ValidationResult> ValidateSetPrimaryAsync(int credentialId, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var credential = await dbContext.FunctionCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId, cancellationToken);

        if (credential == null)
        {
            return ValidationResult.Failure("Credential not found");
        }

        if (!credential.IsEnabled)
        {
            return ValidationResult.Failure("Cannot set a disabled credential as primary");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates if a credential can be disabled
    /// </summary>
    public async Task<ValidationResult> ValidateDisableCredentialAsync(int credentialId, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var credential = await dbContext.FunctionCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId, cancellationToken);

        if (credential == null)
        {
            return ValidationResult.Failure("Credential not found");
        }

        if (credential.IsPrimary)
        {
            return ValidationResult.Failure("Cannot disable a primary credential. Set another credential as primary first.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Ensures at least one credential is enabled for a provider type
    /// </summary>
    public async Task<ValidationResult> ValidateProviderTypeHasEnabledCredentialAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var hasEnabledCredential = await dbContext.FunctionCredentials
            .AnyAsync(c => c.ProviderType == providerType && c.IsEnabled, cancellationToken);

        if (!hasEnabledCredential)
        {
            return ValidationResult.Failure("Provider type must have at least one enabled credential");
        }

        return ValidationResult.Success();
    }
}
