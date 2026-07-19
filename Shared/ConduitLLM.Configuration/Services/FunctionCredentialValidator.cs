using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Validates business rules for FunctionCredential operations
/// </summary>
public class FunctionCredentialValidator : CredentialValidatorBase<FunctionCredential>
{
    protected override int MaxPerGroup => 32;
    protected override string EntityName => "credential";
    protected override string GroupName => "Provider type";
    protected override DbSet<FunctionCredential> GetDbSet(ConduitDbContext context) => context.FunctionCredentials;

    public FunctionCredentialValidator(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<FunctionCredentialValidator> logger)
        : base(dbContextFactory, logger) { }

    /// <summary>
    /// Validates if a new credential can be added to a provider type
    /// </summary>
    public Task<ValidationResult> ValidateAddCredentialAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
        => ValidateAddAsync(c => c.ProviderType == providerType, cancellationToken);

    /// <summary>
    /// Validates if a credential can be disabled
    /// </summary>
    public Task<ValidationResult> ValidateDisableCredentialAsync(int credentialId, CancellationToken cancellationToken = default)
        => ValidateDisableAsync(credentialId, cancellationToken);

    /// <summary>
    /// Ensures at least one credential is enabled for a provider type
    /// </summary>
    public Task<ValidationResult> ValidateProviderTypeHasEnabledCredentialAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default)
        => ValidateHasEnabledAsync(c => c.ProviderType == providerType && c.IsEnabled, cancellationToken);
}
