using System.Text.Json;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Gateway.RateLimiting;

/// <summary>
/// Single source of the 429 contract. Limits are enforced in two places — middleware for the
/// windows knowable before the body is read, an endpoint filter for the ones that need the
/// bound request — and a client must not be able to tell which one rejected it.
/// </summary>
public static class RateLimitResponse
{
    /// <summary>
    /// Sets the informational X-RateLimit-* family. Written on allowed responses too, so
    /// clients can pace themselves before they are ever throttled.
    /// </summary>
    public static void SetHeaders(HttpContext context, long limit, long remaining, DateTime resetsAt, string scope)
    {
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = ResetUnixSeconds(resetsAt).ToString();
        if (!string.IsNullOrEmpty(scope))
        {
            context.Response.Headers["X-RateLimit-Scope"] = scope;
        }
    }

    /// <summary>
    /// Seconds a client should wait, rounded up. Truncating would advertise an instant
    /// fractionally before the window frees, so a client retrying on the hint is rejected again.
    /// </summary>
    public static int RetryAfterSeconds(DateTime resetsAt) =>
        Math.Max(1, (int)Math.Ceiling((resetsAt - DateTime.UtcNow).TotalSeconds));

    /// <summary>
    /// Reset instant as whole unix seconds, rounded up for the same reason.
    /// </summary>
    public static long ResetUnixSeconds(DateTime resetsAt)
    {
        var ms = new DateTimeOffset(DateTime.SpecifyKind(resetsAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        return (ms + 999) / 1000;
    }

    /// <summary>
    /// Writes the 429 body and Retry-After directly to the response. Used by middleware, which
    /// has no <c>IResult</c> to return.
    /// </summary>
    public static async Task WriteAsync(HttpContext context, string scope, long limit, DateTime resetsAt, string? message = null)
    {
        var retryAfter = RetryAfterSeconds(resetsAt);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] = retryAfter.ToString();
        context.Response.ContentType = "application/json";

        await JsonSerializer.SerializeAsync(context.Response.Body, BuildError(scope, limit, retryAfter, message));
    }

    /// <summary>
    /// Builds the identical response as an <see cref="IResult"/> for endpoint filters.
    /// </summary>
    public static IResult AsResult(HttpContext context, string scope, long limit, DateTime resetsAt, string? message = null)
    {
        var retryAfter = RetryAfterSeconds(resetsAt);
        context.Response.Headers["Retry-After"] = retryAfter.ToString();

        return Results.Json(
            BuildError(scope, limit, retryAfter, message),
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    private static OpenAIErrorResponse BuildError(string scope, long limit, int retryAfterSeconds, string? message) =>
        new()
        {
            Error = new OpenAIError
            {
                Message = message ?? $"{scope} rate limit exceeded ({limit}). Retry after {retryAfterSeconds} seconds.",
                Type = "rate_limit_exceeded",
                Code = "rate_limit_exceeded"
            }
        };
}
