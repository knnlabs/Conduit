using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// No-operation implementation of webhook delivery tracker.
    /// Used when Redis is not available — delivery deduplication is disabled.
    /// </summary>
    public class NoOpWebhookDeliveryTracker : IWebhookDeliveryTracker
    {
        private readonly ILogger<NoOpWebhookDeliveryTracker> _logger;
        private int _loggedFallbackWarning;

        public NoOpWebhookDeliveryTracker(ILogger<NoOpWebhookDeliveryTracker> logger)
        {
            _logger = logger;
        }

        public Task<bool> IsDeliveredAsync(string deliveryKey)
        {
            LogFallbackWarningOnce();
            // Always return false to allow delivery — no deduplication without Redis
            return Task.FromResult(false);
        }

        public Task MarkDeliveredAsync(string deliveryKey, string webhookUrl)
        {
            // No-op — delivery tracking disabled without Redis
            return Task.CompletedTask;
        }

        public Task<WebhookDeliveryStats> GetStatsAsync(string webhookUrl)
        {
            // Return empty stats — no tracking available
            return Task.FromResult(new WebhookDeliveryStats());
        }

        public Task RecordFailureAsync(string deliveryKey, string webhookUrl, string error)
        {
            _logger.LogDebug(
                "Webhook delivery failure not tracked (Redis unavailable): {DeliveryKey} to {WebhookUrl}",
                deliveryKey, webhookUrl);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs a one-time warning that the no-op tracker is active (Redis unavailable).
        /// </summary>
        private void LogFallbackWarningOnce()
        {
            if (Interlocked.CompareExchange(ref _loggedFallbackWarning, 1, 0) == 0)
            {
                _logger.LogWarning(
                    "NoOpWebhookDeliveryTracker is active — webhook delivery deduplication is disabled. " +
                    "Configure Redis to enable delivery tracking");
            }
        }
    }
}
