using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Middleware
{
    /// <summary>
    /// Middleware that maps exceptions to OpenAI-compatible error responses with proper HTTP status codes.
    /// </summary>
    public class OpenAIErrorMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<OpenAIErrorMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly ISecurityEventLogger? _securityEventLogger;

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
            // Log the exception with full details
            var traceId = context.TraceIdentifier;
            _logger.LogError(exception,
                "Exception handled by OpenAIErrorMiddleware {TraceId} {Method} {Path}",
                traceId,
                context.Request.Method.Replace(Environment.NewLine, ""),
                context.Request.Path.ToString().Replace(Environment.NewLine, ""));

            // Map exception to OpenAI error response
            var (statusCode, errorResponse) = MapExceptionToResponse(exception, traceId);

            // Log security-relevant exceptions
            await LogSecurityExceptionAsync(context, exception, statusCode);

            // Set response headers
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            
            // Add correlation ID header
            context.Response.Headers["X-Request-Id"] = traceId;

            // Add Retry-After header for rate limit exceptions
            if (exception is RateLimitExceededException rateLimitEx && rateLimitEx.RetryAfterSeconds.HasValue)
            {
                context.Response.Headers["Retry-After"] = rateLimitEx.RetryAfterSeconds.Value.ToString();
            }

            // Serialize and write response
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
            await context.Response.WriteAsync(json);
        }

        private (int statusCode, OpenAIErrorResponse response) MapExceptionToResponse(Exception exception, string traceId)
        {
            switch (exception)
            {
                case ModelNotFoundException modelEx:
                    return (404, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = modelEx.Message,
                            Type = "invalid_request_error",
                            Code = "model_not_found",
                            Param = "model"
                        }
                    });

                case InvalidRequestException invalidEx:
                    return (400, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = invalidEx.Message,
                            Type = "invalid_request_error",
                            Code = invalidEx.ErrorCode ?? "invalid_request",
                            Param = invalidEx.Param
                        }
                    });

                case AuthorizationException authEx:
                    return (403, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = authEx.Message,
                            Type = "invalid_request_error",
                            Code = "authorization_required"
                        }
                    });

                case RequestTimeoutException timeoutEx:
                    return (408, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = timeoutEx.Message,
                            Type = "timeout_error",
                            Code = "request_timeout"
                        }
                    });

                case PayloadTooLargeException payloadEx:
                    return (413, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = payloadEx.Message,
                            Type = "invalid_request_error",
                            Code = "payload_too_large"
                        }
                    });

                case RateLimitExceededException rateLimitEx:
                    return (429, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = rateLimitEx.Message,
                            Type = "rate_limit_error",
                            Code = "rate_limit_exceeded"
                        }
                    });

                case ServiceUnavailableException serviceEx:
                    return (503, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = serviceEx.Message,
                            Type = "service_unavailable",
                            Code = "service_unavailable"
                        }
                    });

                case ConfigurationException configEx:
                    // Legacy ConfigurationException support - map to 500
                    return (500, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = _environment.IsDevelopment() ? configEx.Message : "Configuration error occurred",
                            Type = "server_error",
                            Code = "configuration_error"
                        }
                    });

                case LLMCommunicationException commEx:
                    // Map based on status code if available
                    if (commEx.StatusCode.HasValue)
                    {
                        var statusCode = (int)commEx.StatusCode.Value;
                        return (statusCode, new OpenAIErrorResponse
                        {
                            Error = new OpenAIError
                            {
                                Message = commEx.Message,
                                Type = statusCode >= 500 ? "server_error" : "invalid_request_error",
                                Code = "provider_communication_error"
                            }
                        });
                    }
                    goto default;

                case UnauthorizedAccessException _:
                    return (401, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Authentication required",
                            Type = "invalid_request_error",
                            Code = "unauthorized"
                        }
                    });

                case ArgumentNullException argNullEx:
                    return (400, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = _environment.IsDevelopment() ? argNullEx.Message : "Required parameter is missing",
                            Type = "invalid_request_error",
                            Code = "missing_parameter",
                            Param = argNullEx.ParamName
                        }
                    });

                case ArgumentException argEx:
                    return (400, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = _environment.IsDevelopment() ? argEx.Message : "Invalid parameter value",
                            Type = "invalid_request_error",
                            Code = "invalid_parameter",
                            Param = argEx.ParamName
                        }
                    });

                case InvalidOperationException invalidOpEx:
                    return (400, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = _environment.IsDevelopment() ? invalidOpEx.Message : "Invalid operation",
                            Type = "invalid_request_error",
                            Code = "invalid_operation"
                        }
                    });

                case TimeoutException _:
                    return (408, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Request timed out",
                            Type = "timeout_error",
                            Code = "timeout"
                        }
                    });

                case NotImplementedException _:
                    return (501, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Feature not implemented",
                            Type = "server_error",
                            Code = "not_implemented"
                        }
                    });

                case KeyNotFoundException _:
                    return (404, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Resource not found",
                            Type = "invalid_request_error",
                            Code = "not_found"
                        }
                    });

                default:
                    // TODO: Future - Add circuit breaker status tracking here
                    // TODO: Future - Emit health monitoring events for 5xx errors
                    
                    // Log unexpected exceptions at ERROR level
                    _logger.LogError(exception, "Unexpected exception: {TraceId}", traceId);
                    
                    return (500, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = _environment.IsDevelopment() 
                                ? exception.Message 
                                : "An unexpected error occurred",
                            Type = "server_error",
                            Code = "internal_error"
                        }
                    });
            }
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