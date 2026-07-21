using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// API controller for managing asynchronous tasks.
    /// </summary>
    public class TasksEndpoints : GatewayEndpointHandlerBase
    {
        private readonly IAsyncTaskService _taskService;

        /// <summary>
        /// Initializes the Tasks endpoint handler.
        /// </summary>
        /// <param name="taskService">The async task service.</param>
        /// <param name="logger">The logger.</param>
        public TasksEndpoints(IAsyncTaskService taskService, IHttpContextAccessor httpContextAccessor, ILogger<TasksEndpoints> logger)
            : base(null, httpContextAccessor, logger)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        /// <summary>
        /// Gets the status of a specific task.
        /// </summary>
        /// <param name="taskId">The ID of the task to retrieve.</param>
        /// <returns>The task status.</returns>
        public async Task<IResult> GetTaskStatus(string taskId)
        {
            Logger.LogDebug("Getting status for task {TaskId}", taskId);
            try
            {
                var status = await _taskService.GetTaskStatusAsync(taskId);
                return Ok(status);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "not_found_error",
                        Code = "not_found"
                    }
                });
            }
        }

        /// <summary>
        /// Cancels a running task.
        /// </summary>
        /// <param name="taskId">The ID of the task to cancel.</param>
        /// <returns>No content on success.</returns>
        public async Task<IResult> CancelTask(string taskId)
        {
            try
            {
                await _taskService.CancelTaskAsync(taskId);
                Logger.LogInformation("Task {TaskId} cancelled successfully", taskId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "not_found_error",
                        Code = "not_found"
                    }
                });
            }
        }

        /// <summary>
        /// Polls a task until it completes or times out.
        /// </summary>
        /// <param name="taskId">The ID of the task to poll.</param>
        /// <param name="timeout">Maximum time to wait in seconds (default: 300, max: 600).</param>
        /// <param name="interval">Polling interval in seconds (default: 2, min: 1).</param>
        /// <returns>The final task status.</returns>
        public async Task<IResult> PollTask(string taskId, int timeout = 300, int interval = 2)
        {
            // Validate and clamp parameters
            timeout = Math.Clamp(timeout, 1, 600); // Max 10 minutes
            interval = Math.Max(interval, 1); // Min 1 second

            Logger.LogDebug("Polling task {TaskId} with timeout {TimeoutSeconds}s, interval {IntervalSeconds}s",
                taskId, timeout, interval);

            try
            {
                var status = await _taskService.PollTaskUntilCompletedAsync(
                    taskId,
                    TimeSpan.FromSeconds(interval),
                    TimeSpan.FromSeconds(timeout));

                return Ok(status);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "not_found_error",
                        Code = "not_found"
                    }
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Task polling timed out",
                        Type = "timeout",
                        Code = "timeout"
                    }
                });
            }
        }

    }
}
