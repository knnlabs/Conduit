using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time video generation progress updates.
    /// Enables WebSocket-based communication for tracking video generation status.
    /// </summary>
    [Authorize]
    public class VideoGenerationHub : Hub
    {
        private readonly IAsyncTaskService _taskService;
        private readonly ILogger<VideoGenerationHub> _logger;

        public VideoGenerationHub(
            IAsyncTaskService taskService,
            ILogger<VideoGenerationHub> logger)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client {ConnectionId} connected to VideoGenerationHub", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client {ConnectionId} disconnected from VideoGenerationHub", Context.ConnectionId);
            
            // Remove from all task groups
            var taskGroups = Context.Items.Values.OfType<string>()
                .Where(v => v.StartsWith("task-"))
                .ToList();
                
            foreach (var group in taskGroups)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribe to real-time updates for a specific video generation task.
        /// </summary>
        /// <param name="taskId">The task ID to subscribe to.</param>
        public async Task SubscribeToTask(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
            }

            // Validate that the task exists and belongs to the authenticated user
            var task = await _taskService.GetTaskStatusAsync(taskId);
            if (task == null)
            {
                await Clients.Caller.SendAsync("Error", new
                {
                    message = $"Task {taskId} not found"
                });
                return;
            }

            // TODO: Validate that the task belongs to the current user's virtual key
            // This would require extracting virtual key from Context.User

            var groupName = $"task-{taskId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            
            // Track subscription in connection items
            Context.Items[taskId] = groupName;
            
            _logger.LogInformation("Client {ConnectionId} subscribed to task {TaskId}", 
                Context.ConnectionId, taskId);

            // Send current task status immediately
            await Clients.Caller.SendAsync("TaskStatus", new
            {
                taskId = task.TaskId,
                status = task.State.ToString().ToLowerInvariant(),
                progress = task.Progress,
                message = task.ProgressMessage,
                error = task.Error,
                createdAt = task.CreatedAt,
                updatedAt = task.UpdatedAt,
                completedAt = task.CompletedAt,
                result = task.Result
            });
        }

        /// <summary>
        /// Unsubscribe from updates for a specific video generation task.
        /// </summary>
        /// <param name="taskId">The task ID to unsubscribe from.</param>
        public async Task UnsubscribeFromTask(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
            }

            var groupName = $"task-{taskId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            
            // Remove from connection items
            Context.Items.Remove(taskId);
            
            _logger.LogInformation("Client {ConnectionId} unsubscribed from task {TaskId}", 
                Context.ConnectionId, taskId);
        }

        /// <summary>
        /// Subscribe to all tasks for the current user's virtual key.
        /// </summary>
        public async Task SubscribeToMyTasks()
        {
            // TODO: Extract virtual key from Context.User and subscribe to all active tasks
            // This would require querying tasks by virtual key
            
            await Clients.Caller.SendAsync("Subscribed", new
            {
                message = "Subscribed to all your active video generation tasks"
            });
        }

        /// <summary>
        /// Get the current status of a video generation task.
        /// </summary>
        /// <param name="taskId">The task ID to check.</param>
        public async Task GetTaskStatus(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                await Clients.Caller.SendAsync("Error", new
                {
                    message = "Task ID cannot be empty"
                });
                return;
            }

            var task = await _taskService.GetTaskStatusAsync(taskId);
            if (task == null)
            {
                await Clients.Caller.SendAsync("Error", new
                {
                    message = $"Task {taskId} not found"
                });
                return;
            }

            await Clients.Caller.SendAsync("TaskStatus", new
            {
                taskId = task.TaskId,
                status = task.State.ToString().ToLowerInvariant(),
                progress = task.Progress,
                message = task.ProgressMessage,
                error = task.Error,
                createdAt = task.CreatedAt,
                updatedAt = task.UpdatedAt,
                completedAt = task.CompletedAt,
                result = task.Result
            });
        }
    }
}