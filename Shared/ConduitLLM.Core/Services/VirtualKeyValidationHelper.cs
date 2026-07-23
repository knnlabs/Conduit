using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Helper class containing shared virtual key validation logic
    /// </summary>
    public static class VirtualKeyValidationHelper
    {
        /// <summary>
        /// Validates a virtual key with common checks
        /// </summary>
        /// <param name="virtualKey">The virtual key to validate</param>
        /// <param name="requestedModel">The requested model, if any</param>
        /// <param name="checkBalance">Whether to check the group balance</param>
        /// <param name="groupRepository">Repository for group operations (required if checkBalance is true)</param>
        /// <param name="logger">Logger for diagnostic output</param>
        /// <param name="batchSpendService">Optional service for pending spend and reservation checks</param>
        /// <returns>Validation result with status and error message if failed</returns>
        public static async Task<VirtualKeyValidationOutcome> ValidateVirtualKeyAsync(
            VirtualKey virtualKey,
            string? requestedModel,
            bool checkBalance,
            IVirtualKeyGroupRepository? groupRepository,
            ILogger logger,
            IBatchSpendUpdateService? batchSpendService = null)
        {
            // Check if key is enabled
            if (!virtualKey.IsEnabled)
            {
                logger.LogWarning("Virtual key is disabled: {KeyName} (ID: {KeyId})",
                    LoggingSanitizer.S(virtualKey.KeyName), virtualKey.Id);
                return VirtualKeyValidationOutcome.Failure(
                    VirtualKeyValidationFailureCodes.KeyDisabled,
                    401,
                    "Virtual key is disabled.",
                    virtualKey);
            }

            // Check expiration
            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                logger.LogWarning("Virtual key has expired: {KeyName} (ID: {KeyId}), expired at {ExpiryDate}",
                    LoggingSanitizer.S(virtualKey.KeyName), virtualKey.Id, virtualKey.ExpiresAt);
                return VirtualKeyValidationOutcome.Failure(
                    VirtualKeyValidationFailureCodes.KeyExpired,
                    401,
                    "Virtual key has expired.",
                    virtualKey);
            }

            // Check group balance if requested
            if (checkBalance && groupRepository != null)
            {
                var group = await groupRepository.GetByIdAsync(virtualKey.VirtualKeyGroupId);
                var pendingSpend = group != null && batchSpendService != null
                    ? await batchSpendService.GetPendingSpendAsync(virtualKey.Id)
                    : 0m;
                var availableBalance = group?.Balance - pendingSpend;
                if (group != null && availableBalance <= 0)
                {
                    logger.LogWarning("Virtual key group budget depleted: {KeyName} (ID: {KeyId}), group {GroupId} has database balance {Balance} and pending spend {PendingSpend}",
                        LoggingSanitizer.S(virtualKey.KeyName), virtualKey.Id, group.Id, group.Balance, pendingSpend);

                    return VirtualKeyValidationOutcome.Failure(
                        VirtualKeyValidationFailureCodes.InsufficientBalance,
                        402,
                        "Your account balance is insufficient to perform this operation.",
                        virtualKey);
                }
            }

            // Check if model is allowed
            if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
            {
                bool isModelAllowed = ConduitLLM.Configuration.Utilities.VirtualKeyUtilities.IsModelAllowed(requestedModel, virtualKey.AllowedModels);
                if (!isModelAllowed)
                {
                    logger.LogWarning("Virtual key {KeyName} (ID: {KeyId}) attempted to access restricted model: {RequestedModel}",
                        LoggingSanitizer.S(virtualKey.KeyName), virtualKey.Id, LoggingSanitizer.S(requestedModel));
                    return VirtualKeyValidationOutcome.Failure(
                        VirtualKeyValidationFailureCodes.ModelNotAllowed,
                        403,
                        "The requested model is not allowed for this virtual key.",
                        virtualKey);
                }
            }

            // All validations passed
            var logLevel = checkBalance ? LogLevel.Information : LogLevel.Debug;
            if (logLevel == LogLevel.Information)
            {
                logger.LogInformation("Validated virtual key successfully: {KeyName} (ID: {KeyId})",
                    LoggingSanitizer.S(virtualKey.KeyName), virtualKey.Id);
            }
            else
            {
                logger.LogDebug("Virtual key authenticated successfully: {KeyName} (ID: {KeyId})",
                    LoggingSanitizer.S(virtualKey.KeyName), virtualKey.Id);
            }

            return VirtualKeyValidationOutcome.Success(virtualKey);
        }
    }
}
