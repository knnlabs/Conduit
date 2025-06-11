using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// API-specific implementation of IVirtualKeyService that directly uses repositories
    /// </summary>
    /// <remarks>
    /// This provides a lightweight implementation of IVirtualKeyService for the API project,
    /// without requiring dependencies on the WebUI project.
    /// </remarks>
    public class ApiVirtualKeyService : IVirtualKeyService
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly ILogger<ApiVirtualKeyService> _logger;

        /// <summary>
        /// Initializes a new instance of the ApiVirtualKeyService
        /// </summary>
        public ApiVirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            ILogger<ApiVirtualKeyService> logger)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            throw new NotImplementedException("Key generation not supported in the API service");
        }

        /// <inheritdoc />
        public Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            throw new NotImplementedException("Key info retrieval not supported in the API service");
        }

        /// <inheritdoc />
        public Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            throw new NotImplementedException("Key listing not supported in the API service");
        }

        /// <inheritdoc />
        public Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            throw new NotImplementedException("Key updating not supported in the API service");
        }

        /// <inheritdoc />
        public Task<bool> DeleteVirtualKeyAsync(int id)
        {
            throw new NotImplementedException("Key deletion not supported in the API service");
        }

        /// <inheritdoc />
        public async Task<bool> ResetSpendAsync(int id)
        {
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null) return false;

            try
            {
                // Record the spend history before resetting
                if (virtualKey.CurrentSpend > 0)
                {
                    var spendHistory = new VirtualKeySpendHistory
                    {
                        VirtualKeyId = virtualKey.Id,
                        Amount = virtualKey.CurrentSpend,
                        Date = DateTime.UtcNow
                    };
                    await _spendHistoryRepository.CreateAsync(spendHistory);
                }

                // Reset spend to zero
                virtualKey.CurrentSpend = 0;
                virtualKey.BudgetStartDate = DateTime.UtcNow;
                virtualKey.UpdatedAt = DateTime.UtcNow;

                return await _virtualKeyRepository.UpdateAsync(virtualKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting spend for virtual key with ID {KeyId}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Empty key provided for validation");
                return null;
            }

            // The key should be a hash already, and we'll query directly
            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(key);
            if (virtualKey == null)
            {
                _logger.LogWarning("No matching virtual key found");
                return null;
            }

            // Check if key is enabled
            if (!virtualKey.IsEnabled)
            {
                _logger.LogWarning("Virtual key is disabled: {KeyName} (ID: {KeyId})", virtualKey.KeyName, virtualKey.Id);
                return null;
            }

            // Check expiration
            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Virtual key has expired: {KeyName} (ID: {KeyId}), expired at {ExpiryDate}",
                    virtualKey.KeyName, virtualKey.Id, virtualKey.ExpiresAt);
                return null;
            }

            // Check budget
            if (virtualKey.MaxBudget.HasValue && virtualKey.CurrentSpend >= virtualKey.MaxBudget.Value)
            {
                _logger.LogWarning("Virtual key budget depleted: {KeyName} (ID: {KeyId}), spent {CurrentSpend}, budget {MaxBudget}",
                    virtualKey.KeyName, virtualKey.Id, virtualKey.CurrentSpend, virtualKey.MaxBudget);
                return null;
            }

            // Check if model is allowed, if model restrictions are in place
            if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
            {
                bool isModelAllowed = IsModelAllowed(requestedModel, virtualKey.AllowedModels);

                if (!isModelAllowed)
                {
                    _logger.LogWarning("Virtual key {KeyName} (ID: {KeyId}) attempted to access restricted model: {RequestedModel}",
                        virtualKey.KeyName, virtualKey.Id, requestedModel);
                    return null;
                }
            }

            // All validations passed
            _logger.LogInformation("Validated virtual key successfully: {KeyName} (ID: {KeyId})",
                virtualKey.KeyName, virtualKey.Id);
            return virtualKey;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            if (cost <= 0) return true; // No cost to add, consider it successful

            var virtualKey = await _virtualKeyRepository.GetByIdAsync(keyId);
            if (virtualKey == null) return false;

            try
            {
                // Update spend and timestamp
                virtualKey.CurrentSpend += cost;
                virtualKey.UpdatedAt = DateTime.UtcNow;

                bool success = await _virtualKeyRepository.UpdateAsync(virtualKey);
                if (success)
                {
                    _logger.LogInformation("Updated spend for key ID {KeyId}. New spend: {CurrentSpend}", keyId, virtualKey.CurrentSpend);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating spend for key ID {KeyId}.", keyId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
            if (virtualKey == null ||
                string.IsNullOrEmpty(virtualKey.BudgetDuration) ||
                !virtualKey.BudgetStartDate.HasValue)
            {
                // Can't reset budget if key doesn't exist or has no budget duration/start date
                return false;
            }

            DateTime now = DateTime.UtcNow;
            bool needsReset = false;

            // Calculate when the current budget period should end
            if (virtualKey.BudgetDuration.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
            {
                // For monthly, check if we're in a new month from the start date
                DateTime startDate = virtualKey.BudgetStartDate.Value;
                DateTime periodEnd = new DateTime(
                    startDate.Year + (startDate.Month == 12 ? 1 : 0),
                    startDate.Month == 12 ? 1 : startDate.Month + 1,
                    1,
                    0, 0, 0,
                    DateTimeKind.Utc).AddDays(-1); // Last day of the month

                needsReset = now > periodEnd;
            }
            else if (virtualKey.BudgetDuration.Equals("Daily", StringComparison.OrdinalIgnoreCase))
            {
                // For daily, check if we're on a different calendar day (UTC)
                needsReset = now.Date > virtualKey.BudgetStartDate.Value.Date;
            }

            if (needsReset)
            {
                try
                {
                    // Record the spend history before resetting
                    if (virtualKey.CurrentSpend > 0)
                    {
                        var spendHistory = new VirtualKeySpendHistory
                        {
                            VirtualKeyId = virtualKey.Id,
                            Amount = virtualKey.CurrentSpend,
                            Date = DateTime.UtcNow
                        };
                        await _spendHistoryRepository.CreateAsync(spendHistory, cancellationToken);
                    }

                    _logger.LogInformation(
                        "Resetting budget for key ID {KeyId}. Previous spend: {PreviousSpend}, Previous start date: {PreviousStartDate}",
                        keyId, virtualKey.CurrentSpend, virtualKey.BudgetStartDate);

                    // Reset the spend
                    virtualKey.CurrentSpend = 0;

                    // Set new budget start date based on duration
                    if (virtualKey.BudgetDuration.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
                    {
                        virtualKey.BudgetStartDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    }
                    else
                    {
                        virtualKey.BudgetStartDate = DateTime.UtcNow.Date; // Start of current day (UTC)
                    }

                    virtualKey.UpdatedAt = now;

                    bool success = await _virtualKeyRepository.UpdateAsync(virtualKey, cancellationToken);

                    if (success)
                    {
                        _logger.LogInformation(
                            "Budget reset completed for key ID {KeyId}. New start date: {NewStartDate}",
                            keyId, virtualKey.BudgetStartDate);
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting budget for key ID {KeyId}", keyId);
                    return false;
                }
            }

            return false; // No reset needed
        }

        /// <inheritdoc />
        public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await _virtualKeyRepository.GetByIdAsync(keyId, cancellationToken);
        }

        // Helper method to check if a model is allowed
        private bool IsModelAllowed(string requestedModel, string allowedModels)
        {
            if (string.IsNullOrEmpty(allowedModels))
                return true; // No restrictions

            var allowedModelsList = allowedModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // First check for exact match
            if (allowedModelsList.Any(m => string.Equals(m, requestedModel, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Then check for wildcard/prefix matches
            foreach (var allowedModel in allowedModelsList)
            {
                // Handle wildcards like "gpt-4*" to match any GPT-4 model
                if (allowedModel.EndsWith("*", StringComparison.OrdinalIgnoreCase) &&
                    allowedModel.Length > 1)
                {
                    string prefix = allowedModel.Substring(0, allowedModel.Length - 1);
                    if (requestedModel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
