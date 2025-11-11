using System.Diagnostics;

using ConduitLLM.Core.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using ConduitLLM.Core.Interfaces;
namespace ConduitLLM.Core.Middleware
{
    /// <summary>
    /// Middleware that provides timeout diagnostics and logging for HTTP requests.
    /// </summary>
    public class TimeoutDiagnosticsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TimeoutDiagnosticsMiddleware> _logger;
        private readonly IOperationTimeoutProvider _timeoutProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutDiagnosticsMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger for diagnostic output.</param>
        /// <param name="timeoutProvider">The operation timeout provider.</param>
        public TimeoutDiagnosticsMiddleware(
            RequestDelegate next,
            ILogger<TimeoutDiagnosticsMiddleware> logger,
            IOperationTimeoutProvider timeoutProvider)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeoutProvider = timeoutProvider ?? throw new ArgumentNullException(nameof(timeoutProvider));
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            var operationType = DetermineOperationType(path);
            
            // Get configured timeout for this operation
            var timeout = _timeoutProvider.GetTimeout(operationType);
            var shouldApplyTimeout = _timeoutProvider.ShouldApplyTimeout(operationType);
            
            // Add timeout information to response headers for diagnostics
            context.Response.Headers.Append("X-Operation-Type", operationType);
            context.Response.Headers.Append("X-Timeout-Seconds", timeout.TotalSeconds.ToString());
            context.Response.Headers.Append("X-Timeout-Applied", shouldApplyTimeout.ToString());
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Log request start with timeout information
                _logger.LogInformation(
                    "Request started: {Method} {Path} - Operation: {OperationType}, Timeout: {TimeoutSeconds}s (Applied: {TimeoutApplied})",
                    context.Request.Method,
                    context.Request.Path,
                    operationType,
                    timeout.TotalSeconds,
                    shouldApplyTimeout);

                await _next(context);
                
                stopwatch.Stop();
                
                // Log request completion
                _logger.LogInformation(
                    "Request completed: {Method} {Path} - Duration: {DurationMs}ms, Status: {StatusCode}",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    context.Response.StatusCode);
                
                // Warn if request took longer than configured timeout
                if (stopwatch.Elapsed > timeout && shouldApplyTimeout)
                {
                    _logger.LogWarning(
                        "Request exceeded configured timeout: {Method} {Path} - Duration: {DurationMs}ms > Timeout: {TimeoutMs}ms",
                        context.Request.Method,
                        context.Request.Path,
                        stopwatch.ElapsedMilliseconds,
                        timeout.TotalMilliseconds);
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                
                _logger.LogWarning(
                    "Request canceled (likely timeout): {Method} {Path} - Duration: {DurationMs}ms, Configured timeout: {TimeoutMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    timeout.TotalMilliseconds);
                
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _logger.LogError(ex,
                    "Request failed: {Method} {Path} - Duration: {DurationMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);
                
                throw;
            }
        }

        private static string DetermineOperationType(string path)
        {
            if (string.IsNullOrEmpty(path))
                return OperationTypes.Completion;

            if (path.Contains("/chat/completions"))
                return OperationTypes.Chat;
            else if (path.Contains("/images/generations"))
                return OperationTypes.ImageGeneration;
            else if (path.Contains("/videos/generations"))
                return OperationTypes.VideoGeneration;
            else if (path.Contains("/health") || path.Contains("/healthz"))
                return OperationTypes.HealthCheck;
            else if (path.Contains("/models"))
                return OperationTypes.ModelDiscovery;
            else if (path.Contains("/completions"))
                return OperationTypes.Completion;
            else
                return OperationTypes.Completion;
        }
    }
}