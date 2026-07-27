using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Policies;

namespace ConduitLLM.Core.Services.BatchOperations
{
    /// <summary>
    /// Abstract base class for batch operations providing common functionality:
    /// - Idempotency tracking to prevent duplicate processing
    /// - Retry logic with exponential backoff
    /// - Standardized error handling and logging
    /// - Progress reporting and metrics collection
    /// - Cancellation support
    /// </summary>
    /// <typeparam name="TItem">Type of items being processed in the batch</typeparam>
    public abstract class BatchOperationBase<TItem>
    {
        protected readonly ILogger Logger;
        protected readonly IBatchOperationService BatchOperationService;
        protected readonly IBatchOperationIdempotencyService? IdempotencyService;

        /// <summary>
        /// Options for retry behavior
        /// </summary>
        protected virtual BatchRetryOptions RetryOptions => new()
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            BackoffMultiplier = 2.0
        };

        /// <summary>
        /// Initializes the batch operation base class
        /// </summary>
        protected BatchOperationBase(
            ILogger logger,
            IBatchOperationService batchOperationService,
            IBatchOperationIdempotencyService? idempotencyService = null)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            BatchOperationService = batchOperationService ?? throw new ArgumentNullException(nameof(batchOperationService));
            IdempotencyService = idempotencyService;
        }

        /// <summary>
        /// Executes the batch operation with idempotency checking, retry logic, and progress tracking
        /// </summary>
        /// <param name="items">Items to process</param>
        /// <param name="virtualKeyId">Virtual key ID for authorization</param>
        /// <param name="idempotencyToken">Optional token to prevent duplicate processing</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the batch operation</returns>
        public async Task<BatchOperationResult> ExecuteAsync(
            List<TItem> items,
            int virtualKeyId,
            string? idempotencyToken = null,
            CancellationToken cancellationToken = default)
        {
            var operationType = GetOperationType();

            // Generate idempotency token if not provided
            if (string.IsNullOrWhiteSpace(idempotencyToken) && IdempotencyService != null)
            {
                idempotencyToken = IdempotencyService.GenerateToken(
                    operationType,
                    virtualKeyId,
                    items);
            }

            // Check for duplicate operation
            if (!string.IsNullOrWhiteSpace(idempotencyToken) && IdempotencyService != null)
            {
                var isDuplicate = await IdempotencyService.IsOperationProcessedAsync(
                    idempotencyToken,
                    cancellationToken);

                if (isDuplicate)
                {
                    Logger.LogInformation(
                        "Duplicate batch operation detected. Token: {Token}, Type: {OperationType}",
                        idempotencyToken,
                        operationType);

                    // Return cached result if available
                    var cachedResult = await IdempotencyService.GetOperationResultAsync<BatchOperationResult>(
                        idempotencyToken,
                        cancellationToken);

                    if (cachedResult != null)
                    {
                        Logger.LogInformation(
                            "Returning cached result for idempotency token {Token}",
                            idempotencyToken);
                        return cachedResult;
                    }
                }
            }

            // Validate items before processing
            await ValidateBatchAsync(items, cancellationToken);

            // Configure batch operation options
            var options = ConfigureBatchOptions(virtualKeyId);

            try
            {
                // Execute the batch operation
                var result = await BatchOperationService.StartBatchOperationAsync(
                    operationType,
                    items,
                    ProcessItemWithRetryAsync,
                    options,
                    cancellationToken);

                // Store result for idempotency
                if (!string.IsNullOrWhiteSpace(idempotencyToken) && IdempotencyService != null)
                {
                    await IdempotencyService.StoreOperationResultAsync(
                        idempotencyToken,
                        result,
                        GetIdempotencyTtl(),
                        cancellationToken);
                }

                // Log operation summary
                LogOperationSummary(result);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Batch operation {OperationType} failed catastrophically",
                    operationType);
                throw;
            }
        }

        /// <summary>
        /// Processes a single item with retry logic and error handling
        /// </summary>
        private async Task<BatchItemResult> ProcessItemWithRetryAsync(
            TItem item,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var itemIdentifier = GetItemIdentifier(item);
            var attempt = 0;
            Exception? lastException = null;

            while (attempt < RetryOptions.MaxRetries)
            {
                attempt++;

                try
                {
                    // Validate the individual item
                    await ValidateItemAsync(item, cancellationToken);

                    // Process the item (implemented by derived class)
                    var result = await ProcessItemAsync(item, cancellationToken);

                    // Add duration if not set
                    if (result.Duration == null || result.Duration == TimeSpan.Zero)
                    {
                        result.Duration = stopwatch.Elapsed;
                    }

                    // Add identifier if not set
                    if (string.IsNullOrWhiteSpace(result.ItemIdentifier))
                    {
                        result.ItemIdentifier = itemIdentifier;
                    }

                    return result;
                }
                catch (Exception ex) when (attempt < RetryOptions.MaxRetries &&
                                           IsRetryableException(ex, cancellationToken))
                {
                    lastException = ex;
                    var delay = CalculateRetryDelay(attempt);

                    Logger.LogWarning(ex,
                        "Retryable error processing item {ItemIdentifier}. Attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms",
                        itemIdentifier,
                        attempt,
                        RetryOptions.MaxRetries,
                        delay.TotalMilliseconds);

                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex,
                        "Failed to process item {ItemIdentifier} on attempt {Attempt}",
                        itemIdentifier,
                        attempt);

                    return new BatchItemResult
                    {
                        Success = false,
                        ItemIdentifier = itemIdentifier,
                        Error = ex.Message,
                        Duration = stopwatch.Elapsed
                    };
                }
            }

            // All retries exhausted
            Logger.LogError(lastException,
                "Failed to process item {ItemIdentifier} after {MaxRetries} attempts",
                itemIdentifier,
                RetryOptions.MaxRetries);

            return new BatchItemResult
            {
                Success = false,
                ItemIdentifier = itemIdentifier,
                Error = $"Failed after {RetryOptions.MaxRetries} retries: {lastException?.Message}",
                Duration = stopwatch.Elapsed
            };
        }

        /// <summary>
        /// Calculates retry delay using exponential backoff
        /// </summary>
        private TimeSpan CalculateRetryDelay(int attempt)
        {
            var delay = RetryOptions.InitialDelay * Math.Pow(RetryOptions.BackoffMultiplier, attempt - 1);
            var maxDelay = RetryOptions.MaxDelay.TotalMilliseconds;
            return TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, maxDelay));
        }

        /// <summary>
        /// Logs a summary of the batch operation results
        /// </summary>
        private void LogOperationSummary(BatchOperationResult result)
        {
            Logger.LogInformation(
                "Batch operation {OperationType} completed: {Status} | " +
                "Total: {Total}, Success: {Success}, Failed: {Failed} | " +
                "Duration: {Duration:mm\\:ss}, Rate: {Rate:F1} items/sec",
                result.OperationType,
                result.Status,
                result.TotalItems,
                result.SuccessCount,
                result.FailedCount,
                result.Duration,
                result.ItemsPerSecond);

            if (result.FailedCount > 0)
            {
                Logger.LogWarning(
                    "Batch operation {OperationType} had {FailedCount} failures. " +
                    "First error: {FirstError}",
                    result.OperationType,
                    result.FailedCount,
                    result.Errors.FirstOrDefault()?.Error ?? "Unknown");
            }
        }

        // Abstract methods to be implemented by derived classes

        /// <summary>
        /// Returns the type identifier for this batch operation (e.g., "spend_update")
        /// </summary>
        protected abstract string GetOperationType();

        /// <summary>
        /// Validates the entire batch before processing begins.
        /// Throw an exception if the batch is invalid.
        /// </summary>
        /// <param name="items">Items to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected abstract Task ValidateBatchAsync(List<TItem> items, CancellationToken cancellationToken);

        /// <summary>
        /// Validates a single item before processing.
        /// Throw an exception if the item is invalid.
        /// </summary>
        /// <param name="item">Item to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected abstract Task ValidateItemAsync(TItem item, CancellationToken cancellationToken);

        /// <summary>
        /// Processes a single item.
        /// This is the core business logic implemented by derived classes.
        /// </summary>
        /// <param name="item">Item to process</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of processing the item</returns>
        protected abstract Task<BatchItemResult> ProcessItemAsync(TItem item, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a unique identifier for an item (for logging and error reporting)
        /// </summary>
        /// <param name="item">Item to identify</param>
        /// <returns>String identifier</returns>
        protected abstract string GetItemIdentifier(TItem item);

        // Virtual methods with default implementations that can be overridden

        /// <summary>
        /// Configures batch operation options.
        /// Override to customize parallelism, checkpointing, etc.
        /// </summary>
        protected virtual BatchOperationOptions ConfigureBatchOptions(int virtualKeyId)
        {
            return new BatchOperationOptions
            {
                VirtualKeyId = virtualKeyId,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                ContinueOnError = true,
                EnableCheckpointing = true,
                CheckpointInterval = 100,
                Metadata = new Dictionary<string, object>
                {
                    ["operationType"] = GetOperationType(),
                    ["framework"] = "BatchOperationBase"
                }
            };
        }

        /// <summary>
        /// Determines if an exception is retryable.
        /// Override to customize retry logic for specific exception types.
        /// </summary>
        protected virtual bool IsRetryableException(
            Exception exception,
            CancellationToken callerToken) =>
            TransientErrorPolicy.IsTransient(exception, callerToken);

        /// <summary>
        /// Gets the TTL for idempotency token storage.
        /// Override to customize cache duration.
        /// </summary>
        protected virtual TimeSpan GetIdempotencyTtl()
        {
            return TimeSpan.FromHours(24);
        }
    }

    /// <summary>
    /// Options for retry behavior in batch operations
    /// </summary>
    public class BatchRetryOptions
    {
        /// <summary>
        /// Maximum number of retry attempts
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Initial delay before first retry
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Maximum delay between retries
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Multiplier for exponential backoff
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;
    }
}
