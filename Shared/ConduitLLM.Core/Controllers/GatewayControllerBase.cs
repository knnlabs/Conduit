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
    /// Returns <see cref="OpenAIErrorResponse"/> for OpenAI API compatibility.
    /// Uses <see cref="ExceptionToResponseMapper"/> for consistent exception-to-response mapping.
    /// Shared utility methods (IsMutationRequest, LogExceptionWithBodyAsync) are in
    /// <see cref="EventPublishingControllerBase"/>.
    /// </remarks>
    public abstract class GatewayControllerBase : EventPublishingControllerBase
    {
        /// <summary>
        /// Initializes a new instance with event publishing support.
        /// </summary>
        protected GatewayControllerBase(
            IPublishEndpoint? publishEndpoint,
            ILogger logger)
            : base(publishEndpoint, logger)
        {
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
                LogOperationSuccess(operationName, contextData);
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
                var result = await operation();
                LogOperationSuccess(operationName, contextData);
                return result;
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
                LogOperationSuccess(operationName, contextData);
                return successResult;
            }
            catch (Exception ex)
            {
                return HandleOpenAIException(ex, operationName, contextData);
            }
        }

        /// <summary>
        /// Logs operation success at Information level for mutations (POST/PUT/PATCH/DELETE)
        /// and Debug level for reads (GET/HEAD/OPTIONS).
        /// </summary>
        private void LogOperationSuccess(string operationName, object? contextData = null)
        {
            if (IsMutationRequest())
            {
                if (contextData != null)
                {
                    Logger.LogInformation("{OperationName} completed successfully with context {ContextData}",
                        operationName, contextData);
                }
                else
                {
                    Logger.LogInformation("{OperationName} completed successfully", operationName);
                }
            }
            else
            {
                if (contextData != null)
                {
                    Logger.LogDebug("{OperationName} completed successfully with context {ContextData}",
                        operationName, contextData);
                }
                else
                {
                    Logger.LogDebug("{OperationName} completed successfully", operationName);
                }
            }
        }

        /// <summary>
        /// Maps an exception to an OpenAI-compatible error response using <see cref="ExceptionToResponseMapper"/>.
        /// Uses the mapper's LogPrefix and IncludeExceptionMessageInLog for structured, consistent error logging.
        /// Captures request body for mutation failures (fire-and-forget) for post-mortem diagnostics.
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

            // Capture request body for mutation failures (fire-and-forget — don't block error response)
            _ = LogExceptionWithBodyAsync(mapping, ex, logMessage);

            return StatusCode(mapping.StatusCode, new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = mapping.ResponseMessage,
                    Type = mapping.OpenAIErrorType,
                    Code = mapping.ErrorCode,
                    Param = mapping.Param
                }
            });
        }

    }
}
