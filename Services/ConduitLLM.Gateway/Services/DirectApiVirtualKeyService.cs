using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Models;
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
        public Task<VirtualKeyValidationOutcome> ValidateVirtualKeyForAuthenticationAsync(
            string key,
            string? requestedModel = null)
        {
            return ValidateInternalAsync(key, requestedModel, checkBalance: false);
        }

        /// <inheritdoc />
        public Task<VirtualKeyValidationOutcome> ValidateVirtualKeyAsync(
            string key,
            string? requestedModel = null)
        {
            return ValidateInternalAsync(key, requestedModel, checkBalance: true);
        }

        private async Task<VirtualKeyValidationOutcome> ValidateInternalAsync(
            string key,
            string? requestedModel,
            bool checkBalance)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Logger.LogWarning("Empty key provided for virtual key validation");
                return VirtualKeyValidationOutcome.Failure(
                    VirtualKeyValidationFailureCodes.MissingKey,
                    401,
                    "Virtual key is required.");
            }

            try
            {
                var keyHash = VirtualKeyUtilities.HashKey(key);
                Logger.LogDebug("Validating key ({ValidationMode}): {KeyPrefix}, Hash: {Hash}",
                    checkBalance ? "balance" : "authentication",
                    LoggingSanitizer.S(ConduitLLM.Core.Utilities.SpanHelper.MaskSecret(key)),
                    keyHash);

                var virtualKey = await VirtualKeyRepository.GetByKeyHashAsync(keyHash);
                if (virtualKey == null)
                {
                    Logger.LogWarning("No matching virtual key found for hash: {Hash}", keyHash);
                    return VirtualKeyValidationOutcome.Failure(
                        VirtualKeyValidationFailureCodes.KeyNotFound,
                        401,
                        "Virtual key was not found.");
                }

                var result = await VirtualKeyValidationHelper.ValidateVirtualKeyAsync(
                    virtualKey,
                    requestedModel,
                    checkBalance,
                    checkBalance ? GroupRepository : null,
                    Logger,
                    checkBalance ? _batchSpendService : null);

                if (!result.IsValid)
                {
                    Logger.LogWarning("Virtual key {KeyId} validation failed: {Reason}",
                        virtualKey.Id, result.Reason ?? "unknown");
                }
                else
                {
                    Logger.LogDebug("Virtual key {KeyId} validated successfully for model: {Model}",
                        virtualKey.Id, LoggingSanitizer.S(requestedModel ?? "any"));
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error validating virtual key");
                return VirtualKeyValidationOutcome.Failure(
                    VirtualKeyValidationFailureCodes.ValidationError,
                    500,
                    "Virtual key validation failed.");
            }
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
                var billingTimestamp = DateTime.UtcNow;
                var newBalance = await GroupRepository.AdjustBalanceAsync(
                    group.Id,
                    -cost,
                    $"API usage by virtual key #{keyId}",
                    "System",
                    ReferenceType.VirtualKey,
                    keyId.ToString(),
                    billingTimestamp.Date.AddHours(billingTimestamp.Hour));

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
