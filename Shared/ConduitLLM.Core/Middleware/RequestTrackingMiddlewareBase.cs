using System.Diagnostics;
using ConduitLLM.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Middleware
{
    /// <summary>
    /// Base class for request tracking middleware that provides structured logging
    /// for API requests. Subclasses customize behavior for Gateway vs Admin APIs.
    /// </summary>
    public abstract class RequestTrackingMiddlewareBase
    {
        private readonly RequestDelegate _next;
        protected readonly ILogger Logger;

        protected RequestTrackingMiddlewareBase(RequestDelegate next, ILogger logger)
        {
            _next = next;
            Logger = logger;
        }

        /// <summary>
        /// The service name used in log messages (e.g., "Gateway API" or "Admin API").
        /// </summary>
        protected abstract string ServiceName { get; }

        /// <summary>
        /// Returns true if the request should be skipped entirely (e.g., health checks).
        /// </summary>
        protected virtual bool ShouldSkipRequest(HttpContext context) => false;

        /// <summary>
        /// Returns a request identifier for logging (e.g., VirtualKeyId). Null if none.
        /// </summary>
        protected virtual string? GetRequestIdentifier(HttpContext context) => null;

        /// <summary>
        /// Called before the request is processed. Override to log request start.
        /// </summary>
        protected virtual void OnBeforeRequest(HttpContext context, string method, string path) { }

        /// <summary>
        /// Threshold in milliseconds for slow request warnings. 0 disables.
        /// </summary>
        protected virtual int SlowRequestWarningThresholdMs => 0;

        /// <summary>
        /// Returns true if the HTTP method is a mutation (POST, PUT, PATCH, DELETE).
        /// </summary>
        protected static bool IsMutationMethod(string method)
        {
            return method is "POST" or "PUT" or "PATCH" or "DELETE";
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (ShouldSkipRequest(context))
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var requestPath = context.Request.Path;
            var requestMethod = context.Request.Method;
            var isMutation = IsMutationMethod(requestMethod);
            var sanitizedPath = LoggingSanitizer.S(requestPath.ToString()) ?? string.Empty;

            OnBeforeRequest(context, requestMethod, sanitizedPath);

            try
            {
                await _next(context);

                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                var identifier = GetRequestIdentifier(context);
                var identifierSuffix = identifier != null ? $" [VirtualKey: {identifier}]" : "";

                if (SlowRequestWarningThresholdMs > 0 && elapsedMs > SlowRequestWarningThresholdMs)
                {
                    Logger.LogWarning(
                        "Slow {ServiceName} request: {Method} {Path} took {ElapsedMs}ms with status {StatusCode}{Identifier}",
                        ServiceName, requestMethod, sanitizedPath, elapsedMs, context.Response.StatusCode, identifierSuffix);
                }
                else if (isMutation || elapsedMs > 1000)
                {
                    Logger.LogInformation(
                        "{ServiceName} Request: {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms{Identifier}",
                        ServiceName, requestMethod, sanitizedPath, context.Response.StatusCode, elapsedMs, identifierSuffix);
                }
                else
                {
                    Logger.LogDebug(
                        "{ServiceName} Request: {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms{Identifier}",
                        ServiceName, requestMethod, sanitizedPath, context.Response.StatusCode, elapsedMs, identifierSuffix);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var identifier = GetRequestIdentifier(context);
                var identifierSuffix = identifier != null ? $" [VirtualKey: {identifier}]" : "";

                Logger.LogError(
                    ex,
                    "{ServiceName} Request: {Method} {Path} failed after {ElapsedMs}ms{Identifier}",
                    ServiceName, requestMethod, sanitizedPath, stopwatch.ElapsedMilliseconds, identifierSuffix);

                throw;
            }
        }
    }
}
