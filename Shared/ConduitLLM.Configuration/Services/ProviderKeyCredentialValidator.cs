using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Validates business rules for ProviderKeyCredential operations
    /// </summary>
    public class ProviderKeyCredentialValidator : CredentialValidatorBase<ProviderKeyCredential>
    {
        protected override int MaxPerGroup => 32;
        protected override string EntityName => "key";
        protected override string GroupName => "Provider";
        protected override DbSet<ProviderKeyCredential> GetDbSet(ConduitDbContext context) => context.ProviderKeyCredentials;

        public ProviderKeyCredentialValidator(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ProviderKeyCredentialValidator> logger)
            : base(dbContextFactory, logger) { }

        /// <summary>
        /// Validates if a new key can be added to a provider
        /// </summary>
        public Task<ConduitValidationResult> ValidateAddKeyAsync(int providerId, CancellationToken cancellationToken = default)
            => ValidateAddAsync(k => k.ProviderId == providerId, cancellationToken);

        /// <summary>
        /// Validates if a key can be disabled
        /// </summary>
        public Task<ConduitValidationResult> ValidateDisableKeyAsync(int keyId, CancellationToken cancellationToken = default)
            => ValidateDisableAsync(keyId, cancellationToken);

        /// <summary>
        /// Ensures at least one key is enabled for a provider
        /// </summary>
        public Task<ConduitValidationResult> ValidateProviderHasEnabledKeyAsync(int providerId, CancellationToken cancellationToken = default)
            => ValidateHasEnabledAsync(k => k.ProviderId == providerId && k.IsEnabled, cancellationToken);
    }
}
