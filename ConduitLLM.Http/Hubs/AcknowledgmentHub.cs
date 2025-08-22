using ConduitLLM.Http.Models;
using ConduitLLM.Http.Services;

using Microsoft.AspNetCore.SignalR;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Base hub that provides message acknowledgment functionality
    /// </summary>
    public abstract class AcknowledgmentHub : SecureHub
    {
        private readonly ISignalRAcknowledgmentService _acknowledgmentService;

        protected AcknowledgmentHub(
            ILogger logger,
            IServiceProvider serviceProvider,
            ISignalRAcknowledgmentService acknowledgmentService) : base(logger, serviceProvider)
        {
            _acknowledgmentService = acknowledgmentService ?? throw new ArgumentNullException(nameof(acknowledgmentService));
        }

        /// <summary>
        /// Acknowledges a message by its ID
        /// </summary>
        /// <param name="messageId">The ID of the message to acknowledge</param>
        public async Task AcknowledgeMessage(string messageId)
        {
            var success = await _acknowledgmentService.AcknowledgeMessageAsync(messageId, Context.ConnectionId);
            if (!success)
            {
                Logger.LogWarning(
                    "Failed to acknowledge message {MessageId} from connection {ConnectionId}",
                    messageId, Context.ConnectionId);
            }
        }

        /// <summary>
        /// Negatively acknowledges a message
        /// </summary>
        /// <param name="messageId">The ID of the message to NACK</param>
        /// <param name="errorMessage">Optional error message</param>
        public async Task NackMessage(string messageId, string? errorMessage = null)
        {
            var success = await _acknowledgmentService.NackMessageAsync(messageId, Context.ConnectionId, errorMessage);
            if (!success)
            {
                Logger.LogWarning(
                    "Failed to NACK message {MessageId} from connection {ConnectionId}",
                    messageId, Context.ConnectionId);
            }
        }

        /// <summary>
        /// Gets the status of a message acknowledgment
        /// </summary>
        /// <param name="messageId">The ID of the message</param>
        /// <returns>The acknowledgment status or null if not found</returns>
        public async Task<AcknowledgmentStatus?> GetMessageStatus(string messageId)
        {
            return await _acknowledgmentService.GetMessageStatusAsync(messageId);
        }

        /// <summary>
        /// Sends a message that requires acknowledgment
        /// </summary>
        protected async Task<PendingAcknowledgment> SendWithAcknowledgmentAsync(
            string methodName,
            SignalRMessage message,
            TimeSpan? timeout = null)
        {
            var pending = await _acknowledgmentService.RegisterMessageAsync(
                message,
                Context.ConnectionId,
                GetType().Name,
                methodName,
                timeout);

            await Clients.Caller.SendAsync(methodName, message);

            return pending;
        }

        /// <summary>
        /// Sends a message to a specific client that requires acknowledgment
        /// </summary>
        protected async Task<PendingAcknowledgment> SendToClientWithAcknowledgmentAsync(
            string connectionId,
            string methodName,
            SignalRMessage message,
            TimeSpan? timeout = null)
        {
            var pending = await _acknowledgmentService.RegisterMessageAsync(
                message,
                connectionId,
                GetType().Name,
                methodName,
                timeout);

            await Clients.Client(connectionId).SendAsync(methodName, message);

            return pending;
        }

        /// <summary>
        /// Sends a message to a group that requires acknowledgment from all members
        /// </summary>
        protected async Task SendToGroupWithAcknowledgmentAsync(
            string groupName,
            string methodName,
            SignalRMessage message,
            TimeSpan? timeout = null)
        {
            // For group messages, we'd need to track acknowledgments from all group members
            // This is a simplified version that just sends without tracking individual acknowledgments
            await Clients.Group(groupName).SendAsync(methodName, message);
            
            Logger.LogDebug(
                "Sent message {MessageId} to group {GroupName} via {MethodName}",
                message.MessageId, groupName, methodName);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _acknowledgmentService.CleanupConnectionAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}