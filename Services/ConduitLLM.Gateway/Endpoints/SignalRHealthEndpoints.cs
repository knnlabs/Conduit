using ConduitLLM.Gateway.Services;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Controller for SignalR health and monitoring endpoints.
    /// Access is controlled by the HealthEndpointAuthorizationMiddleware which allows
    /// requests from private networks or with a valid X-Conduit-Health-Key header.
    /// </summary>
    /// <remarks>
    /// The middleware handles basic health endpoint authorization (private network or health key).
    /// Methods without explicit auth attributes are protected by the middleware.
    /// Methods with [Authorize(Policy = "AdminOnly")] require additional backend authentication.
    /// </remarks>
    public class SignalRHealthEndpoints : GatewayEndpointHandlerBase
    {
        private readonly ISignalRConnectionMonitor _connectionMonitor;
        private readonly ISignalRMessageQueueService _messageQueueService;
        private readonly ISignalRAcknowledgmentService _acknowledgmentService;

        public SignalRHealthEndpoints(
            ISignalRConnectionMonitor connectionMonitor,
            ISignalRMessageQueueService messageQueueService,
            ISignalRAcknowledgmentService acknowledgmentService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SignalRHealthEndpoints> logger)
            : base(null, httpContextAccessor, logger)
        {
            _connectionMonitor = connectionMonitor;
            _messageQueueService = messageQueueService;
            _acknowledgmentService = acknowledgmentService;
        }

        /// <summary>
        /// Gets SignalR connection statistics.
        /// Access controlled by health endpoint middleware (private network or valid health key).
        /// </summary>
        public async Task<IResult> GetConnectionStatistics()
        {
            var stats = await _connectionMonitor.GetStatisticsAsync();
            return Ok(stats);
        }

        /// <summary>
        /// Gets SignalR message queue statistics.
        /// Access controlled by health endpoint middleware (private network or valid health key).
        /// </summary>
        public IResult GetQueueStatistics()
        {
            var stats = _messageQueueService.GetStatistics();
            return Ok(stats);
        }

        /// <summary>
        /// Gets detailed connection information (requires admin auth)
        /// </summary>
        public async Task<IResult> GetConnectionDetails()
        {
            var connections = await _connectionMonitor.GetActiveConnectionsAsync();
            return Ok(new
            {
                activeConnections = connections,
                count = connections.Count()
            });
        }

        /// <summary>
        /// Gets connections for a specific hub.
        /// Access controlled by health endpoint middleware (private network or valid health key).
        /// </summary>
        public async Task<IResult> GetHubConnections(string hubName)
        {
            var connections = await _connectionMonitor.GetHubConnectionsAsync(hubName);
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
        public async Task<IResult> GetVirtualKeyConnections(int virtualKeyId)
        {
            // Check if the requester has permission to view this virtual key's connections
            // This would normally involve checking if the requester owns or has admin access to the key

            var connections = await _connectionMonitor.GetVirtualKeyConnectionsAsync(virtualKeyId);
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
        /// Gets connections in a specific group.
        /// Access controlled by health endpoint middleware (private network or valid health key).
        /// </summary>
        public async Task<IResult> GetGroupConnections(string groupName)
        {
            var connections = await _connectionMonitor.GetGroupConnectionsAsync(groupName);
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
        public IResult GetDeadLetterMessages()
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
        public async Task<IResult> RequeueDeadLetter(string messageId)
        {
            await _messageQueueService.RequeueDeadLetterAsync(messageId);
            Logger.LogInformation("Dead letter message {MessageId} requeued by admin", messageId);
            return Ok(new { message = "Message requeued successfully" });
        }

        /// <summary>
        /// Gets overall SignalR health status.
        /// Access controlled by health endpoint middleware (private network or valid health key).
        /// </summary>
        public async Task<IResult> GetHealthStatus()
        {
            var connectionStats = await _connectionMonitor.GetStatisticsAsync();
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
