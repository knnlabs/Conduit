using System.Diagnostics;

using ConduitLLM.Admin.DTOs;

namespace ConduitLLM.Admin.Endpoints;

/// <summary>Standardized explicit results shared by Admin Minimal-API endpoint groups.</summary>
public static class AdminResults
{
    public static IResult BadRequest(string message, string? code = null) =>
        Problem(StatusCodes.Status400BadRequest, message, code ?? "bad_request");

    public static IResult NotFound(string message, string? code = null) =>
        Problem(StatusCodes.Status404NotFound, message, code ?? "not_found");

    public static IResult NotFoundEntity(string entityType, object? entityId = null)
    {
        var message = entityId is null
            ? $"{entityType} not found"
            : $"{entityType} with ID '{entityId}' not found";
        return NotFound(message, "not_found");
    }

    public static IResult Conflict(string message, string? code = null) =>
        Problem(StatusCodes.Status409Conflict, message, code ?? "conflict");

    public static IResult ValidationError(string message) =>
        Problem(StatusCodes.Status400BadRequest, message, "validation_error");

    public static IResult ServiceUnavailable(string message, string? code = null) =>
        Problem(StatusCodes.Status503ServiceUnavailable, message, code ?? "service_unavailable");

    public static IResult Problem(
        int status,
        string detail,
        string? code = null,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? traceId = null) =>
        Results.Json(
            new AdminProblemDetails
            {
                Type = $"https://httpstatuses.com/{status}",
                Title = TitleFor(status),
                Status = status,
                Detail = detail,
                Code = code,
                TraceId = traceId ?? Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
                Errors = errors
            },
            statusCode: status,
            contentType: "application/problem+json");

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        StatusCodes.Status501NotImplemented => "Not Implemented",
        StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
        _ => "Internal Server Error"
    };
}
