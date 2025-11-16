using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ConduitLLM.Core.Metrics
{
    /// <summary>
    /// OpenTelemetry metrics for media generation orchestrators
    /// Provides comprehensive monitoring of performance, success rates, and resource utilization
    /// </summary>
    public class MediaGenerationMetrics : IDisposable
    {
        private readonly Meter _meter;
        
        // Performance Metrics
        public Histogram<double> GenerationDuration { get; }
        public UpDownCounter<long> QueueDepth { get; }
        public Counter<long> GenerationThroughput { get; }
        public Histogram<double> LatencyPercentiles { get; }
        
        // Success/Failure Metrics  
        public Counter<long> GenerationsCompleted { get; }
        public Counter<long> GenerationsFailed { get; }
        public Counter<long> GenerationsRetried { get; }
        public Counter<long> GenerationsCancelled { get; }
        
        // Provider Metrics
        public Histogram<double> ProviderResponseTime { get; }
        public Counter<long> ProviderErrors { get; }
        public Counter<long> ProviderRequests { get; }
        public UpDownCounter<long> ProviderAvailability { get; }
        
        // Resource Utilization Metrics
        public UpDownCounter<long> ActiveGenerations { get; }
        public UpDownCounter<long> TaskRegistrySize { get; }
        public Histogram<double> StorageOperationDuration { get; }
        public Counter<long> StorageOperations { get; }
        
        // Cost Tracking
        public Histogram<double> GenerationCost { get; }
        public Counter<long> CostCalculations { get; }

        // Activity source for distributed tracing
        public static readonly ActivitySource ActivitySource = new("ConduitLLM.MediaGeneration", "1.0.0");

        public MediaGenerationMetrics(IMeterFactory meterFactory)
        {
            _meter = meterFactory.Create("ConduitLLM.MediaGeneration", "1.0.0");

            // Performance Metrics
            GenerationDuration = _meter.CreateHistogram<double>(
                "media.generation.duration",
                "seconds",
                "Duration of media generation from request to completion");

            QueueDepth = _meter.CreateUpDownCounter<long>(
                "media.generation.queue.depth",
                "tasks",
                "Number of pending generation tasks in queue");

            GenerationThroughput = _meter.CreateCounter<long>(
                "media.generation.throughput",
                "generations",
                "Total number of generations processed");

            LatencyPercentiles = _meter.CreateHistogram<double>(
                "media.generation.latency",
                "milliseconds",
                "End-to-end generation latency for percentile calculations");

            // Success/Failure Metrics
            GenerationsCompleted = _meter.CreateCounter<long>(
                "media.generation.completed",
                "completions",
                "Number of successful generations");

            GenerationsFailed = _meter.CreateCounter<long>(
                "media.generation.failed",
                "failures",
                "Number of failed generations categorized by error type");

            GenerationsRetried = _meter.CreateCounter<long>(
                "media.generation.retried",
                "retries",
                "Number of generation retry attempts");

            GenerationsCancelled = _meter.CreateCounter<long>(
                "media.generation.cancelled",
                "cancellations",
                "Number of cancelled generations categorized by cancellation source");

            // Provider Metrics
            ProviderResponseTime = _meter.CreateHistogram<double>(
                "media.generation.provider.response_time",
                "seconds",
                "Response time from provider API calls");

            ProviderErrors = _meter.CreateCounter<long>(
                "media.generation.provider.errors",
                "errors",
                "Number of provider-specific errors");

            ProviderRequests = _meter.CreateCounter<long>(
                "media.generation.provider.requests",
                "requests",
                "Total number of requests sent to providers");

            ProviderAvailability = _meter.CreateUpDownCounter<long>(
                "media.generation.provider.availability",
                "status",
                "Provider availability status (1 = available, 0 = unavailable)");

            // Resource Utilization Metrics
            ActiveGenerations = _meter.CreateUpDownCounter<long>(
                "media.generation.active",
                "generations",
                "Number of currently active generations");

            TaskRegistrySize = _meter.CreateUpDownCounter<long>(
                "media.generation.task_registry.size",
                "tasks",
                "Number of tasks registered in the task registry");

            StorageOperationDuration = _meter.CreateHistogram<double>(
                "media.generation.storage.duration",
                "milliseconds",
                "Duration of media storage operations");

            StorageOperations = _meter.CreateCounter<long>(
                "media.generation.storage.operations",
                "operations",
                "Number of storage operations performed");

            // Cost Tracking
            GenerationCost = _meter.CreateHistogram<double>(
                "media.generation.cost",
                "dollars",
                "Cost per generation in dollars");

            CostCalculations = _meter.CreateCounter<long>(
                "media.generation.cost.calculations",
                "calculations",
                "Number of cost calculation operations");
        }

        /// <summary>
        /// Records the start of a generation process
        /// </summary>
        public void RecordGenerationStarted(string mediaType, string model, string provider, string virtualKeyId)
        {
            var tags = new TagList
            {
                { "media_type", mediaType.ToLowerInvariant() },
                { "model", model },
                { "provider", provider },
                { "virtual_key_id", virtualKeyId }
            };

            GenerationThroughput.Add(1, tags);
            ActiveGenerations.Add(1, tags);
            ProviderRequests.Add(1, tags);
        }

        /// <summary>
        /// Records a completed generation with comprehensive metrics
        /// </summary>
        public void RecordGenerationCompleted(string mediaType, string model, string provider, 
            string virtualKeyId, double durationSeconds, double cost, bool hasRetries = false, int retryCount = 0)
        {
            var tags = new TagList
            {
                { "media_type", mediaType.ToLowerInvariant() },
                { "model", model },
                { "provider", provider },
                { "virtual_key_id", virtualKeyId },
                { "has_retries", hasRetries.ToString().ToLowerInvariant() }
            };

            GenerationsCompleted.Add(1, tags);
            GenerationDuration.Record(durationSeconds, tags);
            LatencyPercentiles.Record(durationSeconds * 1000, tags); // Convert to ms for latency
            ActiveGenerations.Add(-1, tags);

            if (cost > 0)
            {
                GenerationCost.Record(cost, tags);
                CostCalculations.Add(1, tags);
            }

            if (hasRetries && retryCount > 0)
            {
                var retryTags = new TagList
                {
                    { "media_type", mediaType.ToLowerInvariant() },
                    { "model", model },
                    { "provider", provider },
                    { "virtual_key_id", virtualKeyId },
                    { "has_retries", hasRetries.ToString().ToLowerInvariant() },
                    { "retry_count", retryCount.ToString() }
                };
                GenerationsRetried.Add(retryCount, retryTags);
            }
        }

        /// <summary>
        /// Records a failed generation with error categorization
        /// </summary>
        public void RecordGenerationFailed(string mediaType, string model, string provider, 
            string virtualKeyId, string errorType, string errorCategory, double durationSeconds, 
            bool isRetryable = false, int retryCount = 0)
        {
            var tags = new TagList
            {
                { "media_type", mediaType.ToLowerInvariant() },
                { "model", model },
                { "provider", provider },
                { "virtual_key_id", virtualKeyId },
                { "error_type", errorType },
                { "error_category", errorCategory },
                { "is_retryable", isRetryable.ToString().ToLowerInvariant() }
            };

            GenerationsFailed.Add(1, tags);
            ProviderErrors.Add(1, tags);
            ActiveGenerations.Add(-1, tags);
            
            if (durationSeconds > 0)
            {
                GenerationDuration.Record(durationSeconds, tags);
            }

            if (retryCount > 0)
            {
                var retryTags = new TagList
                {
                    { "media_type", mediaType.ToLowerInvariant() },
                    { "model", model },
                    { "provider", provider },
                    { "virtual_key_id", virtualKeyId },
                    { "error_type", errorType },
                    { "error_category", errorCategory },
                    { "is_retryable", isRetryable.ToString().ToLowerInvariant() },
                    { "retry_count", retryCount.ToString() }
                };
                GenerationsRetried.Add(retryCount, retryTags);
            }
        }

        /// <summary>
        /// Records a cancelled generation
        /// </summary>
        public void RecordGenerationCancelled(string mediaType, string model, string provider, 
            string virtualKeyId, string cancellationSource, double durationSeconds)
        {
            var tags = new TagList
            {
                { "media_type", mediaType.ToLowerInvariant() },
                { "model", model },
                { "provider", provider },
                { "virtual_key_id", virtualKeyId },
                { "cancellation_source", cancellationSource }
            };

            GenerationsCancelled.Add(1, tags);
            ActiveGenerations.Add(-1, tags);
            
            if (durationSeconds > 0)
            {
                GenerationDuration.Record(durationSeconds, tags);
            }
        }

        /// <summary>
        /// Records provider response time for health monitoring
        /// </summary>
        public void RecordProviderResponseTime(string provider, string model, double responseTimeSeconds)
        {
            var tags = new TagList
            {
                { "provider", provider },
                { "model", model }
            };

            ProviderResponseTime.Record(responseTimeSeconds, tags);
        }

        /// <summary>
        /// Updates provider availability status
        /// </summary>
        public void UpdateProviderAvailability(string provider, bool isAvailable)
        {
            var tags = new TagList { { "provider", provider } };
            ProviderAvailability.Add(isAvailable ? 1 : -1, tags);
        }

        /// <summary>
        /// Records storage operation metrics
        /// </summary>
        public void RecordStorageOperation(string operation, string mediaType, double durationMs, bool success)
        {
            var tags = new TagList
            {
                { "operation", operation },
                { "media_type", mediaType.ToLowerInvariant() },
                { "success", success.ToString().ToLowerInvariant() }
            };

            StorageOperations.Add(1, tags);
            StorageOperationDuration.Record(durationMs, tags);
        }

        /// <summary>
        /// Updates queue depth for monitoring queue pressure
        /// </summary>
        public void UpdateQueueDepth(string mediaType, int delta)
        {
            var tags = new TagList { { "media_type", mediaType.ToLowerInvariant() } };
            QueueDepth.Add(delta, tags);
        }

        /// <summary>
        /// Updates task registry size for resource monitoring
        /// </summary>
        public void UpdateTaskRegistrySize(int delta)
        {
            TaskRegistrySize.Add(delta);
        }

        /// <summary>
        /// Starts a distributed tracing activity for generation operations
        /// </summary>
        public static Activity? StartGenerationActivity(string operationName, string mediaType, 
            string model, string provider)
        {
            return ActivitySource.StartActivity(operationName, ActivityKind.Internal, Activity.Current?.Context ?? default, 
                new TagList
                {
                    { "media.type", mediaType.ToLowerInvariant() },
                    { "media.model", model },
                    { "media.provider", provider }
                });
        }

        public void Dispose()
        {
            _meter?.Dispose();
        }
    }
}