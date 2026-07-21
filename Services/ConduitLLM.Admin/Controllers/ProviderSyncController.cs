using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Models.ProviderSync;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Admin API for reviewing and applying OpenRouter metadata drift, and triggering the sync.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class ProviderSyncController : AdminControllerBase
    {
        private readonly IAdminProviderSyncService _syncService;
        private readonly IOpenRouterDriftDetectionService _detectionService;
        private readonly IDistributedLockService _lockService;

        private const string SyncLockKey = "openrouter:metadata-sync:leader";

        public ProviderSyncController(
            IAdminProviderSyncService syncService,
            IOpenRouterDriftDetectionService detectionService,
            IDistributedLockService lockService,
            ILogger<ProviderSyncController> logger)
            : base(logger)
        {
            _syncService = syncService;
            _detectionService = detectionService;
            _lockService = lockService;
        }

        /// <summary>Lists drift items (defaults to Pending), optionally filtered.</summary>
        [HttpGet("drift")]
        [ProducesResponseType(typeof(List<DriftItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDrift(
            [FromQuery] string? status,
            [FromQuery] string? driftType,
            [FromQuery] int? providerId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);
            var items = await _syncService.GetDriftItemsAsync(status, driftType, providerId, page, pageSize);
            return Ok(items);
        }

        /// <summary>Gets a single drift item.</summary>
        [HttpGet("drift/{id}")]
        [ProducesResponseType(typeof(DriftItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDriftItem(int id)
        {
            var item = await _syncService.GetDriftItemAsync(id);
            if (item == null)
                return this.NotFoundEntity("Drift item", id);
            return Ok(item);
        }

        /// <summary>Applies a drift item's proposed change.</summary>
        [HttpPost("drift/{id}/apply")]
        [ProducesResponseType(typeof(DriftActionResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Apply(int id)
        {
            var result = await _syncService.ApplyAsync(id, CurrentActor());
            LogAdminAudit("AppliedDrift", "ProviderMetadataDriftItem", id, result.Success ? "applied" : result.Error);
            return Ok(result);
        }

        /// <summary>Dismisses a drift item.</summary>
        [HttpPost("drift/{id}/dismiss")]
        [ProducesResponseType(typeof(DriftActionResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Dismiss(int id)
        {
            var result = await _syncService.DismissAsync(id, CurrentActor());
            LogAdminAudit("DismissedDrift", "ProviderMetadataDriftItem", id);
            return Ok(result);
        }

        /// <summary>Applies multiple drift items.</summary>
        [HttpPost("drift/bulk/apply")]
        [ProducesResponseType(typeof(BulkDriftActionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ApplyBulk([FromBody] BulkDriftActionRequest request)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
                return BadRequest(new ErrorResponseDto("No drift item ids provided."));
            var result = await _syncService.ApplyBulkAsync(request.Ids, CurrentActor());
            LogAdminAuditBulk("BulkAppliedDrift", "ProviderMetadataDriftItem", result.SucceededCount, result.FailedCount);
            return Ok(result);
        }

        /// <summary>Dismisses multiple drift items.</summary>
        [HttpPost("drift/bulk/dismiss")]
        [ProducesResponseType(typeof(BulkDriftActionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DismissBulk([FromBody] BulkDriftActionRequest request)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
                return BadRequest(new ErrorResponseDto("No drift item ids provided."));
            var result = await _syncService.DismissBulkAsync(request.Ids, CurrentActor());
            LogAdminAuditBulk("BulkDismissedDrift", "ProviderMetadataDriftItem", result.SucceededCount, result.FailedCount);
            return Ok(result);
        }

        /// <summary>Triggers a sync now. Returns 409 if a sync is already in progress.</summary>
        [HttpPost("run")]
        [ProducesResponseType(typeof(ProviderSyncRunDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RunNow()
        {
            using var lockHandle = await _lockService.AcquireLockAsync(SyncLockKey, TimeSpan.FromMinutes(15));
            if (lockHandle == null)
                return Conflict(new ErrorResponseDto("A sync is already in progress."));

            var run = await _detectionService.RunSyncAsync("Manual");
            LogAdminAudit("RanSync", "ProviderMetadataSyncRun", run.Id, $"Status: {run.Status}");
            return Ok(run);
        }

        /// <summary>Lists recent sync runs.</summary>
        [HttpGet("runs")]
        [ProducesResponseType(typeof(List<ProviderSyncRunDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRuns([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var runs = await _syncService.GetSyncRunsAsync(page, pageSize);
            return Ok(runs);
        }

        private string CurrentActor() => User?.Identity?.Name ?? "admin";
    }
}
