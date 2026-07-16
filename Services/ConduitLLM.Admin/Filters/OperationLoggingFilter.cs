using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ConduitLLM.Admin.Filters
{
    /// <summary>
    /// Logs successful completion of controller actions, replacing the per-action success
    /// logging that previously lived inside <c>AdminControllerBase.ExecuteAsync</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Logs at <see cref="Microsoft.Extensions.Logging.LogLevel.Information"/> for mutations
    /// (POST/PUT/PATCH/DELETE) and <see cref="Microsoft.Extensions.Logging.LogLevel.Debug"/> for
    /// reads — matching the previous <c>LogOperationSuccess</c> behavior. The log category is the
    /// concrete controller type, so existing per-controller log filtering keeps working.
    /// </para>
    /// <para>
    /// Exceptions are intentionally ignored here: they propagate to the global
    /// <c>AdminExceptionMiddleware</c>, which owns exception-to-response mapping and error logging.
    /// </para>
    /// <para>
    /// Applied per controller via <c>[ServiceFilter(typeof(OperationLoggingFilter))]</c> during the
    /// incremental Tier 1a migration (issue #902). Once every controller is converted off the
    /// <c>ExecuteAsync</c> wrappers, this can be promoted to a single global filter registered in
    /// <c>AddControllers(o =&gt; o.Filters.Add&lt;OperationLoggingFilter&gt;())</c> and the
    /// per-controller attributes removed.
    /// </para>
    /// </remarks>
    public sealed class OperationLoggingFilter : IAsyncActionFilter
    {
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="OperationLoggingFilter"/> class.
        /// </summary>
        /// <param name="loggerFactory">Factory used to create a logger categorized to the controller type.</param>
        public OperationLoggingFilter(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc/>
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();

            // Success only — exceptions are owned by the global exception middleware. Leave the
            // exception unhandled so it continues to propagate there.
            if (executed.Exception != null && !executed.ExceptionHandled)
            {
                return;
            }

            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var actionName = descriptor?.ActionName ?? "Unknown";

            var logger = descriptor?.ControllerTypeInfo.FullName is { } categoryName
                ? _loggerFactory.CreateLogger(categoryName)
                : _loggerFactory.CreateLogger<OperationLoggingFilter>();

            var method = context.HttpContext.Request.Method;
            var isMutation = method is "POST" or "PUT" or "PATCH" or "DELETE";

            if (isMutation)
            {
                logger.LogInformation("{Action} completed successfully", actionName);
            }
            else
            {
                logger.LogDebug("{Action} completed successfully", actionName);
            }
        }
    }
}
