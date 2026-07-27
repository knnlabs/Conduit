namespace ConduitLLM.Admin.DTOs;

/// <summary>
/// Stable machine-readable error codes for the Admin API.
/// </summary>
public static class AdminErrorCodes
{
    public const string ValidationError = "validation_error";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string RequestTimeout = "request_timeout";
    public const string Conflict = "conflict";
    public const string RateLimitExceeded = "rate_limit_exceeded";
    public const string InternalError = "internal_error";
    public const string NotImplemented = "not_implemented";
    public const string ServiceUnavailable = "service_unavailable";
    public const string RequestFailed = "request_failed";

    /// <summary>
    /// Returns the canonical Admin API error code for an HTTP status.
    /// </summary>
    public static string ForStatus(int? statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ValidationError,
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status408RequestTimeout => RequestTimeout,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status429TooManyRequests => RateLimitExceeded,
        StatusCodes.Status500InternalServerError => InternalError,
        StatusCodes.Status501NotImplemented => NotImplemented,
        StatusCodes.Status503ServiceUnavailable => ServiceUnavailable,
        _ => RequestFailed
    };
}
