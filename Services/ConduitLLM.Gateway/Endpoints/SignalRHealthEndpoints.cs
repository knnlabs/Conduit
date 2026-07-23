using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.DTOs;

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
            var activeConnections = connections.ToList();
            return Ok(new ConnectionDetailsResponse(activeConnections, activeConnections.Count));
        }

        /// <summary>
        /// Gets connections for a specific hub.
        /// Access controlled by health endpoint middleware (private network or valid health key).
        /// </summary>
        public async Task<IResult> GetHubConnections(string hubName)
        {
            var connections = await _connectionMonitor.GetHubConnectionsAsync(hubName);
            var projected = connections.Select(c => new HubConnectionDto(
                c.ConnectionId,
                c.ConnectedAt,
                c.ConnectionDuration,
                c.Groups,
                c.MessagesSent,
                c.MessagesAcknowledged)).ToList();
            return Ok(new HubConnectionsResponse(hubName, projected, projected.Count));
        }

        /// <summary>
        /// Gets connections for a specific virtual key
        /// </summary>
        public async Task<IResult> GetVirtualKeyConnections(int virtualKeyId)
        {
            // Check if the requester has permission to view this virtual key's connections
            // This would normally involve checking if the requester owns or has admin access to the key

            var connections = await _connectionMonitor.GetVirtualKeyConnectionsAsync(virtualKeyId);
            var projected = connections.Select(c => new VirtualKeyConnectionDto(
                c.ConnectionId,
                c.HubName,
                c.ConnectedAt,
                c.ConnectionDuration,
                c.Groups)).ToList();
            return Ok(new VirtualKeyConnectionsResponse(virtualKeyId, projected, projected.Count));
        }

        /// <summary>
        /// Gets connections in a specific group.
        /// Access controlled by health endpoint middleware (private network or valid health key).
        /// </summary>
        public async Task<IResult> GetGroupConnections(string groupName)
        {
            var connections = await _connectionMonitor.GetGroupConnectionsAsync(groupName);
            var projected = connections.Select(c => new GroupConnectionDto(
                c.ConnectionId,
                c.HubName,
                c.ConnectedAt,
                c.VirtualKeyId)).ToList();
            return Ok(new GroupConnectionsResponse(groupName, projected, projected.Count));
        }

        /// <summary>
        /// Gets dead letter queue messages
        /// </summary>
        public IResult GetDeadLetterMessages()
        {
            var messages = _messageQueueService.GetDeadLetterMessages();
            var projected = messages.Select(m => new DeadLetterMessageDto(
                m.Message.MessageId,
                m.Message.MessageType,
                m.HubName,
                m.MethodName,
                m.QueuedAt,
                m.DeliveryAttempts,
                m.LastError,
                m.DeadLetterReason)).ToList();
            return Ok(new DeadLetterMessagesResponse(projected, projected.Count));
        }

        /// <summary>
        /// Requeues a dead letter message
        /// </summary>
        public async Task<IResult> RequeueDeadLetter(string messageId)
        {
            await _messageQueueService.RequeueDeadLetterAsync(messageId);
            Logger.LogInformation("Dead letter message {MessageId} requeued by admin", messageId);
            return Ok(new MessageResponse("Message requeued successfully"));
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

            return Ok(new SignalRHealthResponse(
                isHealthy ? "Healthy" : "Degraded",
                DateTime.UtcNow,
                new SignalRConnectionHealthDto(
                    connectionStats.TotalActiveConnections,
                    connectionStats.StaleConnections,
                    $"{connectionStats.AcknowledgmentRate:F2}%"),
                new SignalRQueueHealthDto(
                    queueStats.PendingMessages,
                    queueStats.DeadLetterMessages,
                    queueStats.CircuitBreakerState.ToString(),
                    queueStats.ProcessedMessages,
                    queueStats.FailedMessages)));
        }
    }
}
