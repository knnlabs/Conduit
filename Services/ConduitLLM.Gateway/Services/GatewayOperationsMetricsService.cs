using Prometheus;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service for tracking Gateway API specific operational metrics.
    /// Provides static recording methods for controllers to report operation-level metrics,
    /// mirroring the pattern established by AdminOperationsMetricsService.
    /// </summary>
    public class GatewayOperationsMetricsService : PeriodicCollectorBackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GatewayOperationsMetricsService> _logger;

        // LLM operation metrics
        private static readonly Counter LlmOperations = Prometheus.Metrics
            .CreateCounter("conduit_gateway_llm_operations_total", "Total LLM operations by type",
                new CounterConfiguration
                {
                    LabelNames = new[] { "operation", "model", "status" } // operation: chat_completion, embedding, image_generation, video_generation
                });

        private static readonly Histogram LlmOperationDuration = Prometheus.Metrics
            .CreateHistogram("conduit_gateway_llm_operation_duration_seconds", "LLM operation duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation", "model" },
                    Buckets = Histogram.ExponentialBuckets(0.01, 2, 16) // 10ms to ~327s
                });

        // Media operation metrics
        private static readonly Counter MediaOperations = Prometheus.Metrics
            .CreateCounter("conduit_gateway_media_operations_total", "Total media operations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "operation", "media_type", "status" } // media_type: image, video; operation: generate, upload, download
                });

        private static readonly Histogram MediaGenerationDuration = Prometheus.Metrics
            .CreateHistogram("conduit_gateway_media_generation_duration_seconds", "Media generation duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "media_type", "model" },
                    Buckets = Histogram.ExponentialBuckets(0.1, 2, 14) // 100ms to ~820s
                });

        // Function execution metrics
        private static readonly Counter FunctionExecutions = Prometheus.Metrics
            .CreateCounter("conduit_gateway_function_executions_total", "Total function executions",
                new CounterConfiguration
                {
                    LabelNames = new[] { "status" } // status: success, failure, timeout
                });

        private static readonly Histogram FunctionExecutionDuration = Prometheus.Metrics
            .CreateHistogram("conduit_gateway_function_execution_duration_seconds", "Function execution duration",
                new HistogramConfiguration
                {
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 14) // 1ms to ~16s
                });

        // Streaming metrics
        private static readonly Counter StreamingRequests = Prometheus.Metrics
            .CreateCounter("conduit_gateway_streaming_requests_total", "Total streaming requests",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "status" }
                });

        // Provider routing metrics
        private static readonly Counter RoutingDecisions = Prometheus.Metrics
            .CreateCounter("conduit_gateway_routing_decisions_total", "Total provider routing decisions",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider", "reason" } // reason: primary, fallback, round_robin, least_loaded
                });

        public GatewayOperationsMetricsService(
            IServiceProvider serviceProvider,
            ILogger<GatewayOperationsMetricsService> logger)
            : base(logger, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5))
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override Task CollectOnceAsync(CancellationToken cancellationToken) =>
            CollectMetricsAsync();

        protected override void OnCollectionFailed(Exception exception) =>
            _logger.LogError(exception, "Error collecting gateway operations metrics");

        private Task CollectMetricsAsync()
        {
            // Currently all metrics are recorded in real-time via static methods.
            // This method is reserved for future periodic gauge collection
            // (e.g., querying task queue depth, active streaming connections).
            _logger.LogDebug("Gateway operations metrics collection cycle completed");
            return Task.CompletedTask;
        }

        // Static methods to be called by Gateway controllers and services

        /// <summary>
        /// Records an LLM operation (chat completion, embedding, image/video generation).
        /// </summary>
        public static void RecordLlmOperation(string operation, string model, string status, double? durationSeconds = null)
        {
            LlmOperations.WithLabels(operation, model, status).Inc();
            if (durationSeconds.HasValue)
            {
                LlmOperationDuration.WithLabels(operation, model).Observe(durationSeconds.Value);
            }
        }

        /// <summary>
        /// Records a media operation (generation, upload, download).
        /// </summary>
        public static void RecordMediaOperation(string operation, string mediaType, string status, double? durationSeconds = null, string? model = null)
        {
            MediaOperations.WithLabels(operation, mediaType, status).Inc();
            if (durationSeconds.HasValue && model != null && operation == "generate")
            {
                MediaGenerationDuration.WithLabels(mediaType, model).Observe(durationSeconds.Value);
            }
        }

        /// <summary>
        /// Records a function execution.
        /// </summary>
        public static void RecordFunctionExecution(string status, double? durationSeconds = null)
        {
            FunctionExecutions.WithLabels(status).Inc();
            if (durationSeconds.HasValue)
            {
                FunctionExecutionDuration.Observe(durationSeconds.Value);
            }
        }

        /// <summary>
        /// Records a streaming request.
        /// </summary>
        public static void RecordStreamingRequest(string model, string status)
        {
            StreamingRequests.WithLabels(model, status).Inc();
        }

        /// <summary>
        /// Records a provider routing decision.
        /// </summary>
        public static void RecordRoutingDecision(string model, string provider, string reason)
        {
            RoutingDecisions.WithLabels(model, provider, reason).Inc();
        }
    }
}
