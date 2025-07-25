using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Validates business rules for ProviderKeyCredential operations
    /// </summary>
    public class ProviderKeyCredentialValidator
    {
        private readonly IConfigurationDbContext _context;
        private const int MaxKeysPerProvider = 32;

        public ProviderKeyCredentialValidator(IConfigurationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Validates if a new key can be added to a provider
        /// </summary>
        public async Task<KeyValidationResult> ValidateAddKeyAsync(int providerCredentialId)
        {
            var currentKeyCount = await _context.ProviderKeyCredentials
                .CountAsync(k => k.ProviderCredentialId == providerCredentialId);

            if (currentKeyCount >= MaxKeysPerProvider)
            {
                return KeyValidationResult.Failure($"Provider already has the maximum of {MaxKeysPerProvider} keys");
            }

            return KeyValidationResult.Success();
        }

        /// <summary>
        /// Validates if a key can be set as primary
        /// </summary>
        public async Task<KeyValidationResult> ValidateSetPrimaryAsync(int keyId)
        {
            var key = await _context.ProviderKeyCredentials
                .FirstOrDefaultAsync(k => k.Id == keyId);

            if (key == null)
            {
                return KeyValidationResult.Failure("Key not found");
            }

            if (!key.IsEnabled)
            {
                return KeyValidationResult.Failure("Cannot set a disabled key as primary");
            }

            return KeyValidationResult.Success();
        }

        /// <summary>
        /// Validates if a key can be disabled
        /// </summary>
        public async Task<KeyValidationResult> ValidateDisableKeyAsync(int keyId)
        {
            var key = await _context.ProviderKeyCredentials
                .FirstOrDefaultAsync(k => k.Id == keyId);

            if (key == null)
            {
                return KeyValidationResult.Failure("Key not found");
            }

            if (key.IsPrimary)
            {
                return KeyValidationResult.Failure("Cannot disable a primary key. Set another key as primary first.");
            }

            return KeyValidationResult.Success();
        }

        /// <summary>
        /// Ensures at least one key is enabled for a provider
        /// </summary>
        public async Task<KeyValidationResult> ValidateProviderHasEnabledKeyAsync(int providerCredentialId)
        {
            var hasEnabledKey = await _context.ProviderKeyCredentials
                .AnyAsync(k => k.ProviderCredentialId == providerCredentialId && k.IsEnabled);

            if (!hasEnabledKey)
            {
                return KeyValidationResult.Failure("Provider must have at least one enabled key");
            }

            return KeyValidationResult.Success();
        }
    }

    public class KeyValidationResult
    {
        public bool IsValid { get; private set; }
        public string? ErrorMessage { get; private set; }

        private KeyValidationResult(bool isValid, string? errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public static KeyValidationResult Success() => new KeyValidationResult(true);
        public static KeyValidationResult Failure(string errorMessage) => new KeyValidationResult(false, errorMessage);
    }
}