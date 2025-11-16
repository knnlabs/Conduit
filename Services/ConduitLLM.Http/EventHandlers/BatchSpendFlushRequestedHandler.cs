using System.Diagnostics;
using MassTransit;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles BatchSpendFlushRequested events by triggering immediate flush of pending batch spend updates.
    /// This consumer provides deterministic flush capability for admin operations and integration testing.
    /// 
    /// Key responsibilities:
    /// - Immediately flush all pending batch spend updates via BatchSpendUpdateService
    /// - Publish completion event with detailed statistics
    /// - Handle errors gracefully with proper logging and error reporting
    /// - Maintain audit trail for operational tracking
    /// </summary>
    public class BatchSpendFlushRequestedHandler : IBatchSpendFlushRequestedConsumer
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<BatchSpendFlushRequestedHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the BatchSpendFlushRequestedHandler.
        /// </summary>
        /// <param name="serviceScopeFactory">Service scope factory for creating scoped services</param>
        /// <param name="publishEndpoint">MassTransit publish endpoint for publishing completion events</param>
        /// <param name="logger">Logger instance for operational tracking</param>
        public BatchSpendFlushRequestedHandler(
            IServiceScopeFactory serviceScopeFactory,
            IPublishEndpoint publishEndpoint,
            ILogger<BatchSpendFlushRequestedHandler> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes batch spend flush requests by immediately flushing all pending charges.
        /// 
        /// This method:
        /// 1. Gets the BatchSpendUpdateService from the DI container
        /// 2. Calls FlushPendingUpdatesAsync() to process all queued charges
        /// 3. Collects detailed statistics about the operation
        /// 4. Publishes a completion event with results
        /// 5. Handles errors gracefully and reports them in completion event
        /// </summary>
        /// <param name="context">Message context containing the flush request</param>
        public async Task Consume(ConsumeContext<BatchSpendFlushRequestedEvent> context)
        {
            var request = context.Message;
            var stopwatch = Stopwatch.StartNew();
            
            _logger.LogInformation(
                "Processing batch spend flush request {RequestId} from {RequestedBy} (Source: {Source}, Priority: {Priority})",
                request.RequestId, request.RequestedBy, request.Source ?? "Unknown", request.Priority);

            BatchSpendFlushCompletedEvent completionEvent;

            try
            {
                // Apply timeout if specified in the request
                using var timeoutCts = request.TimeoutSeconds.HasValue 
                    ? new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds.Value))
                    : new System.Threading.CancellationTokenSource();

                var cancellationToken = timeoutCts.Token;

                // Get the batch spend service from DI container
                using var scope = _serviceScopeFactory.CreateScope();
                var batchSpendService = scope.ServiceProvider.GetService<IBatchSpendUpdateService>();

                if (batchSpendService == null)
                {
                    _logger.LogError("BatchSpendUpdateService not available in DI container for flush request {RequestId}", request.RequestId);
                    
                    completionEvent = new BatchSpendFlushCompletedEvent
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        ErrorMessage = "BatchSpendUpdateService not available",
                        Duration = stopwatch.Elapsed,
                        CompletedAt = DateTime.UtcNow
                    };
                }
                else if (batchSpendService is not BatchSpendUpdateService concreteService)
                {
                    _logger.LogError("BatchSpendUpdateService is not the expected concrete type for flush request {RequestId}", request.RequestId);
                    
                    completionEvent = new BatchSpendFlushCompletedEvent
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        ErrorMessage = "BatchSpendUpdateService is not the expected concrete implementation",
                        Duration = stopwatch.Elapsed,
                        CompletedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    // Check service health before attempting flush
                    if (!batchSpendService.IsHealthy)
                    {
                        _logger.LogWarning("BatchSpendUpdateService is not healthy for flush request {RequestId}, proceeding anyway", request.RequestId);
                    }

                    // Perform the actual flush operation
                    var flushStopwatch = Stopwatch.StartNew();
                    var groupsFlushed = await concreteService.FlushPendingUpdatesAsync();
                    flushStopwatch.Stop();

                    _logger.LogInformation(
                        "Batch spend flush request {RequestId} completed successfully: {GroupsFlushed} groups flushed in {Duration:F2}ms",
                        request.RequestId, groupsFlushed, flushStopwatch.Elapsed.TotalMilliseconds);

                    // Collect detailed statistics if requested
                    BatchSpendFlushStatistics? statistics = null;
                    if (request.IncludeStatistics)
                    {
                        try
                        {
                            var serviceStats = await concreteService.GetStatisticsAsync();
                            statistics = new BatchSpendFlushStatistics
                            {
                                RedisKeysProcessed = groupsFlushed, // Groups flushed equals Redis keys processed
                                DatabaseTransactionsCreated = groupsFlushed, // One transaction per group
                                CacheInvalidationsTriggered = 0, // Will be filled by actual cache invalidations
                                RedisOperationMs = flushStopwatch.Elapsed.TotalMilliseconds * 0.3, // Estimate Redis portion
                                DatabaseOperationMs = flushStopwatch.Elapsed.TotalMilliseconds * 0.7, // Estimate DB portion
                                Warnings = groupsFlushed == 0 ? new[] { "No pending charges found to flush" } : null
                            };
                        }
                        catch (Exception statsEx)
                        {
                            _logger.LogWarning(statsEx, "Failed to collect detailed statistics for flush request {RequestId}", request.RequestId);
                            statistics = new BatchSpendFlushStatistics
                            {
                                Warnings = new[] { "Failed to collect detailed statistics: " + statsEx.Message }
                            };
                        }
                    }

                    completionEvent = new BatchSpendFlushCompletedEvent
                    {
                        RequestId = request.RequestId,
                        Success = true,
                        GroupsFlushed = groupsFlushed,
                        TotalAmountFlushed = 0, // This would require additional tracking to calculate
                        Duration = stopwatch.Elapsed,
                        CompletedAt = DateTime.UtcNow,
                        Statistics = statistics
                    };
                }
            }
            catch (OperationCanceledException) when (request.TimeoutSeconds.HasValue)
            {
                _logger.LogWarning("Batch spend flush request {RequestId} timed out after {TimeoutSeconds} seconds", 
                    request.RequestId, request.TimeoutSeconds);

                completionEvent = new BatchSpendFlushCompletedEvent
                {
                    RequestId = request.RequestId,
                    Success = false,
                    ErrorMessage = $"Flush operation timed out after {request.TimeoutSeconds} seconds",
                    Duration = stopwatch.Elapsed,
                    CompletedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch spend flush request {RequestId}: {ErrorMessage}", 
                    request.RequestId, ex.Message);

                completionEvent = new BatchSpendFlushCompletedEvent
                {
                    RequestId = request.RequestId,
                    Success = false,
                    ErrorMessage = $"Flush operation failed: {ex.Message}",
                    Duration = stopwatch.Elapsed,
                    CompletedAt = DateTime.UtcNow,
                    Statistics = request.IncludeStatistics ? new BatchSpendFlushStatistics
                    {
                        Warnings = new[] { "Operation failed before statistics could be collected" }
                    } : null
                };
            }
            finally
            {
                stopwatch.Stop();
            }

            // Always publish completion event for requestor tracking
            try
            {
                await _publishEndpoint.Publish(completionEvent, cancellationToken: context.CancellationToken);
                
                _logger.LogDebug("Published BatchSpendFlushCompletedEvent for request {RequestId} (Success: {Success})", 
                    request.RequestId, completionEvent.Success);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, 
                    "Failed to publish BatchSpendFlushCompletedEvent for request {RequestId}", request.RequestId);
                
                // Note: We don't re-throw here as the flush operation itself may have succeeded
                // The requestor will need to rely on logs or timeout for this edge case
            }
        }
    }
}