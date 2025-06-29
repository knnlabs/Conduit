using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// API controller for managing asynchronous tasks.
    /// </summary>
    [ApiController]
    [Route("v1/tasks")]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly IAsyncTaskService _taskService;
        private readonly ILogger<TasksController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TasksController"/> class.
        /// </summary>
        /// <param name="taskService">The async task service.</param>
        /// <param name="logger">The logger.</param>
        public TasksController(IAsyncTaskService taskService, ILogger<TasksController> logger)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the status of a specific task.
        /// </summary>
        /// <param name="taskId">The ID of the task to retrieve.</param>
        /// <returns>The task status.</returns>
        [HttpGet("{taskId}")]
        public async Task<IActionResult> GetTaskStatus(string taskId)
        {
            try
            {
                var status = await _taskService.GetTaskStatusAsync(taskId);
                return Ok(status);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = new { message = ex.Message, type = "not_found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving task {TaskId}", taskId);
                return StatusCode(500, new { error = new { message = "An error occurred while retrieving the task", type = "server_error" } });
            }
        }

        /// <summary>
        /// Cancels a running task.
        /// </summary>
        /// <param name="taskId">The ID of the task to cancel.</param>
        /// <returns>No content on success.</returns>
        [HttpPost("{taskId}/cancel")]
        public async Task<IActionResult> CancelTask(string taskId)
        {
            try
            {
                await _taskService.CancelTaskAsync(taskId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = new { message = ex.Message, type = "not_found" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling task {TaskId}", taskId);
                return StatusCode(500, new { error = new { message = "An error occurred while cancelling the task", type = "server_error" } });
            }
        }

        /// <summary>
        /// Polls a task until it completes or times out.
        /// </summary>
        /// <param name="taskId">The ID of the task to poll.</param>
        /// <param name="timeout">Maximum time to wait in seconds (default: 300, max: 600).</param>
        /// <param name="interval">Polling interval in seconds (default: 2, min: 1).</param>
        /// <returns>The final task status.</returns>
        [HttpGet("{taskId}/poll")]
        public async Task<IActionResult> PollTask(string taskId, [FromQuery] int timeout = 300, [FromQuery] int interval = 2)
        {
            try
            {
                // Validate and clamp parameters
                timeout = Math.Clamp(timeout, 1, 600); // Max 10 minutes
                interval = Math.Max(interval, 1); // Min 1 second

                var status = await _taskService.PollTaskUntilCompletedAsync(
                    taskId,
                    TimeSpan.FromSeconds(interval),
                    TimeSpan.FromSeconds(timeout));

                return Ok(status);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = new { message = ex.Message, type = "not_found" } });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new { error = new { message = "Task polling timed out", type = "timeout" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling task {TaskId}", taskId);
                return StatusCode(500, new { error = new { message = "An error occurred while polling the task", type = "server_error" } });
            }
        }

        /// <summary>
        /// Cleans up old completed tasks.
        /// </summary>
        /// <param name="olderThanHours">Remove tasks older than this many hours (default: 24, min: 1).</param>
        /// <returns>The number of tasks cleaned up.</returns>
        [HttpPost("cleanup")]
        [Authorize(Policy = "MasterKey")] // Require admin access
        public async Task<IActionResult> CleanupOldTasks([FromQuery] int olderThanHours = 24)
        {
            try
            {
                olderThanHours = Math.Max(olderThanHours, 1); // Min 1 hour
                var count = await _taskService.CleanupOldTasksAsync(TimeSpan.FromHours(olderThanHours));
                
                return Ok(new { cleaned_up = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old tasks");
                return StatusCode(500, new { error = new { message = "An error occurred while cleaning up tasks", type = "server_error" } });
            }
        }
    }
}