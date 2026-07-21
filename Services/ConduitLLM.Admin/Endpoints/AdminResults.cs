using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Admin.Endpoints;

/// <summary>Standardized explicit results shared by Admin Minimal-API endpoint groups.</summary>
public static class AdminResults
{
    public static IResult BadRequest(string message, string? code = null) =>
        Results.BadRequest(new ErrorResponseDto(message) { Code = code });

    public static IResult NotFound(string message, string? code = null) =>
        Results.NotFound(new ErrorResponseDto(message) { Code = code });

    public static IResult NotFoundEntity(string entityType, object? entityId = null)
    {
        var message = entityId is null
            ? $"{entityType} not found"
            : $"{entityType} with ID '{entityId}' not found";
        return NotFound(message, "not_found");
    }

    public static IResult Conflict(string message, string? code = null) =>
        Results.Conflict(new ErrorResponseDto(message) { Code = code });

    public static IResult ValidationError(string message) =>
        Results.BadRequest(new ErrorResponseDto(message) { Code = "validation_error" });
}
