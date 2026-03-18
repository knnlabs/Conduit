using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Extensions;
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
    /// <para>
    /// Mirrors <see cref="ConduitLLM.Admin.Controllers.AdminControllerBase"/> but returns
    /// <see cref="OpenAIErrorResponse"/> instead of ErrorResponseDto for OpenAI API compatibility.
    /// Uses <see cref="ExceptionToResponseMapper"/> for consistent exception-to-response mapping.
    /// </para>
    /// <para>
    /// Features:
    /// <list type="bullet">
    ///   <item><description>Success logging with mutation/read differentiation</description></item>
    ///   <item><description>Structured error logging using ExceptionToResponseMapper's LogPrefix and IncludeExceptionMessageInLog</description></item>
    ///   <item><description>Fire-and-forget event publishing via MassTransit</description></item>
    /// </list>
    /// </para>
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
        /// Returns true if the current HTTP request is a mutation (POST, PUT, PATCH, DELETE).
        /// </summary>
        private bool IsMutationRequest()
        {
            var method = HttpContext?.Request?.Method;
            return method is "POST" or "PUT" or "PATCH" or "DELETE";
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

        /// <summary>
        /// Logs the exception with the request body for mutation requests.
        /// Falls back to logging without body if capture fails.
        /// </summary>
        private async Task LogExceptionWithBodyAsync(
            ExceptionToResponseMapper.ExceptionMappingResult mapping,
            Exception ex,
            string logMessage)
        {
            string? requestBody = null;
            try
            {
                requestBody = await RequestBodyCapture.CaptureAsync(HttpContext);
            }
            catch
            {
                // Body capture should never prevent error logging
            }

            if (requestBody != null)
            {
                if (mapping.IncludeExceptionMessageInLog)
                {
                    Logger.Log(mapping.LogLevel, ex,
                        "{LogPrefix} in {Operation}: {Message}. RequestBody: {RequestBody}",
                        mapping.LogPrefix, logMessage, ex.Message, requestBody);
                }
                else if (mapping.LogLevel == LogLevel.Error)
                {
                    Logger.LogError(ex,
                        "{LogPrefix} in {Operation}. RequestBody: {RequestBody}",
                        mapping.LogPrefix, logMessage, requestBody);
                }
                else
                {
                    Logger.LogWarning(
                        "{LogPrefix} in {Operation}. RequestBody: {RequestBody}",
                        mapping.LogPrefix, logMessage, requestBody);
                }
            }
            else
            {
                if (mapping.IncludeExceptionMessageInLog)
                {
                    Logger.Log(mapping.LogLevel, ex,
                        "{LogPrefix} in {Operation}: {Message}",
                        mapping.LogPrefix, logMessage, ex.Message);
                }
                else if (mapping.LogLevel == LogLevel.Error)
                {
                    Logger.LogError(ex,
                        "{LogPrefix} in {Operation}",
                        mapping.LogPrefix, logMessage);
                }
                else
                {
                    Logger.LogWarning(
                        "{LogPrefix} in {Operation}",
                        mapping.LogPrefix, logMessage);
                }
            }
        }
    }
}
