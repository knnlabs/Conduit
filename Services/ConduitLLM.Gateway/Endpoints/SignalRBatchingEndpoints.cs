using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.DTOs;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Controller for managing SignalR message batching
    /// </summary>
    public class SignalRBatchingEndpoints : GatewayEndpointHandlerBase
    {
        private readonly ISignalRMessageBatcher _messageBatcher;

        public SignalRBatchingEndpoints(
            ISignalRMessageBatcher messageBatcher,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SignalRBatchingEndpoints> logger)
            : base(null, httpContextAccessor, logger)
        {
            _messageBatcher = messageBatcher;
        }

        /// <summary>
        /// Gets current batching statistics
        /// </summary>
        public async Task<IResult> GetStatistics()
        {
            var stats = await _messageBatcher.GetStatisticsAsync();
            return Ok(stats);
        }

        /// <summary>
        /// Pauses message batching (messages sent immediately)
        /// </summary>
        public IResult PauseBatching()
        {
            _messageBatcher.PauseBatching();
            Logger.LogInformation("Message batching paused by admin");
            return Ok(new MessageResponse("Batching paused successfully"));
        }

        /// <summary>
        /// Resumes message batching
        /// </summary>
        public IResult ResumeBatching()
        {
            _messageBatcher.ResumeBatching();
            Logger.LogInformation("Message batching resumed by admin");
            return Ok(new MessageResponse("Batching resumed successfully"));
        }

        /// <summary>
        /// Forces immediate sending of all pending batches
        /// </summary>
        public async Task<IResult> FlushBatches()
        {
            await _messageBatcher.FlushAllBatchesAsync();
            Logger.LogInformation("All batches flushed by admin");
            return Ok(new MessageResponse("All batches flushed successfully"));
        }

        /// <summary>
        /// Gets batching efficiency metrics
        /// </summary>
        public async Task<IResult> GetEfficiencyMetrics()
        {
            var stats = await _messageBatcher.GetStatisticsAsync();
            
            return Ok(new BatchingEfficiencyResponse(
                stats.TotalMessagesBatched,
                stats.TotalBatchesSent,
                stats.AverageMessagesPerBatch,
                stats.NetworkCallsSaved,
                stats.BatchEfficiencyPercentage,
                stats.AverageBatchLatency.TotalMilliseconds,
                stats.IsBatchingEnabled));
        }
    }
}
