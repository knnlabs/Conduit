using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for maintaining virtual keys including budget resets and expiration
    /// </summary>
    public class VirtualKeyMaintenanceService
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly ILogger<VirtualKeyMaintenanceService> _logger;
        
        /// <summary>
        /// Initializes a new instance of the VirtualKeyMaintenanceService
        /// </summary>
        /// <param name="virtualKeyRepository">The virtual key repository</param>
        /// <param name="spendHistoryRepository">The virtual key spend history repository</param>
        /// <param name="logger">The logger</param>
        public VirtualKeyMaintenanceService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            ILogger<VirtualKeyMaintenanceService> logger)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Processes budget resets for all virtual keys
        /// </summary>
        public async Task ProcessBudgetResetsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                
                // Get all active keys with budget durations
                var allKeys = await _virtualKeyRepository.GetAllAsync();
                var keysToCheck = allKeys
                    .Where(k => k.IsEnabled)
                    .Where(k => !string.IsNullOrEmpty(k.BudgetDuration))
                    .ToList();
                    
                if (!keysToCheck.Any())
                {
                    return;
                }
                
                var keysToReset = new List<VirtualKey>();
                var spendHistory = new List<VirtualKeySpendHistory>();
                
                // Determine which keys need resetting
                foreach (var key in keysToCheck)
                {
                    bool shouldReset = false;
                    
                    switch (key.BudgetDuration?.ToLower())
                    {
                        case "daily":
                            // Reset if the day has changed
                            shouldReset = key.BudgetStartDate?.Date < now.Date;
                            break;
                            
                        case "monthly":
                            // Reset if the month or year has changed
                            shouldReset = key.BudgetStartDate?.Month != now.Month || 
                                        key.BudgetStartDate?.Year != now.Year;
                            break;
                        
                        // Add more budget periods as needed
                    }
                    
                    if (shouldReset)
                    {
                        keysToReset.Add(key);
                        
                        // Create history record
                        spendHistory.Add(new VirtualKeySpendHistory
                        {
                            VirtualKeyId = key.Id,
                            Amount = key.CurrentSpend,
                            Timestamp = key.BudgetStartDate ?? now
                        });
                    }
                }
                
                if (!keysToReset.Any())
                {
                    return;
                }
                
                _logger.LogInformation("Processing budget resets for {Count} virtual keys", keysToReset.Count);
                
                // Add spend history records
                foreach (var history in spendHistory)
                {
                    await _spendHistoryRepository.CreateAsync(history);
                }
                
                // Update keys to reset spending
                foreach (var key in keysToReset)
                {
                    // Reset the spending
                    key.CurrentSpend = 0;
                    key.BudgetStartDate = now;
                    key.UpdatedAt = now;
                    
                    await _virtualKeyRepository.UpdateAsync(key);
                }
                
                _logger.LogInformation("Successfully processed budget resets for {Count} virtual keys", keysToReset.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing budget resets for virtual keys");
                throw;
            }
        }
        
        /// <summary>
        /// Disables all expired virtual keys
        /// </summary>
        public async Task DisableExpiredKeysAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                
                // Get all active keys with expiration dates that have passed
                var allKeys = await _virtualKeyRepository.GetAllAsync();
                var expiredKeys = allKeys
                    .Where(k => k.IsEnabled)
                    .Where(k => k.ExpiresAt.HasValue && k.ExpiresAt.Value < now)
                    .ToList();
                    
                if (!expiredKeys.Any())
                {
                    return;
                }
                
                _logger.LogInformation("Disabling {Count} expired virtual keys", expiredKeys.Count);
                
                // Update keys to disable them
                foreach (var key in expiredKeys)
                {
                    key.IsEnabled = false;
                    key.UpdatedAt = now;
                    
                    await _virtualKeyRepository.UpdateAsync(key);
                }
                
                _logger.LogInformation("Successfully disabled {Count} expired virtual keys", expiredKeys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling expired virtual keys");
                throw;
            }
        }
        
        /// <summary>
        /// Checks keys approaching budget limits and returns their IDs
        /// </summary>
        /// <param name="thresholdPercentage">Threshold percentage (e.g., 90 for 90%)</param>
        /// <returns>List of key IDs approaching budget limits</returns>
        public async Task<List<int>> GetKeysApproachingBudgetLimitAsync(int thresholdPercentage = 90)
        {
            try
            {
                // Convert percentage to decimal (e.g., 90% -> 0.9)
                decimal thresholdDecimal = thresholdPercentage / 100m;
                
                // Get all active keys with budget limits
                var allKeys = await _virtualKeyRepository.GetAllAsync();
                return allKeys
                    .Where(k => k.IsEnabled)
                    .Where(k => k.MaxBudget.HasValue && k.MaxBudget.Value > 0)
                    .Where(k => (k.CurrentSpend / k.MaxBudget!.Value) >= thresholdDecimal)
                    .Select(k => k.Id)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking keys approaching budget limit of {ThresholdPercentage}%", thresholdPercentage);
                throw;
            }
        }
    }
}