using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Validates business rules for FunctionCredential operations
/// Mirrors the validation patterns used in ProviderKeyCredentialValidator
/// </summary>
public class FunctionCredentialValidator
{
    private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
    private const int MaxCredentialsPerConfiguration = 32;

    public FunctionCredentialValidator(IDbContextFactory<ConduitDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// Validates if a new credential can be added to a function configuration
    /// </summary>
    public async Task<CredentialValidationResult> ValidateAddCredentialAsync(int functionConfigurationId, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var currentCredentialCount = await dbContext.FunctionCredentials
            .CountAsync(c => c.FunctionConfigurationId == functionConfigurationId, cancellationToken);

        if (currentCredentialCount >= MaxCredentialsPerConfiguration)
        {
            return CredentialValidationResult.Failure($"Function configuration already has the maximum of {MaxCredentialsPerConfiguration} credentials");
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
    /// Ensures at least one credential is enabled for a function configuration
    /// </summary>
    public async Task<CredentialValidationResult> ValidateConfigurationHasEnabledCredentialAsync(int functionConfigurationId, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var hasEnabledCredential = await dbContext.FunctionCredentials
            .AnyAsync(c => c.FunctionConfigurationId == functionConfigurationId && c.IsEnabled, cancellationToken);

        if (!hasEnabledCredential)
        {
            return CredentialValidationResult.Failure("Function configuration must have at least one enabled credential");
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
