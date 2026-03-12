using ConduitLLM.Core.Controllers;
using ConduitLLM.Gateway.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// Controller for managing SignalR message batching
    /// </summary>
    [ApiController]
    [Route("api/signalr/batching")]
    [Authorize(Policy = "AdminOnly")]
    public class SignalRBatchingController : GatewayControllerBase
    {
        private readonly ISignalRMessageBatcher _messageBatcher;

        public SignalRBatchingController(
            ISignalRMessageBatcher messageBatcher,
            ILogger<SignalRBatchingController> logger)
            : base(logger)
        {
            _messageBatcher = messageBatcher;
        }

        /// <summary>
        /// Gets current batching statistics
        /// </summary>
        [HttpGet("statistics")]
        [AllowAnonymous]
        public async Task<ActionResult<BatchingStatistics>> GetStatistics()
        {
            var stats = await _messageBatcher.GetStatisticsAsync();
            return Ok(stats);
        }

        /// <summary>
        /// Pauses message batching (messages sent immediately)
        /// </summary>
        [HttpPost("pause")]
        public ActionResult PauseBatching()
        {
            _messageBatcher.PauseBatching();
            Logger.LogInformation("Message batching paused by admin");
            return Ok(new { message = "Batching paused successfully" });
        }

        /// <summary>
        /// Resumes message batching
        /// </summary>
        [HttpPost("resume")]
        public ActionResult ResumeBatching()
        {
            _messageBatcher.ResumeBatching();
            Logger.LogInformation("Message batching resumed by admin");
            return Ok(new { message = "Batching resumed successfully" });
        }

        /// <summary>
        /// Forces immediate sending of all pending batches
        /// </summary>
        [HttpPost("flush")]
        public async Task<ActionResult> FlushBatches()
        {
            await _messageBatcher.FlushAllBatchesAsync();
            Logger.LogInformation("All batches flushed by admin");
            return Ok(new { message = "All batches flushed successfully" });
        }

        /// <summary>
        /// Gets batching efficiency metrics
        /// </summary>
        [HttpGet("efficiency")]
        [AllowAnonymous]
        public async Task<ActionResult> GetEfficiencyMetrics()
        {
            var stats = await _messageBatcher.GetStatisticsAsync();
            
            return Ok(new
            {
                totalMessagesBatched = stats.TotalMessagesBatched,
                totalBatchesSent = stats.TotalBatchesSent,
                averageMessagesPerBatch = stats.AverageMessagesPerBatch,
                networkCallsSaved = stats.NetworkCallsSaved,
                batchEfficiencyPercentage = stats.BatchEfficiencyPercentage,
                averageBatchLatency = stats.AverageBatchLatency.TotalMilliseconds,
                isBatchingEnabled = stats.IsBatchingEnabled
            });
        }
    }
}
