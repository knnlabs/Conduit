using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.Endpoints;

/// <summary>Minimal-API balance check for billable Gateway endpoints.</summary>
public sealed class RequireBalanceEndpointFilter : IEndpointFilter
{
    private readonly IVirtualKeyService _virtualKeyService;
    private readonly ILogger<RequireBalanceEndpointFilter> _logger;

    public RequireBalanceEndpointFilter(
        IVirtualKeyService virtualKeyService,
        ILogger<RequireBalanceEndpointFilter> logger)
    {
        _virtualKeyService = virtualKeyService;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return GatewayResults.OpenAIError(
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "unauthorized",
                "authentication_error");
        }

        var virtualKey = httpContext.GetVirtualKey();
        if (virtualKey is null)
        {
            _logger.LogWarning("No virtual key found in authenticated request for balance check");
            return GatewayResults.OpenAIError(
                StatusCodes.Status401Unauthorized,
                "Virtual key not found in authentication context",
                "unauthorized",
                "authentication_error");
        }

        try
        {
            var model = httpContext.Request.RouteValues.TryGetValue("model", out var modelValue)
                ? modelValue?.ToString()
                : null;
            var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey, model);
            if (keyEntity is null)
            {
                _logger.LogWarning(
                    "Virtual key validation failed during balance check for key prefix {KeyPrefix}",
                    LoggingSanitizer.S(virtualKey[..Math.Min(10, virtualKey.Length)]));
                return GatewayResults.OpenAIError(
                    StatusCodes.Status402PaymentRequired,
                    "Your account balance is insufficient to perform this operation.",
                    "insufficient_balance",
                    "billing_error");
            }

            httpContext.Items["ValidatedVirtualKey"] = keyEntity;
            return await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during balance authorization check");
            return GatewayResults.OpenAIError(
                StatusCodes.Status500InternalServerError,
                "An error occurred while checking account balance.",
                "balance_check_error",
                "server_error");
        }
    }
}
