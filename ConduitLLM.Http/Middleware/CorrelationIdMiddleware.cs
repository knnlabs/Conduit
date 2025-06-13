using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Middleware for managing correlation IDs across distributed requests.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;
        private readonly CorrelationIdOptions _options;

        // Common header names for correlation IDs
        private static readonly string[] IncomingHeaderNames = new[]
        {
            "X-Correlation-ID",
            "X-Request-ID",
            "X-Trace-ID",
            "X-Amzn-Trace-Id",
            "TraceId"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="options">The correlation ID options.</param>
        public CorrelationIdMiddleware(
            RequestDelegate next,
            ILogger<CorrelationIdMiddleware> logger,
            CorrelationIdOptions? options = null)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new CorrelationIdOptions();
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = GetOrCreateCorrelationId(context);
            
            // Set correlation ID in various contexts
            context.TraceIdentifier = correlationId;
            context.Items[CorrelationIdOptions.CorrelationIdItemsKey] = correlationId;
            
            // Add to response headers
            if (_options.IncludeInResponse)
            {
                context.Response.OnStarting(() =>
                {
                    if (!context.Response.Headers.ContainsKey(_options.ResponseHeader))
                    {
                        context.Response.Headers[_options.ResponseHeader] = correlationId;
                    }
                    return Task.CompletedTask;
                });
            }

            // Set logging scope
            using (_logger.BeginScope("{CorrelationId}", correlationId))
            {
                // Update Activity (for OpenTelemetry integration)
                var activity = Activity.Current;
                if (activity != null)
                {
                    activity.SetBaggage("correlation.id", correlationId);
                    activity.SetTag("correlation.id", correlationId);
                }

                _logger.LogDebug(
                    "Processing request with correlation ID: {CorrelationId}, Path: {Path}",
                    correlationId, context.Request.Path);

                try
                {
                    await _next(context);
                }
                finally
                {
                    _logger.LogDebug(
                        "Completed request with correlation ID: {CorrelationId}, Status: {StatusCode}",
                        correlationId, context.Response.StatusCode);
                }
            }
        }

        private string GetOrCreateCorrelationId(HttpContext context)
        {
            // First, check if we have an incoming correlation ID
            foreach (var headerName in IncomingHeaderNames)
            {
                if (context.Request.Headers.TryGetValue(headerName, out var values))
                {
                    var correlationId = values.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(correlationId))
                    {
                        _logger.LogDebug(
                            "Using incoming correlation ID from header {HeaderName}: {CorrelationId}",
                            headerName, correlationId);
                        return correlationId;
                    }
                }
            }

            // Check if we have a W3C trace parent header
            if (context.Request.Headers.TryGetValue("traceparent", out var traceParent))
            {
                var parts = traceParent.ToString().Split('-');
                if (parts.Length >= 3)
                {
                    // Use the trace ID portion
                    var traceId = parts[1];
                    _logger.LogDebug(
                        "Using trace ID from traceparent header as correlation ID: {CorrelationId}",
                        traceId);
                    return traceId;
                }
            }

            // Check Activity.Current for existing trace ID
            var currentActivity = Activity.Current;
            if (currentActivity != null && currentActivity.TraceId != default)
            {
                var activityTraceId = currentActivity.TraceId.ToString();
                _logger.LogDebug(
                    "Using Activity trace ID as correlation ID: {CorrelationId}",
                    activityTraceId);
                return activityTraceId;
            }

            // Generate a new correlation ID
            var newCorrelationId = GenerateCorrelationId();
            _logger.LogDebug(
                "Generated new correlation ID: {CorrelationId}",
                newCorrelationId);
            return newCorrelationId;
        }

        private string GenerateCorrelationId()
        {
            return _options.UseShortIds 
                ? Guid.NewGuid().ToString("N").Substring(0, 8) 
                : Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Options for correlation ID middleware.
    /// </summary>
    public class CorrelationIdOptions
    {
        /// <summary>
        /// The key used to store correlation ID in HttpContext.Items.
        /// </summary>
        public const string CorrelationIdItemsKey = "CorrelationId";

        /// <summary>
        /// Gets or sets the response header name for correlation ID.
        /// </summary>
        public string ResponseHeader { get; set; } = "X-Correlation-ID";

        /// <summary>
        /// Gets or sets whether to include correlation ID in response headers.
        /// </summary>
        public bool IncludeInResponse { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to use short IDs (8 characters) instead of full GUIDs.
        /// </summary>
        public bool UseShortIds { get; set; } = false;
    }

    /// <summary>
    /// Extension methods for correlation ID middleware.
    /// </summary>
    public static class CorrelationIdMiddlewareExtensions
    {
        /// <summary>
        /// Adds correlation ID middleware to the application pipeline.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>();
        }

        /// <summary>
        /// Adds correlation ID middleware with custom options.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="options">The correlation ID options.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseCorrelationId(
            this IApplicationBuilder builder,
            CorrelationIdOptions options)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>(options);
        }

        /// <summary>
        /// Gets the correlation ID from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The correlation ID, or null if not found.</returns>
        public static string? GetCorrelationId(this HttpContext context)
        {
            if (context.Items.TryGetValue(CorrelationIdOptions.CorrelationIdItemsKey, out var correlationId))
            {
                return correlationId as string;
            }
            return context.TraceIdentifier;
        }
    }
}