using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Validates business rules for ProviderKeyCredential operations
    /// </summary>
    public class ProviderKeyCredentialValidator
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private const int MaxKeysPerProvider = 32;

        public ProviderKeyCredentialValidator(IDbContextFactory<ConduitDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        /// <summary>
        /// Validates if a new key can be added to a provider
        /// </summary>
        public async Task<ValidationResult> ValidateAddKeyAsync(int ProviderId, CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var currentKeyCount = await context.ProviderKeyCredentials
                .CountAsync(k => k.ProviderId == ProviderId, cancellationToken);

            if (currentKeyCount >= MaxKeysPerProvider)
            {
                return ValidationResult.Failure($"Provider already has the maximum of {MaxKeysPerProvider} keys");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates if a key can be set as primary
        /// </summary>
        public async Task<ValidationResult> ValidateSetPrimaryAsync(int keyId, CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var key = await context.ProviderKeyCredentials
                .FirstOrDefaultAsync(k => k.Id == keyId, cancellationToken);

            if (key == null)
            {
                return ValidationResult.Failure("Key not found");
            }

            if (!key.IsEnabled)
            {
                return ValidationResult.Failure("Cannot set a disabled key as primary");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates if a key can be disabled
        /// </summary>
        public async Task<ValidationResult> ValidateDisableKeyAsync(int keyId, CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var key = await context.ProviderKeyCredentials
                .FirstOrDefaultAsync(k => k.Id == keyId, cancellationToken);

            if (key == null)
            {
                return ValidationResult.Failure("Key not found");
            }

            if (key.IsPrimary)
            {
                return ValidationResult.Failure("Cannot disable a primary key. Set another key as primary first.");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Ensures at least one key is enabled for a provider
        /// </summary>
        public async Task<ValidationResult> ValidateProviderHasEnabledKeyAsync(int ProviderId, CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var hasEnabledKey = await context.ProviderKeyCredentials
                .AnyAsync(k => k.ProviderId == ProviderId && k.IsEnabled, cancellationToken);

            if (!hasEnabledKey)
            {
                return ValidationResult.Failure("Provider must have at least one enabled key");
            }

            return ValidationResult.Success();
        }
    }
}
