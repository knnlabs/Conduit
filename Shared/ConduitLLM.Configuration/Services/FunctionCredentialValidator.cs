using ConduitLLM.Functions.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Validates business rules for FunctionCredential operations
/// Mirrors the validation patterns used in ProviderKeyCredentialValidator
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
    public async Task<CredentialValidationResult> ValidateAddCredentialAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var currentCredentialCount = await dbContext.FunctionCredentials
            .CountAsync(c => c.ProviderType == providerType, cancellationToken);

        if (currentCredentialCount >= MaxCredentialsPerProviderType)
        {
            return CredentialValidationResult.Failure($"Provider type already has the maximum of {MaxCredentialsPerProviderType} credentials");
        }

        return CredentialValidationResult.Success();
    }

    /// <summary>
    /// Validates if a credential can be set as primary
    /// </summary>
    public async Task<CredentialValidationResult> ValidateSetPrimaryAsync(int credentialId, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var credential = await dbContext.FunctionCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId, cancellationToken);

        if (credential == null)
        {
            return CredentialValidationResult.Failure("Credential not found");
        }

        if (!credential.IsEnabled)
        {
            return CredentialValidationResult.Failure("Cannot set a disabled credential as primary");
        }

        return CredentialValidationResult.Success();
    }

    /// <summary>
    /// Validates if a credential can be disabled
    /// </summary>
    public async Task<CredentialValidationResult> ValidateDisableCredentialAsync(int credentialId, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var credential = await dbContext.FunctionCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId, cancellationToken);

        if (credential == null)
        {
            return CredentialValidationResult.Failure("Credential not found");
        }

        if (credential.IsPrimary)
        {
            return CredentialValidationResult.Failure("Cannot disable a primary credential. Set another credential as primary first.");
        }

        return CredentialValidationResult.Success();
    }

    /// <summary>
    /// Ensures at least one credential is enabled for a provider type
    /// </summary>
    public async Task<CredentialValidationResult> ValidateProviderTypeHasEnabledCredentialAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var hasEnabledCredential = await dbContext.FunctionCredentials
            .AnyAsync(c => c.ProviderType == providerType && c.IsEnabled, cancellationToken);

        if (!hasEnabledCredential)
        {
            return CredentialValidationResult.Failure("Provider type must have at least one enabled credential");
        }

        return CredentialValidationResult.Success();
    }
}

public class CredentialValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }

    private CredentialValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static CredentialValidationResult Success() => new CredentialValidationResult(true);
    public static CredentialValidationResult Failure(string errorMessage) => new CredentialValidationResult(false, errorMessage);
}
