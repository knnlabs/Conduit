using System;
using System.Threading.Tasks;

using ConduitLLM.Core.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Middleware for exposing Prometheus metrics endpoint.
    /// </summary>
    public class PrometheusMetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PrometheusMetricsMiddleware> _logger;
        private readonly string _metricsPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusMetricsMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="metricsPath">The path for metrics endpoint.</param>
        public PrometheusMetricsMiddleware(
            RequestDelegate next,
            ILogger<PrometheusMetricsMiddleware> logger,
            string metricsPath = "/metrics")
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsPath = metricsPath;
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.Equals(_metricsPath, StringComparison.OrdinalIgnoreCase))
            {
                await HandleMetricsRequest(context);
                return;
            }

            await _next(context);
        }

        private async Task HandleMetricsRequest(HttpContext context)
        {
            try
            {
                // Check if the metrics exporter is registered
                var exporter = context.RequestServices.GetService<PrometheusAudioMetricsExporter>();
                if (exporter == null)
                {
                    _logger.LogWarning("Prometheus metrics exporter not registered");
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsync("Metrics exporter not available");
                    return;
                }

                // Get metrics
                var metrics = await exporter.GetMetricsAsync();

                // Set response headers
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";

                // Write metrics
                await context.Response.WriteAsync(metrics);

                _logger.LogDebug("Served Prometheus metrics, {ByteCount} bytes", metrics.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving Prometheus metrics");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Error generating metrics");
            }
        }
    }

    /// <summary>
    /// Extension methods for adding Prometheus metrics middleware.
    /// </summary>
    public static class PrometheusMetricsMiddlewareExtensions
    {
        /// <summary>
        /// Adds Prometheus metrics endpoint to the application pipeline.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="path">The path for the metrics endpoint.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UsePrometheusMetrics(
            this IApplicationBuilder builder,
            string path = "/metrics")
        {
            return builder.UseMiddleware<PrometheusMetricsMiddleware>(path);
        }
    }
}