namespace ConduitLLM.Gateway.Endpoints;

/// <summary>Logs successful completion of Gateway Minimal-API handlers.</summary>
public sealed class OperationLoggingEndpointFilter : IEndpointFilter
{
    private readonly ILogger<OperationLoggingEndpointFilter> _logger;

    public OperationLoggingEndpointFilter(ILogger<OperationLoggingEndpointFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var result = await next(context);
        var request = context.HttpContext.Request;
        if (request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
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
