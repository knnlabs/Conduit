using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Middleware
{
    /// <summary>
    /// Global exception handling middleware that provides consistent error responses
    /// and prevents sensitive information disclosure.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly ISecurityEventLogger? _securityEventLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalExceptionMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="environment">The web host environment.</param>
        /// <param name="securityEventLogger">Optional security event logger.</param>
        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
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
            // Log the exception with full details
            var traceId = context.TraceIdentifier;
            _logger.LogError(exception, 
                "Unhandled exception occurred {TraceId} {Method} {Path}",
                traceId, context.Request.Method, context.Request.Path);

            // Determine the response based on exception type
            var (statusCode, errorResponse) = GetErrorResponse(exception, traceId);

            // Log security-relevant exceptions
            await LogSecurityExceptionAsync(context, exception, statusCode);

            // Write the response
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
            await context.Response.WriteAsync(json);
        }

        private (int statusCode, ErrorResponse response) GetErrorResponse(Exception exception, string traceId)
        {
            var errorResponse = new ErrorResponse
            {
                TraceId = traceId,
                Timestamp = DateTime.UtcNow,
                Path = null // Will be set later if needed
            };

            // Handle specific exception types
            switch (exception)
            {
                case UnauthorizedAccessException _:
                    errorResponse.Error = "Unauthorized";
                    errorResponse.Message = "Access denied. Please check your credentials.";
                    return ((int)HttpStatusCode.Unauthorized, errorResponse);

                case ArgumentNullException _:
                    errorResponse.Error = "Bad Request";
                    errorResponse.Message = "Required parameter is missing.";
                    if (_environment.IsDevelopment())
                    {
                        errorResponse.Details = exception.Message;
                    }
                    return ((int)HttpStatusCode.BadRequest, errorResponse);

                case ArgumentException _:
                    errorResponse.Error = "Bad Request";
                    errorResponse.Message = "Invalid request parameters.";
                    if (_environment.IsDevelopment())
                    {
                        errorResponse.Details = exception.Message;
                    }
                    return ((int)HttpStatusCode.BadRequest, errorResponse);

                case InvalidOperationException _:
                    errorResponse.Error = "Invalid Operation";
                    errorResponse.Message = "The requested operation is not valid.";
                    if (_environment.IsDevelopment())
                    {
                        errorResponse.Details = exception.Message;
                    }
                    return ((int)HttpStatusCode.BadRequest, errorResponse);

                case TimeoutException _:
                    errorResponse.Error = "Request Timeout";
                    errorResponse.Message = "The request timed out. Please try again.";
                    return ((int)HttpStatusCode.RequestTimeout, errorResponse);

                case NotImplementedException _:
                    errorResponse.Error = "Not Implemented";
                    errorResponse.Message = "This feature is not yet available.";
                    return ((int)HttpStatusCode.NotImplemented, errorResponse);

                case KeyNotFoundException _:
                    errorResponse.Error = "Not Found";
                    errorResponse.Message = "The requested resource was not found.";
                    return ((int)HttpStatusCode.NotFound, errorResponse);

                default:
                    // Generic error response for production
                    errorResponse.Error = "Internal Server Error";
                    errorResponse.Message = "An unexpected error occurred. Please try again later.";
                    
                    // Only include details in development
                    if (_environment.IsDevelopment())
                    {
                        errorResponse.Details = exception.ToString();
                    }
                    
                    return ((int)HttpStatusCode.InternalServerError, errorResponse);
            }
        }

        private async Task LogSecurityExceptionAsync(HttpContext context, Exception exception, int statusCode)
        {
            if (_securityEventLogger == null)
                return;

            // Log certain exceptions as security events
            if (exception is UnauthorizedAccessException)
            {
                var virtualKey = context.Request.Headers["X-Virtual-Key"].FirstOrDefault() ?? "Unknown";
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                
                await _securityEventLogger.LogAuthorizationViolationAsync(
                    virtualKey,
                    context.Request.Path,
                    context.Request.Method,
                    ipAddress);
            }
            else if (statusCode == (int)HttpStatusCode.BadRequest && 
                     (exception is ArgumentException || exception is FormatException))
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
                        ["ipAddress"] = ipAddress
                    });
            }
        }
    }

    /// <summary>
    /// Standardized error response format.
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Gets or sets the trace ID for correlation.
        /// </summary>
        public string TraceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error type.
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user-friendly error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional details (only in development).
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the request path (optional).
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets validation errors (for bad requests).
        /// </summary>
        public Dictionary<string, string[]>? ValidationErrors { get; set; }
    }

    /// <summary>
    /// Extension methods for global exception middleware.
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        /// <summary>
        /// Adds the global exception handling middleware to the pipeline.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}