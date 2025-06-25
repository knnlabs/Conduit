using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// No-operation implementation of webhook delivery tracker
    /// Used when Redis is not available
    /// </summary>
    public class NoOpWebhookDeliveryTracker : IWebhookDeliveryTracker
    {
        public Task<bool> IsDeliveredAsync(string deliveryKey)
        {
            // Always return false to allow delivery
            return Task.FromResult(false);
        }
        
        public Task MarkDeliveredAsync(string deliveryKey, string webhookUrl)
        {
            // No-op
            return Task.CompletedTask;
        }
        
        public Task<WebhookDeliveryStats> GetStatsAsync(string webhookUrl)
        {
            // Return empty stats
            return Task.FromResult(new WebhookDeliveryStats());
        }
        
        public Task RecordFailureAsync(string deliveryKey, string webhookUrl, string error)
        {
            // No-op
            return Task.CompletedTask;
        }
    }
}