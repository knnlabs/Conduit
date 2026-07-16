using System.Text.RegularExpressions;
using ConduitLLM.Core.Middleware;
using Prometheus;

namespace ConduitLLM.Admin.Middleware
{
    /// <summary>
    /// Middleware for collecting HTTP metrics for the Admin API.
    /// Tracks request/response metrics including duration, size, and status codes.
    /// </summary>
    public class AdminHttpMetricsMiddleware : HttpMetricsMiddlewareBase
    {
        // Core HTTP metrics
        private static readonly Counter RequestsTotal = Prometheus.Metrics
            .CreateCounter("conduit_admin_http_requests_total", "Total number of HTTP requests to Admin API",
                new CounterConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code" }
                });

        private static readonly Histogram RequestDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_http_request_duration_seconds", "HTTP request duration in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code" },
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 16) // 1ms to ~65s
                });

        private static readonly Histogram RequestSize = Prometheus.Metrics
            .CreateHistogram("conduit_admin_http_request_size_bytes", "HTTP request size in bytes",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint" },
                    Buckets = Histogram.ExponentialBuckets(100, 10, 8) // 100B to 10GB
                });

        private static readonly Histogram ResponseSize = Prometheus.Metrics
            .CreateHistogram("conduit_admin_http_response_size_bytes", "HTTP response size in bytes",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code" },
                    Buckets = Histogram.ExponentialBuckets(100, 10, 8) // 100B to 10GB
                });

        private static readonly Gauge ActiveRequests = Prometheus.Metrics
            .CreateGauge("conduit_admin_http_requests_active", "Number of active HTTP requests",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "method", "endpoint" }
                });

        private static readonly Counter ErrorsTotal = Prometheus.Metrics
            .CreateCounter("conduit_admin_http_errors_total", "Total number of HTTP errors",
                new CounterConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code", "error_type" }
                });

        // Regex patterns for path normalization
        private static readonly Regex GuidPattern = new(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.Compiled);
        private static readonly Regex NumberPattern = new(@"\b\d+\b", RegexOptions.Compiled);

        public AdminHttpMetricsMiddleware(RequestDelegate next, ILogger<AdminHttpMetricsMiddleware> logger)
            : base(next, logger) { }

        protected override bool ShouldSkipMetrics(HttpContext context) => false;

        protected override string GetNormalizedPath(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "/";

            if (string.IsNullOrEmpty(path))
                return "/";

            path = path.ToLowerInvariant();
            path = GuidPattern.Replace(path, "{id}");
            path = NumberPattern.Replace(path, "{id}");

            // Specific normalization for Admin API endpoints
            var normalizations = new Dictionary<string, string>
            {
                { "/api/virtualkeys/{id}", "/api/virtualkeys/{id}" },
                { "/api/providerhealth/{id}", "/api/providerhealth/{id}" },
                { "/api/modelmappings/{id}", "/api/modelmappings/{id}" },
                { "/api/providers/{id}", "/api/providers/{id}" },
                { "/api/providers/{id}/test", "/api/providers/{id}/test" },
                { "/api/providerhealth/providers/{id}", "/api/providerhealth/providers/{id}" }
            };

            foreach (var (pattern, normalized) in normalizations)
            {
                if (path.StartsWith(pattern.Replace("{id}", "")))
                {
                    return normalized;
                }
            }

            return path;
        }

        protected override void IncrementActiveRequests(string method, string path)
            => ActiveRequests.WithLabels(method, path).Inc();

        protected override void DecrementActiveRequests(string method, string path)
            => ActiveRequests.WithLabels(method, path).Dec();

        protected override void RecordRequestSize(string method, string path, long bytes)
            => RequestSize.WithLabels(method, path).Observe(bytes);

        protected override void RecordError(string method, string path, int statusCode, string errorType)
            => ErrorsTotal.WithLabels(method, path, statusCode.ToString(), errorType).Inc();

        protected override void RecordResponseMetrics(
            string method, string path, int statusCode, double durationSeconds,
            long responseBytes, HttpContext context)
        {
            var statusCodeStr = statusCode.ToString();

            ResponseSize.WithLabels(method, path, statusCodeStr).Observe(responseBytes);
            RequestsTotal.WithLabels(method, path, statusCodeStr).Inc();
            RequestDuration.WithLabels(method, path, statusCodeStr).Observe(durationSeconds);
        }
    }
}
