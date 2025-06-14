using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Middleware
{
    /// <summary>
    /// Middleware for sanitizing user input to prevent log injection and other security vulnerabilities.
    /// </summary>
    /// <remarks>
    /// This middleware sanitizes route values, query strings, and headers to remove potentially dangerous characters
    /// that could be used for log injection, header injection, or other security exploits.
    /// </remarks>
    public class InputSanitizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<InputSanitizationMiddleware> _logger;
        
        // Regex patterns for dangerous characters
        private static readonly Regex CrlfPattern = new(@"[\r\n]", RegexOptions.Compiled);
        private static readonly Regex ControlCharPattern = new(@"[\x00-\x1F\x7F]", RegexOptions.Compiled);
        
        // Maximum length for input values
        private const int MaxInputLength = 1000;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputSanitizationMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger.</param>
        public InputSanitizationMiddleware(RequestDelegate next, ILogger<InputSanitizationMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Sanitize route values
                SanitizeRouteValues(context);

                // Sanitize query string
                SanitizeQueryString(context);

                // Sanitize headers (only specific headers that might be logged)
                SanitizeHeaders(context);

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in input sanitization middleware");
                throw;
            }
        }

        private void SanitizeRouteValues(HttpContext context)
        {
            if (context.Request.RouteValues?.Count > 0)
            {
                var routeValues = context.Request.RouteValues.ToList();
                foreach (var kvp in routeValues)
                {
                    if (kvp.Value is string stringValue)
                    {
                        var sanitized = SanitizeString(stringValue);
                        if (sanitized != stringValue)
                        {
                            context.Request.RouteValues[kvp.Key] = sanitized;
                            _logger.LogDebug("Sanitized route value {Key}", kvp.Key);
                        }
                    }
                }
            }
        }

        private void SanitizeQueryString(HttpContext context)
        {
            if (context.Request.Query?.Count > 0)
            {
                var query = context.Request.Query;
                var newQuery = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
                var modified = false;

                foreach (var kvp in query)
                {
                    var sanitizedValues = kvp.Value
                        .Select(v => SanitizeString(v ?? string.Empty))
                        .ToArray();

                    if (!kvp.Value.SequenceEqual(sanitizedValues))
                    {
                        modified = true;
                        _logger.LogDebug("Sanitized query parameter {Key}", kvp.Key);
                    }

                    newQuery[kvp.Key] = new Microsoft.Extensions.Primitives.StringValues(sanitizedValues);
                }

                if (modified)
                {
                    context.Request.QueryString = QueryString.Create(newQuery);
                }
            }
        }

        private void SanitizeHeaders(HttpContext context)
        {
            // Only sanitize headers that might be logged or used in responses
            var headersToSanitize = new[] { "User-Agent", "Referer", "X-Forwarded-For", "X-Real-IP" };

            foreach (var headerName in headersToSanitize)
            {
                if (context.Request.Headers.TryGetValue(headerName, out var headerValue))
                {
                    var sanitized = headerValue
                        .Select(v => SanitizeString(v ?? string.Empty))
                        .ToArray();

                    if (!headerValue.SequenceEqual(sanitized))
                    {
                        context.Request.Headers[headerName] = sanitized;
                        _logger.LogDebug("Sanitized header {HeaderName}", headerName);
                    }
                }
            }
        }

        /// <summary>
        /// Sanitizes a string by removing dangerous characters.
        /// </summary>
        /// <param name="input">The input string to sanitize.</param>
        /// <returns>The sanitized string.</returns>
        public static string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Remove CRLF characters to prevent log injection
            var sanitized = CrlfPattern.Replace(input, " ");

            // Remove other control characters
            sanitized = ControlCharPattern.Replace(sanitized, string.Empty);

            // Truncate if too long
            if (sanitized.Length > MaxInputLength)
            {
                sanitized = sanitized.Substring(0, MaxInputLength);
            }

            return sanitized;
        }
    }

    /// <summary>
    /// Extension methods for adding input sanitization middleware.
    /// </summary>
    public static class InputSanitizationMiddlewareExtensions
    {
        /// <summary>
        /// Adds the input sanitization middleware to the pipeline.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseInputSanitization(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<InputSanitizationMiddleware>();
        }
    }
}