using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Interfaces;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Helper methods for updating virtual key spending.
    /// </summary>
    public static class SpendUpdateHelper
    {
        /// <summary>
        /// Updates virtual key spending through batch service or direct update.
        /// </summary>
        public static async Task UpdateSpendAsync(
            int virtualKeyId,
            decimal cost,
            IBatchSpendUpdateService batchSpendService,
            IVirtualKeyService virtualKeyService,
            ILogger logger)
        {
            try
            {
                // Try batch update first
                if (batchSpendService.IsHealthy)
                {
                    batchSpendService.QueueSpendUpdate(virtualKeyId, cost);
                }
                else
                {
                    // Fallback to direct update
                    logger.LogWarning("BatchSpendUpdateService unhealthy, using direct update for VirtualKey {VirtualKeyId}", virtualKeyId);
                    await virtualKeyService.UpdateSpendAsync(virtualKeyId, cost);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update spend for VirtualKey {VirtualKeyId}, Cost {Cost:C}", virtualKeyId, cost);
                // Don't throw - we've already sent the response to the user
            }
        }
    }
}