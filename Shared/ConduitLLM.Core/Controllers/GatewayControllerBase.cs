using ConduitLLM.Core.Models;

using ConduitLLM.Configuration.Messaging;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Controllers
{
    /// <summary>
    /// Base class for Gateway API controllers providing virtual-key accessors and an
    /// OpenAI-compatible explicit-error helper.
    /// </summary>
    /// <remarks>
    /// Error handling is delegated to the global <c>OpenAIErrorMiddleware</c> (thrown exceptions are
    /// mapped to <see cref="OpenAIErrorResponse"/> via <c>ExceptionToResponseMapper</c>), and per-action
    /// success logging is provided by <c>OperationLoggingFilter</c>. The former per-action
    /// <c>ExecuteAsync</c> wrappers were removed in the Tier 1a cleanup (#902).
    /// </remarks>
    public abstract class GatewayControllerBase : EventPublishingControllerBase
    {
        /// <summary>
        /// Initializes a new instance with event publishing support.
        /// </summary>
        protected GatewayControllerBase(
            IEventBus? eventBus,
            ILogger logger)
            : base(eventBus, logger)
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
        /// Authenticated virtual key ID for the current request, or null if unauthenticated.
        /// Reads from <c>HttpContext.Items["VirtualKeyId"]</c> (populated by VirtualKeyAuthenticationHandler)
        /// and falls back to the <c>VirtualKeyId</c> claim.
        /// </summary>
        protected int? CurrentVirtualKeyId
        {
            get
            {
                if (HttpContext.Items.TryGetValue("VirtualKeyId", out var idObj) && idObj is int id)
                {
                    return id;
                }
                var claim = User.FindFirst("VirtualKeyId")?.Value;
                if (!string.IsNullOrEmpty(claim) && int.TryParse(claim, out var parsed))
                {
                    return parsed;
                }
                return null;
            }
        }

        /// <summary>
        /// Raw virtual key string for the current request, or null if unauthenticated.
        /// Reads from <c>HttpContext.Items["VirtualKey"]</c> (populated by VirtualKeyAuthenticationHandler)
        /// and falls back to the <c>VirtualKey</c> claim.
        /// </summary>
        protected string? CurrentVirtualKey
        {
            get
            {
                if (HttpContext.Items.TryGetValue("VirtualKey", out var keyObj) && keyObj is string key && !string.IsNullOrEmpty(key))
                {
                    return key;
                }
                return User.FindFirst("VirtualKey")?.Value;
            }
        }

        /// <summary>
        /// Creates an OpenAI-compatible error response for explicit (non-exception) error returns.
        /// Use this when returning validation errors or other expected failures from action methods.
        /// </summary>
        protected IActionResult OpenAIError(
            int statusCode,
            string message,
            string code,
            string type = "invalid_request_error")
        {
            return StatusCode(statusCode, new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = message,
                    Type = type,
                    Code = code
                }
            });
        }
    }
}
