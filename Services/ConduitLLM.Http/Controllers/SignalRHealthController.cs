using ConduitLLM.Http.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for SignalR health and monitoring endpoints
    /// </summary>
    [ApiController]
    [Route("health/signalr")]
    public class SignalRHealthController : ControllerBase
    {
        private readonly ISignalRConnectionMonitor _connectionMonitor;
        private readonly ISignalRMessageQueueService _messageQueueService;
        private readonly ISignalRAcknowledgmentService _acknowledgmentService;
        private readonly ILogger<SignalRHealthController> _logger;

        public SignalRHealthController(
            ISignalRConnectionMonitor connectionMonitor,
            ISignalRMessageQueueService messageQueueService,
            ISignalRAcknowledgmentService acknowledgmentService,
            ILogger<SignalRHealthController> logger)
        {
            _connectionMonitor = connectionMonitor;
            _messageQueueService = messageQueueService;
            _acknowledgmentService = acknowledgmentService;
            _logger = logger;
        }

        /// <summary>
        /// Gets SignalR connection statistics
        /// </summary>
        [HttpGet("connections")]
        [AllowAnonymous]
        public ActionResult<ConnectionStatistics> GetConnectionStatistics()
        {
            var stats = _connectionMonitor.GetStatistics();
            return Ok(stats);
        }

        /// <summary>
        /// Gets SignalR message queue statistics
        /// </summary>
        [HttpGet("queue")]
        [AllowAnonymous]
        public ActionResult<QueueStatistics> GetQueueStatistics()
        {
            var stats = _messageQueueService.GetStatistics();
            return Ok(stats);
        }

        /// <summary>
        /// Gets detailed connection information (requires admin auth)
        /// </summary>
        [HttpGet("connections/details")]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult<object> GetConnectionDetails()
        {
            var connections = _connectionMonitor.GetActiveConnections();
            return Ok(new
            {
                activeConnections = connections,
                count = connections.Count()
            });
        }

        /// <summary>
        /// Gets connections for a specific hub
        /// </summary>
        [HttpGet("connections/hub/{hubName}")]
        [AllowAnonymous]
        public ActionResult<object> GetHubConnections(string hubName)
        {
            var connections = _connectionMonitor.GetHubConnections(hubName);
            return Ok(new
            {
                hubName,
                connections = connections.Select(c => new
                {
                    c.ConnectionId,
                    c.ConnectedAt,
                    c.ConnectionDuration,
                    c.Groups,
                    c.MessagesSent,
                    c.MessagesAcknowledged
                }),
                count = connections.Count()
            });
        }

        /// <summary>
        /// Gets connections for a specific virtual key
        /// </summary>
        [HttpGet("connections/key/{virtualKeyId}")]
        [Authorize]
        public ActionResult<object> GetVirtualKeyConnections(int virtualKeyId)
        {
            // Check if the requester has permission to view this virtual key's connections
            // This would normally involve checking if the requester owns or has admin access to the key
            
            var connections = _connectionMonitor.GetVirtualKeyConnections(virtualKeyId);
            return Ok(new
            {
                virtualKeyId,
                connections = connections.Select(c => new
                {
                    c.ConnectionId,
                    c.HubName,
                    c.ConnectedAt,
                    c.ConnectionDuration,
                    c.Groups
                }),
                count = connections.Count()
            });
        }

        /// <summary>
        /// Gets connections in a specific group
        /// </summary>
        [HttpGet("connections/group/{groupName}")]
        [AllowAnonymous]
        public ActionResult<object> GetGroupConnections(string groupName)
        {
            var connections = _connectionMonitor.GetGroupConnections(groupName);
            return Ok(new
            {
                groupName,
                connections = connections.Select(c => new
                {
                    c.ConnectionId,
                    c.HubName,
                    c.ConnectedAt,
                    c.VirtualKeyId
                }),
                count = connections.Count()
            });
        }

        /// <summary>
        /// Gets dead letter queue messages
        /// </summary>
        [HttpGet("queue/deadletter")]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult<object> GetDeadLetterMessages()
        {
            var messages = _messageQueueService.GetDeadLetterMessages();
            return Ok(new
            {
                messages = messages.Select(m => new
                {
                    m.Message.MessageId,
                    m.Message.MessageType,
                    m.HubName,
                    m.MethodName,
                    m.QueuedAt,
                    m.DeliveryAttempts,
                    m.LastError,
                    m.DeadLetterReason
                }),
                count = messages.Count()
            });
        }

        /// <summary>
        /// Requeues a dead letter message
        /// </summary>
        [HttpPost("queue/deadletter/{messageId}/requeue")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult> RequeueDeadLetter(string messageId)
        {
            await _messageQueueService.RequeueDeadLetterAsync(messageId);
            _logger.LogInformation("Dead letter message {MessageId} requeued by admin", messageId);
            return Ok(new { message = "Message requeued successfully" });
        }

        /// <summary>
        /// Gets overall SignalR health status
        /// </summary>
        [HttpGet]
        public ActionResult<object> GetHealthStatus()
        {
            var connectionStats = _connectionMonitor.GetStatistics();
            var queueStats = _messageQueueService.GetStatistics();

            var isHealthy = connectionStats.TotalActiveConnections >= 0 &&
                           queueStats.CircuitBreakerState != Polly.CircuitBreaker.CircuitState.Open &&
                           queueStats.DeadLetterMessages < 100; // Threshold for unhealthy

            return Ok(new
            {
                status = isHealthy ? "Healthy" : "Degraded",
                timestamp = DateTime.UtcNow,
                connections = new
                {
                    active = connectionStats.TotalActiveConnections,
                    stale = connectionStats.StaleConnections,
                    acknowledgmentRate = $"{connectionStats.AcknowledgmentRate:F2}%"
                },
                queue = new
                {
                    pending = queueStats.PendingMessages,
                    deadLetter = queueStats.DeadLetterMessages,
                    circuitBreaker = queueStats.CircuitBreakerState.ToString(),
                    processed = queueStats.ProcessedMessages,
                    failed = queueStats.FailedMessages
                }
            });
        }
    }
}
