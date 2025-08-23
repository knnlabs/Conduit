using System.Diagnostics;

using Prometheus;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Middleware for tracking HTTP request metrics using Prometheus.
    /// Provides comprehensive metrics for monitoring API performance at scale.
    /// </summary>
    public class HttpMetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<HttpMetricsMiddleware> _logger;

        // Prometheus metrics
        private static readonly Counter RequestsTotal = Prometheus.Metrics
            .CreateCounter("conduit_http_requests_total", "Total number of HTTP requests",
                new CounterConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code", "virtual_key_id" }
                });

        private static readonly Histogram RequestDuration = Prometheus.Metrics
            .CreateHistogram("conduit_http_request_duration_seconds", "HTTP request duration in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code" },
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 16) // 1ms to ~65s
                });

        private static readonly Gauge ActiveRequests = Prometheus.Metrics
            .CreateGauge("conduit_http_requests_active", "Number of active HTTP requests",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "method", "endpoint" }
                });

        private static readonly Histogram RequestSize = Prometheus.Metrics
            .CreateHistogram("conduit_http_request_size_bytes", "HTTP request size in bytes",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint" },
                    Buckets = Histogram.ExponentialBuckets(100, 2, 16) // 100 bytes to ~6.5MB
                });

        private static readonly Histogram ResponseSize = Prometheus.Metrics
            .CreateHistogram("conduit_http_response_size_bytes", "HTTP response size in bytes",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code" },
                    Buckets = Histogram.ExponentialBuckets(100, 2, 16) // 100 bytes to ~6.5MB
                });

        private static readonly Counter RateLimitHits = Prometheus.Metrics
            .CreateCounter("conduit_rate_limit_exceeded_total", "Total number of rate limit exceeded responses",
                new CounterConfiguration
                {
                    LabelNames = new[] { "endpoint", "virtual_key_id" }
                });

        private static readonly Summary RequestDurationSummary = Prometheus.Metrics
            .CreateSummary("conduit_http_request_duration_summary", "Summary of HTTP request durations",
                new SummaryConfiguration
                {
                    LabelNames = new[] { "method", "endpoint" },
                    Objectives = new[]
                    {
                        new QuantileEpsilonPair(0.5, 0.05),   // p50
                        new QuantileEpsilonPair(0.9, 0.01),   // p90
                        new QuantileEpsilonPair(0.95, 0.005), // p95
                        new QuantileEpsilonPair(0.99, 0.001)  // p99
                    },
                    MaxAge = TimeSpan.FromMinutes(5),
                    AgeBuckets = 5
                });

        public HttpMetricsMiddleware(RequestDelegate next, ILogger<HttpMetricsMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = GetNormalizedPath(context.Request.Path);
            var method = context.Request.Method;

            // Skip metrics for health checks to avoid noise
            if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Track request size
            if (context.Request.ContentLength.HasValue)
            {
                RequestSize.WithLabels(method, path).Observe(context.Request.ContentLength.Value);
            }

            // Start timing the request
            var stopwatch = Stopwatch.StartNew();
            
            // Track active requests
            using (ActiveRequests.WithLabels(method, path).TrackInProgress())
            {
                try
                {
                    // Capture original response body stream
                    var originalBodyStream = context.Response.Body;
                    using var responseBody = new System.IO.MemoryStream();
                    context.Response.Body = responseBody;

                    await _next(context);

                    // Copy response to original stream and track size
                    context.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                    context.Response.Body = originalBodyStream;

                    // Track response size
                    ResponseSize.WithLabels(method, path, context.Response.StatusCode.ToString())
                        .Observe(responseBody.Length);
                }
                catch (OperationCanceledException)
                {
                    context.Response.StatusCode = 499; // Client closed request
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in request pipeline");
                    if (context.Response.StatusCode == 200)
                    {
                        context.Response.StatusCode = 500;
                    }
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    var duration = stopwatch.Elapsed.TotalSeconds;
                    var statusCode = context.Response.StatusCode.ToString();
                    var virtualKeyId = GetVirtualKeyId(context);

                    // Record metrics
                    RequestsTotal.WithLabels(method, path, statusCode, virtualKeyId).Inc();
                    RequestDuration.WithLabels(method, path, statusCode).Observe(duration);
                    RequestDurationSummary.WithLabels(method, path).Observe(duration);

                    // Track rate limit hits
                    if (context.Response.StatusCode == 429)
                    {
                        RateLimitHits.WithLabels(path, virtualKeyId).Inc();
                    }

                    // Log slow requests
                    if (duration > 5.0)
                    {
                        _logger.LogWarning("Slow request detected: {Method} {Path} took {Duration:F2}s with status {StatusCode}",
                            method, path, duration, statusCode);
                    }
                }
            }
        }

        private static string GetNormalizedPath(PathString path)
        {
            var pathValue = path.Value ?? "/";

            // Normalize common path patterns to reduce cardinality
            // Replace GUIDs with {id}
            pathValue = System.Text.RegularExpressions.Regex.Replace(
                pathValue,
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
                "{id}");

            // Replace numeric IDs with {id}
            pathValue = System.Text.RegularExpressions.Regex.Replace(
                pathValue,
                @"\/\d+",
                "/{id}");

            // Common API endpoints
            if (pathValue.StartsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
                return "/v1/chat/completions";
            if (pathValue.StartsWith("/v1/embeddings", StringComparison.OrdinalIgnoreCase))
                return "/v1/embeddings";
            if (pathValue.StartsWith("/v1/models", StringComparison.OrdinalIgnoreCase))
                return "/v1/models";
            if (pathValue.StartsWith("/v1/images/generations", StringComparison.OrdinalIgnoreCase))
                return "/v1/images/generations";
            if (pathValue.StartsWith("/v1/videos/generations", StringComparison.OrdinalIgnoreCase))
                return "/v1/videos/generations";
            if (pathValue.StartsWith("/v1/audio", StringComparison.OrdinalIgnoreCase))
                return "/v1/audio/{operation}";

            return pathValue.ToLowerInvariant();
        }

        private static string GetVirtualKeyId(HttpContext context)
        {
            // Try to get virtual key ID from the authenticated user
            var virtualKeyId = context.User?.FindFirst("VirtualKeyId")?.Value;
            if (!string.IsNullOrEmpty(virtualKeyId))
                return virtualKeyId;

            // Try to get it from a custom header set by authentication
            if (context.Items.TryGetValue("VirtualKeyId", out var keyId) && keyId is string strKeyId)
                return strKeyId;

            return "anonymous";
        }
    }
}