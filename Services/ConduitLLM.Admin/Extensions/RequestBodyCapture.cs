using System.Text.RegularExpressions;

using ConduitLLM.Core.Extensions;

namespace ConduitLLM.Admin.Extensions;

/// <summary>
/// Provides methods for capturing and sanitizing HTTP request bodies for error diagnostics.
/// Used by both <see cref="Controllers.AdminControllerBase"/> and
/// <see cref="Middleware.AdminExceptionMiddleware"/> to log request payloads on failure.
/// </summary>
public static partial class RequestBodyCapture
{
    /// <summary>
    /// Maximum number of characters to capture from the request body.
    /// </summary>
    private const int MaxBodyLength = 4096;

    /// <summary>
    /// HTTP methods that typically carry a request body worth capturing.
    /// </summary>
    private static readonly HashSet<string> MutationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE"
    };

    /// <summary>
    /// Reads and sanitizes the request body from the current HTTP context.
    /// Returns null for GET/HEAD requests or when the body is empty/unreadable.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The sanitized request body, or null if not applicable.</returns>
    public static async Task<string?> CaptureAsync(HttpContext? context)
    {
        if (context == null)
            return null;

        // Only capture body for mutation methods
        if (!MutationMethods.Contains(context.Request.Method))
            return null;

        // Content-Length check — skip if body is clearly empty
        if (context.Request.ContentLength is 0)
            return null;

        try
        {
            // Ensure the body can be re-read (requires EnableBuffering called earlier)
            context.Request.Body.Position = 0;

            using var reader = new StreamReader(
                context.Request.Body,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();

            // Reset position for any downstream consumers
            context.Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
                return null;

            // Redact sensitive fields first, then truncate
            var redacted = RedactSensitiveFields(body);

            if (redacted.Length > MaxBodyLength)
            {
                redacted = redacted[..MaxBodyLength] + "...[truncated]";
            }

            // LoggingSanitizer.S() strips control characters and enforces its own
            // max length (1000 chars), providing a final safety net
            return LoggingSanitizer.S(redacted);
        }
        catch
        {
            // Body read failures should never impact error handling
            return null;
        }
    }

    /// <summary>
    /// Redacts values of JSON fields whose names suggest sensitive content.
    /// Works on raw strings — does not require valid JSON.
    /// </summary>
    private static string RedactSensitiveFields(string body)
    {
        return SensitiveFieldPattern().Replace(body, "$1\"[REDACTED]\"");
    }

    /// <summary>
    /// Matches JSON key-value pairs where the key contains a sensitive keyword.
    /// Captures: "apiKey": "some-value" → "apiKey": "[REDACTED]"
    /// Group 1 captures everything up to and including the colon+whitespace before the value.
    /// </summary>
    [GeneratedRegex(
        """("(?:[^"]*(?:key|secret|password|token|credential|auth|apikey|api_key)[^"]*)":\s*)"(?:[^"\\]|\\.)*""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SensitiveFieldPattern();
}
