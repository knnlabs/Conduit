using MassTransit;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.Options;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Processes media cleanup batches and publishes R2 delete events.
    /// Acts as an intermediary to prepare batches for storage deletion.
    /// </summary>
    public class MediaCleanupBatchConsumer : IConsumer<MediaCleanupBatchRequested>
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly MediaLifecycleOptions _options;
        private readonly ILogger<MediaCleanupBatchConsumer> _logger;
        private readonly IConfiguration _configuration;

        public MediaCleanupBatchConsumer(
            IPublishEndpoint publishEndpoint,
            IOptions<MediaLifecycleOptions> options,
            ILogger<MediaCleanupBatchConsumer> logger,
            IConfiguration configuration)
        {
            _publishEndpoint = publishEndpoint;
            _options = options.Value;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task Consume(ConsumeContext<MediaCleanupBatchRequested> context)
        {
            var message = context.Message;
            
            _logger.LogInformation(
                "Processing cleanup batch {BatchId} for group {GroupId} with {Count} items, Reason: {Reason}",
                message.BatchId, message.VirtualKeyGroupId, message.BatchSize, message.CleanupReason);

            try
            {
                // Get R2 bucket configuration
                var bucketName = _configuration["ConduitLLM:Storage:S3:BucketName"] ?? "conduit-media";

                // Validate storage keys
                var validKeys = message.StorageKeys
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct()
                    .ToList();

                if (validKeys.Count != message.StorageKeys.Count)
                {
                    _logger.LogWarning(
                        "Batch {BatchId} contained {Invalid} invalid storage keys out of {Total}",
                        message.BatchId, 
                        message.StorageKeys.Count - validKeys.Count,
                        message.StorageKeys.Count);
                }

                if (!validKeys.Any())
                {
                    _logger.LogWarning(
                        "Batch {BatchId} contained no valid storage keys",
                        message.BatchId);
                    return;
                }

                // Create R2 delete request
                var r2DeleteRequest = new R2BatchDeleteRequested(
                    bucketName,
                    validKeys,
                    message.VirtualKeyGroupId,
                    message.BatchId);

                if (_options.DryRunMode)
                {
                    _logger.LogInformation(
                        "[DRY RUN] Would publish R2 delete request for {Count} objects in bucket {Bucket}",
                        validKeys.Count, bucketName);
                    
                    // Log sample of keys that would be deleted
                    var sampleKeys = validKeys.Take(5);
                    foreach (var key in sampleKeys)
                    {
                        _logger.LogDebug("[DRY RUN] Would delete: {Key}", key);
                    }
                    if (validKeys.Count > 5)
                    {
                        _logger.LogDebug("[DRY RUN] ... and {Count} more", validKeys.Count - 5);
                    }
                }
                else
                {
                    await _publishEndpoint.Publish(r2DeleteRequest);
                    
                    _logger.LogInformation(
                        "Published R2 delete request for {Count} objects in bucket {Bucket}",
                        validKeys.Count, bucketName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing cleanup batch {BatchId} for group {GroupId}",
                    message.BatchId, message.VirtualKeyGroupId);
                throw;
            }
        }
    }
}