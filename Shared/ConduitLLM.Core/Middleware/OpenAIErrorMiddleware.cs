using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Prometheus;

namespace ConduitLLM.Core.Middleware
{
    /// <summary>
    /// Middleware that maps exceptions to OpenAI-compatible error responses with proper HTTP status codes.
    /// Uses <see cref="ExceptionToResponseMapper"/> as the single source of truth for exception mapping.
    /// </summary>
    public class OpenAIErrorMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<OpenAIErrorMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly ISecurityEventLogger? _securityEventLogger;

        private static readonly Counter ExceptionsHandled = Prometheus.Metrics
            .CreateCounter("conduit_error_middleware_exceptions_total", "Total exceptions handled by error middleware",
                new CounterConfiguration
                {
                    LabelNames = new[] { "exception_type", "status_code", "endpoint" }
                });

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIErrorMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="environment">The web host environment.</param>
        /// <param name="securityEventLogger">Optional security event logger.</param>
        public OpenAIErrorMiddleware(
            RequestDelegate next,
            ILogger<OpenAIErrorMiddleware> logger,
            IWebHostEnvironment environment,
            ISecurityEventLogger? securityEventLogger = null)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _securityEventLogger = securityEventLogger;
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Log the exception with full details including request body for mutations
            var traceId = context.TraceIdentifier;
            string? requestBody = null;
            try
            {
                requestBody = await RequestBodyCapture.CaptureAsync(context);
            }
            catch
            {
                // Body capture should never prevent error handling
            }

            if (requestBody != null)
            {
                _logger.LogError(exception,
                    "Exception handled by OpenAIErrorMiddleware {TraceId} {Method} {Path}. RequestBody: {RequestBody}",
                    traceId,
                    LoggingSanitizer.S(context.Request.Method),
                    LoggingSanitizer.S(context.Request.Path.ToString()),
                    requestBody);
            }
            else
            {
                _logger.LogError(exception,
                    "Exception handled by OpenAIErrorMiddleware {TraceId} {Method} {Path}",
                    traceId,
                    LoggingSanitizer.S(context.Request.Method),
                    LoggingSanitizer.S(context.Request.Path.ToString()));
            }

            // Map exception using the single source of truth
            var mapping = ExceptionToResponseMapper.Map(exception);

            // Record exception metrics
            var normalizedEndpoint = NormalizeEndpointForMetrics(context.Request.Path.Value ?? "/");
            ExceptionsHandled.WithLabels(
                exception.GetType().Name,
                mapping.StatusCode.ToString(),
                normalizedEndpoint).Inc();

            // In development, show actual exception messages for redacted responses
            var message = mapping.IncludeExceptionMessageInLog
                ? mapping.ResponseMessage
                : (_environment.IsDevelopment() ? exception.Message : mapping.ResponseMessage);

            // Log security-relevant exceptions
            await LogSecurityExceptionAsync(context, exception, mapping.StatusCode);

            // Set response headers
            context.Response.StatusCode = mapping.StatusCode;
            context.Response.ContentType = "application/json";

            // Add correlation ID header
            context.Response.Headers["X-Request-Id"] = traceId;

            // Add Retry-After header for rate limit exceptions
            if (exception is RateLimitExceededException rateLimitEx && rateLimitEx.RetryAfterSeconds.HasValue)
            {
                context.Response.Headers["Retry-After"] = rateLimitEx.RetryAfterSeconds.Value.ToString();
            }

            // Build and serialize response
            var errorResponse = new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = message,
                    Type = mapping.OpenAIErrorType,
                    Code = mapping.ErrorCode,
                    Param = mapping.Param
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
            await context.Response.WriteAsync(json);
        }

        private static string NormalizeEndpointForMetrics(string path)
        {
            // Reduce cardinality by normalizing to known endpoint patterns
            if (path.StartsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
                return "/v1/chat/completions";
            if (path.StartsWith("/v1/embeddings", StringComparison.OrdinalIgnoreCase))
                return "/v1/embeddings";
            if (path.StartsWith("/v1/images", StringComparison.OrdinalIgnoreCase))
                return "/v1/images";
            if (path.StartsWith("/v1/videos", StringComparison.OrdinalIgnoreCase))
                return "/v1/videos";
            if (path.StartsWith("/v1/models", StringComparison.OrdinalIgnoreCase))
                return "/v1/models";
            if (path.StartsWith("/v1/batch", StringComparison.OrdinalIgnoreCase))
                return "/v1/batch";
            if (path.StartsWith("/v1/audio", StringComparison.OrdinalIgnoreCase))
                return "/v1/audio";
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                return "/api/*";
            return "/other";
        }

        private async Task LogSecurityExceptionAsync(HttpContext context, Exception exception, int statusCode)
        {
            if (_securityEventLogger == null)
                return;

            // Log certain exceptions as security events
            if (exception is UnauthorizedAccessException || exception is AuthorizationException)
            {
                var virtualKey = context.Request.Headers["X-Virtual-Key"].FirstOrDefault() ?? "Unknown";
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                await _securityEventLogger.LogAuthorizationViolationAsync(
                    virtualKey,
                    context.Request.Path,
                    context.Request.Method,
                    ipAddress);
            }
            else if (statusCode == 400 &&
                     (exception is ArgumentException || exception is InvalidRequestException))
            {
                // Potential injection attempt or malformed input
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                await _securityEventLogger.LogSuspiciousActivityAsync(
                    $"Malformed input detected: {exception.GetType().Name}",
                    SecurityEventSeverity.Low,
                    new Dictionary<string, object>
                    {
                        ["path"] = context.Request.Path.ToString(),
                        ["method"] = context.Request.Method,
                        ["ipAddress"] = ipAddress,
                        ["traceId"] = context.TraceIdentifier
                    });
            }
        }
    }

    /// <summary>
    /// Extension methods for OpenAI error middleware.
    /// </summary>
    public static class OpenAIErrorMiddlewareExtensions
    {
        /// <summary>
        /// Adds the OpenAI error handling middleware to the pipeline.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseOpenAIErrorHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<OpenAIErrorMiddleware>();
        }
    }
}
