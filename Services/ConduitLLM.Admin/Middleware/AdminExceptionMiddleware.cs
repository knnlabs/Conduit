using System.Text.Json;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Middleware;

using Microsoft.AspNetCore.Hosting;

namespace ConduitLLM.Admin.Middleware;

/// <summary>
/// Global exception handling middleware for the Admin API.
/// Catches any unhandled exceptions that escape controller-level error handling
/// and returns standardized <see cref="ErrorResponseDto"/> responses.
/// </summary>
/// <remarks>
/// This is the common safety net for exceptions raised by Admin endpoints.
/// This middleware catches anything that slips through, ensuring the Admin API never returns
/// raw exception details to clients.
/// </remarks>
public class AdminExceptionMiddleware : ExceptionHandlingMiddlewareBase
{
    protected override string MiddlewareName => "AdminExceptionMiddleware";

    public AdminExceptionMiddleware(
        RequestDelegate next,
        ILogger<AdminExceptionMiddleware> logger,
        IWebHostEnvironment environment)
        : base(next, logger, environment)
    {
    }

    /// <inheritdoc/>
    protected override string CreateErrorResponseJson(
        string message,
        ExceptionToResponseMapper.ExceptionMappingResult mapping)
    {
        var errorResponse = new ErrorResponseDto(message) { Code = mapping.ErrorCode };
        return JsonSerializer.Serialize(errorResponse, ErrorJsonOptions);
    }
}

/// <summary>
/// Extension methods for Admin exception middleware.
/// </summary>
public static class AdminExceptionMiddlewareExtensions
{
    /// <summary>
    /// Adds the Admin API global exception handling middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseAdminExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AdminExceptionMiddleware>();
    }
}
