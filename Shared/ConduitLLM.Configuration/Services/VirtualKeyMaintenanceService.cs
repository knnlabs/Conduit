using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
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
            // Budget tracking is now at the group level
            // This method is deprecated but kept for interface compatibility
            _logger.LogDebug("ProcessBudgetResetsAsync called but budget tracking is now at group level");
            await Task.CompletedTask;
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

                if (expiredKeys.Count() == 0)
                {
                    return;
                }

                _logger.LogInformation("Disabling {Count} expired virtual keys", expiredKeys.Count());

                // Update keys to disable them
                foreach (var key in expiredKeys)
                {
                    key.IsEnabled = false;
                    key.UpdatedAt = now;

                    await _virtualKeyRepository.UpdateAsync(key);
                }

                _logger.LogInformation("Successfully disabled {Count} expired virtual keys", expiredKeys.Count());
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
            // Budget tracking is now at the group level
            // This method is deprecated but kept for interface compatibility
            _logger.LogDebug("GetKeysApproachingBudgetLimitAsync called but budget tracking is now at group level");
            await Task.CompletedTask;
            return new List<int>();
        }
    }
}
