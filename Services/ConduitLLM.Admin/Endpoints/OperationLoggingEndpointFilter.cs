using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Endpoints
{
    /// <summary>
    /// Logs successful endpoint
    /// completion (Information for mutations, Debug for reads). Exceptions are not caught here —
    /// they propagate to the global <c>AdminExceptionMiddleware</c>.
    /// </summary>
    /// <remarks>Introduced for the Tier 3 Minimal-API pilot (#906).</remarks>
    public sealed class OperationLoggingEndpointFilter : IEndpointFilter
    {
        private readonly ILogger<OperationLoggingEndpointFilter> _logger;

        public OperationLoggingEndpointFilter(ILogger<OperationLoggingEndpointFilter> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            // If the handler throws, this awaits-and-rethrows so the exception reaches the global
            // middleware; the success log below only runs when the handler completed normally.
            var result = await next(context);

            var request = context.HttpContext.Request;
            var isMutation = request.Method is "POST" or "PUT" or "PATCH" or "DELETE";

            if (isMutation)
            {
                _logger.LogInformation("{Method} {Path} completed successfully", request.Method, request.Path);
            }
            else
            {
                _logger.LogDebug("{Method} {Path} completed successfully", request.Method, request.Path);
            }

            return result;
        }
    }
}
