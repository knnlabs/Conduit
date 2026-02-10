using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using MassTransit;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Controllers
{
    /// <summary>
    /// Base class for Gateway API controllers providing standardized OpenAI-compatible
    /// error handling and event publishing.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="ConduitLLM.Admin.Controllers.AdminControllerBase"/> but returns
    /// <see cref="OpenAIErrorResponse"/> instead of ErrorResponseDto for OpenAI API compatibility.
    /// Uses <see cref="ExceptionToResponseMapper"/> for consistent exception-to-response mapping.
    /// </remarks>
    public abstract class GatewayControllerBase : EventPublishingControllerBase
    {
        /// <summary>
        /// Logger instance for derived controllers.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Initializes a new instance with event publishing support.
        /// </summary>
        protected GatewayControllerBase(
            IPublishEndpoint? publishEndpoint,
            ILogger logger)
            : base(publishEndpoint, logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes a new instance without event publishing.
        /// </summary>
        protected GatewayControllerBase(ILogger logger)
            : this(null, logger)
        {
        }

        /// <summary>
        /// Executes an async operation with standardized OpenAI-compatible error handling.
        /// </summary>
        protected async Task<IActionResult> ExecuteAsync<T>(
            Func<Task<T>> operation,
            Func<T, IActionResult> successAction,
            string operationName,
            object? contextData = null)
        {
            try
            {
                var result = await operation();
                return successAction(result);
            }
            catch (Exception ex)
            {
                return HandleOpenAIException(ex, operationName, contextData);
            }
        }

        /// <summary>
        /// Executes an async operation that directly returns an IActionResult,
        /// with standardized OpenAI-compatible error handling.
        /// </summary>
        protected async Task<IActionResult> ExecuteAsync(
            Func<Task<IActionResult>> operation,
            string operationName,
            object? contextData = null)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                return HandleOpenAIException(ex, operationName, contextData);
            }
        }

        /// <summary>
        /// Executes a void async operation with standardized OpenAI-compatible error handling.
        /// </summary>
        protected async Task<IActionResult> ExecuteAsync(
            Func<Task> operation,
            IActionResult successResult,
            string operationName,
            object? contextData = null)
        {
            try
            {
                await operation();
                return successResult;
            }
            catch (Exception ex)
            {
                return HandleOpenAIException(ex, operationName, contextData);
            }
        }

        /// <summary>
        /// Maps an exception to an OpenAI-compatible error response using <see cref="ExceptionToResponseMapper"/>.
        /// </summary>
        private IActionResult HandleOpenAIException(
            Exception ex,
            string operationName,
            object? contextData = null)
        {
            var mapping = ExceptionToResponseMapper.Map(ex);

            var logMessage = contextData != null
                ? $"{operationName} (context: {contextData})"
                : operationName;

            Logger.Log(
                mapping.LogLevel,
                ex,
                "Error in {Operation}: {Message}",
                logMessage,
                ex.Message);

            return StatusCode(mapping.StatusCode, new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = mapping.ResponseMessage,
                    Type = MapStatusToOpenAIType(mapping.StatusCode),
                    Code = mapping.ErrorCode
                }
            });
        }

        /// <summary>
        /// Maps HTTP status codes to OpenAI error type strings.
        /// </summary>
        private static string MapStatusToOpenAIType(int statusCode)
        {
            return statusCode switch
            {
                400 => "invalid_request_error",
                401 => "authentication_error",
                403 => "permission_error",
                404 => "not_found_error",
                429 => "rate_limit_error",
                _ => "server_error"
            };
        }
    }
}
