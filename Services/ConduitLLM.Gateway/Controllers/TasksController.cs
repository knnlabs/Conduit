using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Models;
using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// API controller for managing asynchronous tasks.
    /// </summary>
    [ApiController]
    [Route("v1/tasks")]
    [Authorize]
    public class TasksController : GatewayControllerBase
    {
        private readonly IAsyncTaskService _taskService;

        /// <summary>
        /// Initializes a new instance of the <see cref="TasksController"/> class.
        /// </summary>
        /// <param name="taskService">The async task service.</param>
        /// <param name="logger">The logger.</param>
        public TasksController(IAsyncTaskService taskService, ILogger<TasksController> logger)
            : base(logger)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        /// <summary>
        /// Gets the status of a specific task.
        /// </summary>
        /// <param name="taskId">The ID of the task to retrieve.</param>
        /// <returns>The task status.</returns>
        [HttpGet("{taskId}")]
        public async Task<IActionResult> GetTaskStatus(string taskId)
        {
            return await ExecuteAsync(async () =>
            {
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
            }, "GetTaskStatus", taskId);
        }

        /// <summary>
        /// Cancels a running task.
        /// </summary>
        /// <param name="taskId">The ID of the task to cancel.</param>
        /// <returns>No content on success.</returns>
        [HttpPost("{taskId}/cancel")]
        public async Task<IActionResult> CancelTask(string taskId)
        {
            return await ExecuteAsync(async () =>
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
            }, "CancelTask", taskId);
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
            return await ExecuteAsync(async () =>
            {
                // Validate and clamp parameters
                timeout = Math.Clamp(timeout, 1, 600); // Max 10 minutes
                interval = Math.Max(interval, 1); // Min 1 second

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
            }, "PollTask", taskId);
        }

    }
}
