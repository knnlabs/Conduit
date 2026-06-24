using ConduitLLM.Configuration.DTOs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ConduitLLM.Admin.Validation
{
    /// <summary>
    /// Produces the Admin API's standard <see cref="ErrorResponseDto"/> for automatic
    /// <c>[ApiController]</c> model-state (DataAnnotation) validation failures, instead of the
    /// framework default <c>ValidationProblemDetails</c>.
    /// </summary>
    /// <remarks>
    /// This unifies validation error responses with the rest of the Admin API — the global
    /// <c>AdminExceptionMiddleware</c> and controller error helpers also emit
    /// <see cref="ErrorResponseDto"/>. Wired via
    /// <c>AddControllers().ConfigureApiBehaviorOptions(o =&gt; o.InvalidModelStateResponseFactory = Create)</c>
    /// (Tier 2a, #904).
    /// </remarks>
    public static class InvalidModelStateResponse
    {
        /// <summary>
        /// Builds a standardized <see cref="ErrorResponseDto"/> describing the model-state errors.
        /// Field errors are aggregated as <c>"{field}: {message}"</c> entries joined by "; ".
        /// </summary>
        public static ErrorResponseDto BuildErrorResponse(ModelStateDictionary modelState)
        {
            var errors = modelState
                .Where(kvp => kvp.Value != null && kvp.Value.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(e =>
                {
                    var msg = string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "The value is invalid."
                        : e.ErrorMessage;
                    return string.IsNullOrEmpty(kvp.Key) ? msg : $"{kvp.Key}: {msg}";
                }))
                .ToList();

            var message = errors.Count > 0
                ? string.Join("; ", errors)
                : "One or more validation errors occurred.";

            return new ErrorResponseDto(message) { Code = "validation_error" };
        }

        /// <summary>
        /// <c>InvalidModelStateResponseFactory</c> implementation: returns a 400 Bad Request with the
        /// standardized <see cref="ErrorResponseDto"/>.
        /// </summary>
        public static IActionResult Create(ActionContext context)
            => new BadRequestObjectResult(BuildErrorResponse(context.ModelState));
    }
}
