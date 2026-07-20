using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Filters;
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
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class TasksController : AdminControllerBase
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
        [ProducesResponseType(typeof(TaskCleanupResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> CleanupOldTasks([FromQuery] int olderThanHours = 24)
        {
            olderThanHours = Math.Max(olderThanHours, 1); // Min 1 hour
            var count = await _taskService.CleanupOldTasksAsync(TimeSpan.FromHours(olderThanHours));

            LogAdminAudit("CleanedUp", "Tasks", null,
                $"Removed {count} tasks older than {olderThanHours} hours");

            return Ok(new TaskCleanupResponseDto { CleanedUp = count, OlderThanHours = olderThanHours });
        }

        /// <summary>Resolves a media task whose provider outcome required reconciliation.</summary>
        [HttpPost("{taskId}/resolve")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResolveIndeterminateTask(
            string taskId,
            [FromBody] ResolveIndeterminateTaskDto request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<IndeterminateTaskResolution>(
                    request.Resolution.Replace("_", string.Empty), true, out var resolution))
            {
                return BadRequest(new { message = "Resolution must be safe_to_retry, failed, or completed" });
            }

            var updated = await _taskService.ResolveIndeterminateTaskAsync(
                taskId, resolution, request.Reason, request.ProviderOperationId, cancellationToken);
            if (!updated)
            {
                return NotFound(new { message = "Indeterminate task was not found" });
            }

            LogAdminAudit("Resolved", "AsyncTask", detail:
                $"TaskId: {taskId}, Resolution: {resolution}, Reason: {request.Reason}");
            return NoContent();
        }
    }
}
