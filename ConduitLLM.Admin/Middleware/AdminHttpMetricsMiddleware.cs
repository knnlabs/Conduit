using System.Diagnostics;
using System.Text.RegularExpressions;
using Prometheus;

namespace ConduitLLM.Admin.Middleware
{
    /// <summary>
    /// Middleware for collecting HTTP metrics for the Admin API.
    /// Tracks request/response metrics including duration, size, and status codes.
    /// </summary>
    public class AdminHttpMetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AdminHttpMetricsMiddleware> _logger;

        // Core HTTP metrics
        private static readonly Counter RequestsTotal = Metrics
            .CreateCounter("conduit_admin_http_requests_total", "Total number of HTTP requests to Admin API",
                new CounterConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code" }
                });

        private static readonly Histogram RequestDuration = Metrics
            .CreateHistogram("conduit_admin_http_request_duration_seconds", "HTTP request duration in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code" },
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 16) // 1ms to ~65s
                });

        private static readonly Histogram RequestSize = Metrics
            .CreateHistogram("conduit_admin_http_request_size_bytes", "HTTP request size in bytes",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint" },
                    Buckets = Histogram.ExponentialBuckets(100, 10, 8) // 100B to 10GB
                });

        private static readonly Histogram ResponseSize = Metrics
            .CreateHistogram("conduit_admin_http_response_size_bytes", "HTTP response size in bytes",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code" },
                    Buckets = Histogram.ExponentialBuckets(100, 10, 8) // 100B to 10GB
                });

        private static readonly Gauge ActiveRequests = Metrics
            .CreateGauge("conduit_admin_http_requests_active", "Number of active HTTP requests",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "method", "endpoint" }
                });

        private static readonly Counter ErrorsTotal = Metrics
            .CreateCounter("conduit_admin_http_errors_total", "Total number of HTTP errors",
                new CounterConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code", "error_type" }
                });

        // Regex patterns for path normalization
        private static readonly Regex GuidPattern = new Regex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.Compiled);
        private static readonly Regex NumberPattern = new Regex(@"\b\d+\b", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminHttpMetricsMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        public AdminHttpMetricsMiddleware(RequestDelegate next, ILogger<AdminHttpMetricsMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Processes an individual request and records metrics.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var path = NormalizePath(context.Request.Path.Value ?? "/");
            var method = context.Request.Method;

            // Track active requests
            using (ActiveRequests.WithLabels(method, path).TrackInProgress())
            {
                // Capture request size
                if (context.Request.ContentLength.HasValue)
                {
                    RequestSize.WithLabels(method, path).Observe(context.Request.ContentLength.Value);
                }

                // Store original response body stream
                var originalBodyStream = context.Response.Body;
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                try
                {
                    await _next(context);

                    // Capture response size
                    var responseSize = responseBody.Length;
                    ResponseSize.WithLabels(method, path, context.Response.StatusCode.ToString()).Observe(responseSize);

                    // Copy the response body back to the original stream
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                }
                catch (TaskCanceledException)
                {
                    context.Response.StatusCode = 499; // Client closed request
                    ErrorsTotal.WithLabels(method, path, "499", "client_cancelled").Inc();
                    throw;
                }
                catch (Exception ex)
                {
                    var errorType = ex.GetType().Name;
                    ErrorsTotal.WithLabels(method, path, context.Response.StatusCode.ToString(), errorType).Inc();
                    _logger.LogError(ex, "Unhandled exception in request pipeline");
                    throw;
                }
                finally
                {
                    context.Response.Body = originalBodyStream;

                    // Record metrics
                    stopwatch.Stop();
                    var statusCode = context.Response.StatusCode.ToString();

                    RequestsTotal.WithLabels(method, path, statusCode).Inc();
                    RequestDuration.WithLabels(method, path, statusCode).Observe(stopwatch.Elapsed.TotalSeconds);

                    // Log slow requests
                    if (stopwatch.Elapsed.TotalSeconds > 5)
                    {
                        _logger.LogWarning("Slow request detected: {Method} {Path} took {Duration}s with status {StatusCode}",
                            method, path, stopwatch.Elapsed.TotalSeconds, statusCode);
                    }
                }
            }
        }

        /// <summary>
        /// Normalizes request paths to reduce cardinality in metrics.
        /// Replaces GUIDs and numeric IDs with placeholders.
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            // Normalize common Admin API endpoints
            path = path.ToLowerInvariant();

            // Replace GUIDs with {id}
            path = GuidPattern.Replace(path, "{id}");

            // Replace numeric IDs with {id}
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
    }
}