using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Administrative API controller for managing asynchronous tasks.
    /// </summary>
    [ApiController]
    [Route("v1/admin/tasks")]
    [Authorize(Policy = "MasterKeyPolicy")]
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
        /// Cleans up old completed tasks system-wide.
        /// </summary>
        /// <param name="olderThanHours">Remove tasks older than this many hours (default: 24, min: 1).</param>
        /// <returns>The number of tasks cleaned up.</returns>
        /// <remarks>
        /// This is an administrative operation that affects all users' tasks.
        /// It archives completed tasks older than the specified threshold and
        /// permanently deletes archived tasks older than 30 days.
        /// </remarks>
        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupOldTasks([FromQuery] int olderThanHours = 24)
        {
            try
            {
                olderThanHours = Math.Max(olderThanHours, 1); // Min 1 hour
                var count = await _taskService.CleanupOldTasksAsync(TimeSpan.FromHours(olderThanHours));
                
                _logger.LogInformation("Admin cleaned up {Count} old tasks (older than {Hours} hours)", 
                    count, olderThanHours);
                
                return Ok(new { cleaned_up = count, older_than_hours = olderThanHours });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old tasks");
                return StatusCode(500, new { error = new { message = "An error occurred while cleaning up tasks", type = "server_error" } });
            }
        }
    }
}