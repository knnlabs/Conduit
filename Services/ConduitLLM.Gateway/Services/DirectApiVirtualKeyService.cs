using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Services;
using VirtualKeyUtilities = ConduitLLM.Configuration.Utilities.VirtualKeyUtilities;

using ConduitLLM.Configuration.Messaging;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Lightweight Gateway implementation of IVirtualKeyService that extends VirtualKeyServiceBase
    /// for shared CRUD and adds Gateway-specific validation and spend tracking.
    /// </summary>
    /// <remarks>
    /// All CRUD operations (Generate, Get, List, Update, Delete, ResetSpend) are inherited
    /// from <see cref="VirtualKeyServiceBase"/>. This class only implements the four
    /// Gateway-specific methods: authentication validation, full validation, spend updates,
    /// and raw entity retrieval for validation.
    /// </remarks>
    public class DirectApiVirtualKeyService : VirtualKeyServiceBase, IVirtualKeyService
    {
        private readonly IBatchSpendUpdateService? _batchSpendService;

        /// <summary>
        /// Initializes a new instance of the DirectApiVirtualKeyService
        /// </summary>
        public DirectApiVirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeyGroupRepository groupRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            IEventBus? eventBus,
            ILogger<DirectApiVirtualKeyService> logger,
            IBatchSpendUpdateService? batchSpendService = null)
            : base(virtualKeyRepository, groupRepository, spendHistoryRepository, eventBus, logger)
        {
            _batchSpendService = batchSpendService;
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> ValidateVirtualKeyForAuthenticationAsync(string key, string? requestedModel = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                Logger.LogWarning("Empty key provided for authentication validation");
                return null;
            }

            // Hash the incoming key before looking it up
            var keyHash = VirtualKeyUtilities.HashKey(key);
            Logger.LogDebug("Validating key for authentication: {KeyPrefix}..., Hash: {Hash}",
                LoggingSanitizer.S(key.Length > 10 ? key.Substring(0, 10) : key), keyHash);

            var virtualKey = await VirtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                Logger.LogWarning("No matching virtual key found for hash: {Hash}", keyHash);
                return null;
            }

            // Delegate to shared validation helper (no balance check for authentication)
            var result = await VirtualKeyValidationHelper.ValidateVirtualKeyAsync(
                virtualKey, requestedModel, checkBalance: false, GroupRepository, Logger);

            return result.IsValid ? virtualKey : null;
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                Logger.LogWarning("Empty key provided for validation");
                return null;
            }

            // Hash the incoming key before looking it up
            var keyHash = VirtualKeyUtilities.HashKey(key);
            Logger.LogDebug("Validating key: {KeyPrefix}..., Hash: {Hash}",
                LoggingSanitizer.S(key.Length > 10 ? key.Substring(0, 10) : key), keyHash);

            var virtualKey = await VirtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                Logger.LogWarning("No matching virtual key found for hash: {Hash}", keyHash);
                return null;
            }

            // Delegate to shared validation helper (with balance check)
            var result = await VirtualKeyValidationHelper.ValidateVirtualKeyAsync(
                virtualKey, requestedModel, checkBalance: true, GroupRepository, Logger, _batchSpendService);

            if (!result.IsValid)
            {
                Logger.LogWarning("Virtual key {KeyId} validation failed: {Reason}",
                    virtualKey.Id, result.Reason ?? "unknown");
                return null;
            }

            Logger.LogDebug("Virtual key {KeyId} validated successfully for model: {Model}",
                virtualKey.Id, LoggingSanitizer.S(requestedModel ?? "any"));
            return virtualKey;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            if (cost <= 0) return true; // No cost to add, consider it successful

            var virtualKey = await VirtualKeyRepository.GetByIdAsync(keyId);
            if (virtualKey == null) return false;

            try
            {
                // Get the key's group
                var group = await GroupRepository.GetByIdAsync(virtualKey.VirtualKeyGroupId);
                if (group == null)
                {
                    Logger.LogError("Virtual key {KeyId} has invalid group ID {GroupId}", keyId, virtualKey.VirtualKeyGroupId);
                    return false;
                }

                // Update the group balance
                var newBalance = await GroupRepository.AdjustBalanceAsync(
                    group.Id,
                    -cost,
                    $"API usage by virtual key #{keyId}",
                    "System",
                    ReferenceType.VirtualKey,
                    keyId.ToString());

                // Update virtual key timestamp
                virtualKey.UpdatedAt = DateTime.UtcNow;
                bool success = await VirtualKeyRepository.UpdateAsync(virtualKey);

                if (success)
                {
                    Logger.LogInformation("Updated spend for key ID {KeyId} in group {GroupId}. New balance: {Balance}",
                        keyId, group.Id, newBalance);
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                "Error updating spend for key ID {KeyId}.",
                keyId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await VirtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
        }
    }
}
