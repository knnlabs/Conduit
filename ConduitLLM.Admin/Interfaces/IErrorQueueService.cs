using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Models.ErrorQueue;

using ConduitLLM.Admin.Interfaces;
namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for managing and monitoring error queues.
    /// </summary>
    public interface IErrorQueueService
    {
        /// <summary>
        /// Gets list of error queues with their statistics.
        /// </summary>
        /// <param name="includeEmpty">Whether to include empty queues.</param>
        /// <param name="minMessages">Minimum message count filter.</param>
        /// <param name="queueNameFilter">Queue name filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Error queue list response.</returns>
        Task<ErrorQueueListResponse> GetErrorQueuesAsync(
            bool includeEmpty = false,
            int? minMessages = null,
            string? queueNameFilter = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets paginated messages from a specific error queue.
        /// </summary>
        /// <param name="queueName">Name of the error queue.</param>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Page size.</param>
        /// <param name="includeHeaders">Whether to include message headers.</param>
        /// <param name="includeBody">Whether to include message body.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Paginated error message list.</returns>
        Task<ErrorMessageListResponse> GetErrorMessagesAsync(
            string queueName,
            int page = 1,
            int pageSize = 20,
            bool includeHeaders = true,
            bool includeBody = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets details of a specific error message.
        /// </summary>
        /// <param name="queueName">Name of the error queue.</param>
        /// <param name="messageId">Message identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Detailed error message or null if not found.</returns>
        Task<ErrorMessageDetail?> GetErrorMessageAsync(
            string queueName,
            string messageId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets aggregated statistics and trends for error queues.
        /// </summary>
        /// <param name="since">Start date for statistics.</param>
        /// <param name="groupBy">Grouping interval (hour, day, week).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Error queue statistics.</returns>
        Task<ErrorQueueStatistics> GetStatisticsAsync(
            DateTime since,
            string groupBy = "hour",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets health status of error queues.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Error queue health status.</returns>
        Task<ErrorQueueHealth> GetHealthAsync(CancellationToken cancellationToken = default);
    }
}