using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for sending webhook notifications to external endpoints.
    /// </summary>
    public interface IWebhookNotificationService
    {
        /// <summary>
        /// Sends a webhook notification about task completion.
        /// </summary>
        /// <param name="webhookUrl">The URL to send the notification to.</param>
        /// <param name="payload">The payload to send in the webhook request.</param>
        /// <param name="headers">Optional headers to include in the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the webhook was sent successfully, false otherwise.</returns>
        Task<bool> SendTaskCompletionWebhookAsync(
            string webhookUrl,
            object payload,
            Dictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a webhook notification about task progress.
        /// </summary>
        /// <param name="webhookUrl">The URL to send the notification to.</param>
        /// <param name="payload">The payload to send in the webhook request.</param>
        /// <param name="headers">Optional headers to include in the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the webhook was sent successfully, false otherwise.</returns>
        Task<bool> SendTaskProgressWebhookAsync(
            string webhookUrl,
            object payload,
            Dictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default);
    }
}