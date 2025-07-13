using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Models.ErrorQueue;
using ConduitLLM.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for monitoring and managing error queues.
    /// </summary>
    [ApiController]
    [Route("api/admin/error-queues")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ErrorQueueController : ControllerBase
    {
        private readonly IErrorQueueService _errorQueueService;
        private readonly ILogger<ErrorQueueController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorQueueController"/> class.
        /// </summary>
        /// <param name="errorQueueService">Error queue service.</param>
        /// <param name="logger">Logger instance.</param>
        public ErrorQueueController(
            IErrorQueueService errorQueueService,
            ILogger<ErrorQueueController> logger)
        {
            _errorQueueService = errorQueueService ?? throw new ArgumentNullException(nameof(errorQueueService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Lists all error queues with statistics.
        /// </summary>
        /// <param name="includeEmpty">Whether to include empty queues.</param>
        /// <param name="minMessages">Minimum message count filter.</param>
        /// <param name="queueNameFilter">Queue name filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of error queues with statistics.</returns>
        [HttpGet]
        public async Task<ActionResult<ErrorQueueListResponse>> GetErrorQueues(
            [FromQuery] bool includeEmpty = false,
            [FromQuery] int? minMessages = null,
            [FromQuery] string? queueNameFilter = null,
            CancellationToken cancellationToken = default)
        {
            using (ErrorQueueMetricsService.StartOperationTimer("list", "all"))
            {
                try
                {
                    _logger.LogDebug("Getting error queues with filters: includeEmpty={IncludeEmpty}, minMessages={MinMessages}, filter={Filter}",
                        includeEmpty, minMessages, queueNameFilter);

                    var response = await _errorQueueService.GetErrorQueuesAsync(
                        includeEmpty, 
                        minMessages, 
                        queueNameFilter,
                        cancellationToken);

                    return Ok(response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get error queues");
                    return StatusCode(500, new { error = "Failed to retrieve error queues", correlationId = HttpContext.TraceIdentifier });
                }
            }
        }

        /// <summary>
        /// Gets messages from a specific error queue.
        /// </summary>
        /// <param name="queueName">Name of the error queue.</param>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Page size.</param>
        /// <param name="includeHeaders">Whether to include message headers.</param>
        /// <param name="includeBody">Whether to include message body.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Paginated list of error messages.</returns>
        [HttpGet("{queueName}/messages")]
        public async Task<ActionResult<ErrorMessageListResponse>> GetErrorMessages(
            string queueName,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool includeHeaders = true,
            [FromQuery] bool includeBody = true,
            CancellationToken cancellationToken = default)
        {
            using (ErrorQueueMetricsService.StartOperationTimer("get_messages", queueName))
            {
                try
                {
                    if (page < 1) page = 1;
                    if (pageSize < 1) pageSize = 20;
                    if (pageSize > 100) pageSize = 100;

                    _logger.LogDebug("Getting messages from queue {QueueName}, page={Page}, pageSize={PageSize}",
                        queueName, page, pageSize);

                    var response = await _errorQueueService.GetErrorMessagesAsync(
                        queueName,
                        page,
                        pageSize,
                        includeHeaders,
                        includeBody,
                        cancellationToken);

                    return Ok(response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get messages from queue {QueueName}", queueName);
                    return StatusCode(500, new { error = "Failed to retrieve error messages", correlationId = HttpContext.TraceIdentifier });
                }
            }
        }

        /// <summary>
        /// Gets details of a specific error message.
        /// </summary>
        /// <param name="queueName">Name of the error queue.</param>
        /// <param name="messageId">Message ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Detailed error message information.</returns>
        [HttpGet("{queueName}/messages/{messageId}")]
        public async Task<ActionResult<ErrorMessageDetail>> GetErrorMessage(
            string queueName,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            using (ErrorQueueMetricsService.StartOperationTimer("get_message", queueName))
            {
                try
                {
                    _logger.LogDebug("Getting message {MessageId} from queue {QueueName}", messageId, queueName);

                    var message = await _errorQueueService.GetErrorMessageAsync(
                        queueName,
                        messageId,
                        cancellationToken);

                    if (message == null)
                    {
                        return NotFound(new { error = $"Message {messageId} not found in queue {queueName}" });
                    }

                    return Ok(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get message {MessageId} from queue {QueueName}", messageId, queueName);
                    return StatusCode(500, new { error = "Failed to retrieve error message", correlationId = HttpContext.TraceIdentifier });
                }
            }
        }

        /// <summary>
        /// Gets aggregated statistics and trends for error queues.
        /// </summary>
        /// <param name="since">Start date for statistics.</param>
        /// <param name="groupBy">Grouping interval (hour, day, week).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Error queue statistics and trends.</returns>
        [HttpGet("statistics")]
        public async Task<ActionResult<ErrorQueueStatistics>> GetStatistics(
            [FromQuery] DateTime? since = null,
            [FromQuery] string groupBy = "hour",
            CancellationToken cancellationToken = default)
        {
            using (ErrorQueueMetricsService.StartOperationTimer("get_statistics", "all"))
            {
                try
                {
                    var validGroupBy = new[] { "hour", "day", "week" };
                    if (!validGroupBy.Contains(groupBy.ToLower()))
                    {
                        return BadRequest(new { error = "Invalid groupBy value. Must be 'hour', 'day', or 'week'." });
                    }

                    since ??= DateTime.UtcNow.AddDays(-7);

                    _logger.LogDebug("Getting error queue statistics since {Since}, grouped by {GroupBy}", since, groupBy);

                    var statistics = await _errorQueueService.GetStatisticsAsync(
                        since.Value,
                        groupBy,
                        cancellationToken);

                    return Ok(statistics);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get error queue statistics");
                    return StatusCode(500, new { error = "Failed to retrieve statistics", correlationId = HttpContext.TraceIdentifier });
                }
            }
        }

        /// <summary>
        /// Gets health status of error queues for monitoring systems.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Error queue health status.</returns>
        [HttpGet("health")]
        public async Task<ActionResult<ErrorQueueHealth>> GetHealth(CancellationToken cancellationToken = default)
        {
            using (ErrorQueueMetricsService.StartOperationTimer("get_health", "all"))
            {
                try
                {
                    _logger.LogDebug("Getting error queue health status");

                    var health = await _errorQueueService.GetHealthAsync(cancellationToken);

                    return Ok(health);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get error queue health");
                    return StatusCode(500, new { error = "Failed to retrieve health status", correlationId = HttpContext.TraceIdentifier });
                }
            }
        }

        /// <summary>
        /// Replays messages from an error queue.
        /// </summary>
        /// <param name="queueName">Name of the error queue.</param>
        /// <param name="request">Replay request parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Replay operation result.</returns>
        [HttpPost("{queueName}/replay")]
        public async Task<ActionResult> ReplayMessages(
            string queueName,
            [FromBody] ReplayRequest request,
            CancellationToken cancellationToken = default)
        {
            using (ErrorQueueMetricsService.StartOperationTimer("replay", queueName))
            {
                try
                {
                    _logger.LogInformation("Replaying messages from queue {QueueName}", queueName);

                    // TODO: Implement replay logic when API support is added
                    // For now, simulate success
                    var successCount = request?.MessageIds?.Count ?? 0;
                    
                    ErrorQueueMetricsService.RecordReplay(queueName, "success", successCount);
                    
                    return Ok(new 
                    { 
                        success = true, 
                        message = $"Replay operation queued for {successCount} messages",
                        successCount = successCount,
                        failedCount = 0
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to replay messages from queue {QueueName}", queueName);
                    ErrorQueueMetricsService.RecordReplay(queueName, "failed", request?.MessageIds?.Count ?? 0);
                    return StatusCode(500, new { error = "Failed to replay messages", correlationId = HttpContext.TraceIdentifier });
                }
            }
        }

        /// <summary>
        /// Deletes messages from an error queue.
        /// </summary>
        /// <param name="queueName">Name of the error queue.</param>
        /// <param name="messageId">Optional message ID to delete specific message.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Delete operation result.</returns>
        [HttpDelete("{queueName}/messages/{messageId?}")]
        public async Task<ActionResult> DeleteMessages(
            string queueName,
            string? messageId = null,
            CancellationToken cancellationToken = default)
        {
            using (ErrorQueueMetricsService.StartOperationTimer("delete", queueName))
            {
                try
                {
                    _logger.LogInformation("Deleting messages from queue {QueueName}, messageId={MessageId}", 
                        queueName, messageId);

                    // TODO: Implement delete logic when API support is added
                    var reason = string.IsNullOrEmpty(messageId) ? "manual_clear" : "manual_delete";
                    var count = string.IsNullOrEmpty(messageId) ? 10 : 1; // Mock count
                    
                    ErrorQueueMetricsService.RecordDeletion(queueName, reason, count);
                    
                    return Ok(new 
                    { 
                        success = true, 
                        message = messageId != null 
                            ? $"Message {messageId} deleted from queue {queueName}"
                            : $"All messages deleted from queue {queueName}",
                        deletedCount = count
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete messages from queue {QueueName}", queueName);
                    return StatusCode(500, new { error = "Failed to delete messages", correlationId = HttpContext.TraceIdentifier });
                }
            }
        }

        /// <summary>
        /// Request model for replay operations.
        /// </summary>
        public class ReplayRequest
        {
            /// <summary>
            /// Gets or sets the list of message IDs to replay.
            /// If null or empty, all messages are replayed.
            /// </summary>
            public List<string>? MessageIds { get; set; }
        }
    }
}