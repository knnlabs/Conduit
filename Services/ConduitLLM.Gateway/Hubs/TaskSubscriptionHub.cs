using Microsoft.AspNetCore.SignalR;

namespace ConduitLLM.Gateway.Hubs
{
    /// <summary>
    /// Base class for media generation hubs that support task subscription.
    /// Provides standardized subscribe/unsubscribe with ownership validation.
    /// </summary>
    public abstract class TaskSubscriptionHub : SecureHub
    {
        protected TaskSubscriptionHub(
            ILogger logger,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
        }

        /// <summary>
        /// Gets the SignalR group name for a given task ID.
        /// </summary>
        protected abstract string GetTaskGroupName(string taskId);

        /// <summary>
        /// Subscribe to updates for a specific generation task.
        /// Validates virtual key ownership before subscribing.
        /// </summary>
        public async Task SubscribeToTask(string taskId)
        {
            var virtualKeyId = RequireVirtualKeyId();
            var groupName = GetTaskGroupName(taskId);

            if (!await CanAccessTaskAsync(taskId))
            {
                Logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to unauthorized task {TaskId}",
                    virtualKeyId, taskId);
                throw new HubException("Unauthorized access to task");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Logger.LogInformation(
                "Virtual Key {KeyId} subscribed to {HubName} task {TaskId} in group {GroupName}, ConnectionId: {ConnectionId}",
                virtualKeyId, GetHubName(), taskId, groupName, Context.ConnectionId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific generation task.
        /// </summary>
        public async Task UnsubscribeFromTask(string taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetTaskGroupName(taskId));
            Logger.LogDebug("Client {ConnectionId} unsubscribed from {HubName} task {TaskId}",
                Context.ConnectionId, GetHubName(), taskId);
        }
    }
}
