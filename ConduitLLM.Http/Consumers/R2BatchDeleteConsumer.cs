using System.Net;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Handles actual R2 storage deletion with rate limiting for free tier.
    /// Implements safety mechanisms and retry logic for transient failures.
    /// </summary>
    public class R2BatchDeleteConsumer : IConsumer<R2BatchDeleteRequested>
    {
        private readonly IMediaStorageService _storageService;
        private readonly IMediaRecordRepository _mediaRepository;
        private readonly IConfigurationDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IDistributedLockService _lockService;
        private readonly MediaLifecycleOptions _options;
        private readonly ILogger<R2BatchDeleteConsumer> _logger;
        private static readonly SemaphoreSlim _rateLimiter = new(5, 5); // Max 5 concurrent operations

        public R2BatchDeleteConsumer(
            IMediaStorageService storageService,
            IMediaRecordRepository mediaRepository,
            IConfigurationDbContext context,
            IPublishEndpoint publishEndpoint,
            IDistributedLockService lockService,
            IOptions<MediaLifecycleOptions> options,
            ILogger<R2BatchDeleteConsumer> logger)
        {
            _storageService = storageService;
            _mediaRepository = mediaRepository;
            _context = context;
            _publishEndpoint = publishEndpoint;
            _lockService = lockService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<R2BatchDeleteRequested> context)
        {
            var message = context.Message;
            
            _logger.LogInformation(
                "Processing R2 delete batch {BatchId} for group {GroupId} with {Count} objects",
                message.BatchId, message.VirtualKeyGroupId, message.ObjectCount);

            // Check monthly budget for free tier
            var monthlyDeletes = await GetMonthlyDeleteCountAsync();
            if (monthlyDeletes >= _options.MonthlyDeleteBudget)
            {
                _logger.LogWarning(
                    "Monthly delete budget exceeded: {Count}/{Budget}. Deferring batch {BatchId}",
                    monthlyDeletes, _options.MonthlyDeleteBudget, message.BatchId);
                
                // Defer for 7 days (until next month)
                await context.Defer(TimeSpan.FromDays(7));
                return;
            }

            // Rate limiting for free tier
            await _rateLimiter.WaitAsync();
            try
            {
                // Distributed lock to prevent duplicate processing
                var lockKey = $"media:cleanup:{message.VirtualKeyGroupId}:{message.BatchId}";
                using var lockHandle = await _lockService.AcquireLockAsync(
                    lockKey, 
                    TimeSpan.FromMinutes(5),
                    context.CancellationToken);
                
                if (lockHandle == null)
                {
                    _logger.LogWarning(
                        "Could not acquire lock for batch {BatchId}. Another instance may be processing it.",
                        message.BatchId);
                    return;
                }

                await ProcessBatchDeletionAsync(message, context);
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        private async Task ProcessBatchDeletionAsync(
            R2BatchDeleteRequested message, 
            ConsumeContext<R2BatchDeleteRequested> context)
        {
            var successfulDeletes = new List<string>();
            var failedDeletes = new List<(string Key, string Error)>();
            long totalBytesFreed = 0;

            foreach (var storageKey in message.StorageKeys)
            {
                try
                {
                    if (_options.DryRunMode)
                    {
                        _logger.LogDebug(
                            "[DRY RUN] Would delete object: {Key} from bucket: {Bucket}",
                            storageKey, message.BucketName);
                        successfulDeletes.Add(storageKey);
                        
                        // Simulate size for dry run
                        var mediaRecord = await _context.MediaRecords
                            .FirstOrDefaultAsync(m => m.StorageKey == storageKey);
                        if (mediaRecord != null)
                        {
                            totalBytesFreed += mediaRecord.SizeBytes ?? 0;
                        }
                    }
                    else
                    {
                        // Get media record for size tracking
                        var mediaRecord = await _context.MediaRecords
                            .FirstOrDefaultAsync(m => m.StorageKey == storageKey);
                        
                        // Attempt deletion from R2
                        var deleted = await DeleteFromStorageAsync(storageKey);
                        
                        if (deleted)
                        {
                            successfulDeletes.Add(storageKey);
                            
                            if (mediaRecord != null)
                            {
                                totalBytesFreed += mediaRecord.SizeBytes ?? 0;
                                
                                // Delete from database
                                await _mediaRepository.DeleteAsync(mediaRecord.Id);
                                
                                _logger.LogDebug(
                                    "Deleted media record {Id} with storage key {Key}",
                                    mediaRecord.Id, storageKey);
                            }
                        }
                        else
                        {
                            failedDeletes.Add((storageKey, "Storage deletion failed"));
                        }
                    }

                    // Small delay between deletions to avoid rate limits
                    await Task.Delay(100);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning(
                        "R2 rate limit hit while deleting {Key}. Backing off.",
                        storageKey);
                    
                    // Put failed items back in the queue with delay
                    var remainingKeys = message.StorageKeys
                        .Skip(successfulDeletes.Count + failedDeletes.Count)
                        .ToList();
                    
                    if (remainingKeys.Any())
                    {
                        remainingKeys.Add(storageKey); // Add current key
                        
                        var retryBatch = new R2BatchDeleteRequested(
                            message.BucketName,
                            remainingKeys,
                            message.VirtualKeyGroupId,
                            message.BatchId);
                        
                        await context.Defer(TimeSpan.FromMinutes(5));
                    }
                    
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to delete object {Key} from bucket {Bucket}",
                        storageKey, message.BucketName);
                    
                    failedDeletes.Add((storageKey, ex.Message));
                }
            }

            // Publish success event if any deletions succeeded
            if (successfulDeletes.Any())
            {
                var deletedEvent = new MediaDeleted(
                    successfulDeletes,
                    message.VirtualKeyGroupId,
                    totalBytesFreed,
                    DateTime.UtcNow);

                await context.Publish(deletedEvent);
                
                _logger.LogInformation(
                    "Successfully deleted {Success}/{Total} objects, freed {Bytes:N0} bytes",
                    successfulDeletes.Count, 
                    message.ObjectCount, 
                    totalBytesFreed);

                // Update monthly counter
                await IncrementMonthlyDeleteCountAsync(successfulDeletes.Count);
            }

            // Handle partial failures
            if (failedDeletes.Any())
            {
                _logger.LogWarning(
                    "Failed to delete {Count} objects in batch {BatchId}",
                    failedDeletes.Count, message.BatchId);
                
                // TODO: Implement poison queue for persistent failures
                foreach (var (key, error) in failedDeletes.Take(5))
                {
                    _logger.LogError("Failed to delete {Key}: {Error}", key, error);
                }
            }
        }

        private async Task<bool> DeleteFromStorageAsync(string storageKey)
        {
            try
            {
                // Use a timeout for R2 operations
                using var cts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(_options.R2OperationTimeoutSeconds));
                
                await _storageService.DeleteAsync(storageKey);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "R2 delete operation timed out for key: {Key}",
                    storageKey);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error deleting object {Key} from storage",
                    storageKey);
                return false;
            }
        }

        private async Task<int> GetMonthlyDeleteCountAsync()
        {
            // TODO: Implement proper monthly counter using Redis or database
            // For now, return 0 to allow operations
            return await Task.FromResult(0);
        }

        private async Task IncrementMonthlyDeleteCountAsync(int count)
        {
            // TODO: Implement proper monthly counter increment
            // This should use Redis with expiry at end of month
            await Task.CompletedTask;
        }
    }
}