using System.Collections;
using ConduitLLM.Configuration.DTOs;
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

            return WrapCollectionResponse(request, result);
        }

        private static object? WrapCollectionResponse(HttpRequest request, object? result)
        {
            if (request.Method != "GET" ||
                request.Path.Equals("/v1/admin/provider-errors/recent", StringComparison.OrdinalIgnoreCase) ||
                request.Path.Equals("/v1/admin/providers/settings-schema", StringComparison.OrdinalIgnoreCase) ||
                result is not IValueHttpResult { Value: IEnumerable values } ||
                values is string or IDictionary ||
                result is IStatusCodeHttpResult { StatusCode: not (null or StatusCodes.Status200OK) })
            {
                return result;
            }

            var allItems = values.Cast<object?>().ToList();
            var page = ParsePositive(request.Query["page"].ToString(), 1);
            var pageSize = Math.Clamp(ParsePositive(request.Query["pageSize"].ToString(), 50), 1, 100);
            var offset = ((long)page - 1) * pageSize;
            var data = offset >= allItems.Count
                ? new List<object?>()
                : allItems.Skip((int)offset).Take(pageSize).ToList();

            return Results.Ok(new PagedResult<object?>
            {
                Data = data,
                Pagination = PaginationMetadata.Create(page, pageSize, allItems.Count)
            });
        }

        private static int ParsePositive(string value, int fallback) =>
            int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }
}
