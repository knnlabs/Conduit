using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
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
        /// <returns>Validation result with status and error message if failed</returns>
        public static async Task<ValidationResult> ValidateVirtualKeyAsync(
            VirtualKey virtualKey,
            string? requestedModel,
            bool checkBalance,
            IVirtualKeyGroupRepository? groupRepository,
            ILogger logger)
        {
            // Check if key is enabled
            if (!virtualKey.IsEnabled)
            {
                logger.LogWarning("Virtual key is disabled: {KeyName} (ID: {KeyId})", 
                    virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id);
                return new ValidationResult { IsValid = false, Reason = "Key is disabled" };
            }

            // Check expiration
            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                logger.LogWarning("Virtual key has expired: {KeyName} (ID: {KeyId}), expired at {ExpiryDate}",
                    virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id, virtualKey.ExpiresAt);
                return new ValidationResult { IsValid = false, Reason = "Key has expired" };
            }

            // Check group balance if requested
            if (checkBalance && groupRepository != null)
            {
                var group = await groupRepository.GetByIdAsync(virtualKey.VirtualKeyGroupId);
                if (group != null && group.Balance <= 0)
                {
                    logger.LogWarning("Virtual key group budget depleted: {KeyName} (ID: {KeyId}), group {GroupId} has balance {Balance}",
                        virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id, group.Id, group.Balance);
                    
                    return new ValidationResult 
                    { 
                        IsValid = false, 
                        Reason = "Insufficient balance",
                        StatusCode = 402 // Payment Required
                    };
                }
            }

            // Check if model is allowed
            if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
            {
                bool isModelAllowed = VirtualKeyUtilities.IsModelAllowed(requestedModel, virtualKey.AllowedModels);
                if (!isModelAllowed)
                {
                    logger.LogWarning("Virtual key {KeyName} (ID: {KeyId}) attempted to access restricted model: {RequestedModel}",
                        virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id, requestedModel.Replace(Environment.NewLine, ""));
                    return new ValidationResult { IsValid = false, Reason = "Model not allowed" };
                }
            }

            // All validations passed
            var logLevel = checkBalance ? LogLevel.Information : LogLevel.Debug;
            if (logLevel == LogLevel.Information)
            {
                logger.LogInformation("Validated virtual key successfully: {KeyName} (ID: {KeyId})",
                    virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id);
            }
            else
            {
                logger.LogDebug("Virtual key authenticated successfully: {KeyName} (ID: {KeyId})",
                    virtualKey.KeyName.Replace(Environment.NewLine, ""), virtualKey.Id);
            }

            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// Result of virtual key validation
        /// </summary>
        public class ValidationResult
        {
            /// <summary>
            /// Whether the validation passed
            /// </summary>
            public bool IsValid { get; set; }
            
            /// <summary>
            /// Reason for validation failure
            /// </summary>
            public string? Reason { get; set; }
            
            /// <summary>
            /// Optional status code to return (e.g., 402 for insufficient balance)
            /// </summary>
            public int? StatusCode { get; set; }
        }
    }
}