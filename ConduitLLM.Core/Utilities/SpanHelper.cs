using System.Buffers;

namespace ConduitLLM.Core.Utilities;

/// <summary>
/// High-performance string operation helpers using Span&lt;T&gt; to minimize allocations.
/// </summary>
/// <remarks>
/// These helpers are designed for hot-path scenarios where string allocations
/// would create significant GC pressure. All methods attempt to use stack allocation
/// where possible, falling back to ArrayPool for larger operations.
/// </remarks>
public static class SpanHelper
{
    /// <summary>
    /// Maximum length for stack allocation. Larger operations use ArrayPool.
    /// </summary>
    private const int MaxStackAllocSize = 256;

    /// <summary>
    /// Extracts a bearer token from an Authorization header value.
    /// </summary>
    /// <param name="authorizationHeader">The full Authorization header value (e.g., "Bearer abc123").</param>
    /// <returns>The trimmed token without the "Bearer " prefix, or null if invalid.</returns>
    /// <remarks>
    /// This is a zero-allocation operation for tokens under 256 characters.
    /// Replaces: authHeader.Substring("Bearer ".Length).Trim()
    /// </remarks>
    public static string? ExtractBearerToken(string authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        ReadOnlySpan<char> headerSpan = authorizationHeader.AsSpan();

        // Check if header starts with "Bearer " (case-insensitive)
        if (headerSpan.Length <= bearerPrefix.Length ||
            !headerSpan.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Extract and trim the token part
        ReadOnlySpan<char> tokenSpan = headerSpan.Slice(bearerPrefix.Length).Trim();

        return tokenSpan.IsEmpty ? null : new string(tokenSpan);
    }

    /// <summary>
    /// Truncates a string to a maximum length and appends an ellipsis if truncated.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="maxLength">Maximum length before ellipsis.</param>
    /// <returns>The truncated string with "..." appended if it exceeded maxLength.</returns>
    /// <remarks>
    /// This is a zero-allocation operation for strings under 256 characters.
    /// Replaces: value.Substring(0, maxLength) + "..."
    /// </remarks>
    public static string TruncateWithEllipsis(ReadOnlySpan<char> value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return new string(value);
        }

        const string ellipsis = "...";
        int totalLength = maxLength + ellipsis.Length;

        // Use stack allocation for small strings
        if (totalLength <= MaxStackAllocSize)
        {
            Span<char> result = stackalloc char[totalLength];
            value.Slice(0, maxLength).CopyTo(result);
            ellipsis.AsSpan().CopyTo(result.Slice(maxLength));
            return new string(result);
        }

        // Use ArrayPool for larger strings
        char[] buffer = ArrayPool<char>.Shared.Rent(totalLength);
        try
        {
            Span<char> result = buffer.AsSpan(0, totalLength);
            value.Slice(0, maxLength).CopyTo(result);
            ellipsis.AsSpan().CopyTo(result.Slice(maxLength));
            return new string(result);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Truncates a string to a maximum length and appends an ellipsis if truncated.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="maxLength">Maximum length before ellipsis.</param>
    /// <returns>The truncated string with "..." appended if it exceeded maxLength.</returns>
    public static string TruncateWithEllipsis(string value, int maxLength)
    {
        return TruncateWithEllipsis(value.AsSpan(), maxLength);
    }

    /// <summary>
    /// Combines a base URL and endpoint path with proper slash handling.
    /// </summary>
    /// <param name="baseUrl">The base URL (e.g., "https://api.example.com").</param>
    /// <param name="endpoint">The endpoint path (e.g., "/v1/completions").</param>
    /// <returns>The combined URL with a single slash separator.</returns>
    /// <remarks>
    /// This is a zero-allocation operation for URLs under 256 characters.
    /// Replaces: baseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/')
    /// </remarks>
    public static string CombineUrl(ReadOnlySpan<char> baseUrl, ReadOnlySpan<char> endpoint)
    {
        var baseTrimmed = baseUrl.TrimEnd('/');
        var endpointTrimmed = endpoint.TrimStart('/');

        int totalLength = baseTrimmed.Length + 1 + endpointTrimmed.Length;

        // Use stack allocation for small URLs
        if (totalLength <= MaxStackAllocSize)
        {
            Span<char> result = stackalloc char[totalLength];
            int pos = 0;

            baseTrimmed.CopyTo(result);
            pos += baseTrimmed.Length;
            result[pos++] = '/';
            endpointTrimmed.CopyTo(result.Slice(pos));

            return new string(result);
        }

        // Use ArrayPool for larger URLs
        char[] buffer = ArrayPool<char>.Shared.Rent(totalLength);
        try
        {
            Span<char> result = buffer.AsSpan(0, totalLength);
            int pos = 0;

            baseTrimmed.CopyTo(result);
            pos += baseTrimmed.Length;
            result[pos++] = '/';
            endpointTrimmed.CopyTo(result.Slice(pos));

            return new string(result);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Combines a base URL and endpoint path with proper slash handling.
    /// </summary>
    /// <param name="baseUrl">The base URL (e.g., "https://api.example.com").</param>
    /// <param name="endpoint">The endpoint path (e.g., "/v1/completions").</param>
    /// <returns>The combined URL with a single slash separator.</returns>
    public static string CombineUrl(string baseUrl, string endpoint)
    {
        return CombineUrl(baseUrl.AsSpan(), endpoint.AsSpan());
    }

    /// <summary>
    /// Extracts the first segment from a comma-separated value and trims it.
    /// </summary>
    /// <param name="value">The comma-separated value (e.g., "192.168.1.1, 10.0.0.1").</param>
    /// <returns>The first segment trimmed, or the original value if no comma exists.</returns>
    /// <remarks>
    /// Common use case: Extracting client IP from X-Forwarded-For header.
    /// Replaces: value.Split(',').First().Trim()
    /// </remarks>
    public static string ExtractFirstSegment(ReadOnlySpan<char> value, char separator = ',')
    {
        int separatorIndex = value.IndexOf(separator);

        if (separatorIndex < 0)
        {
            // No separator found, return trimmed original
            return new string(value.Trim());
        }

        // Extract and trim the first segment
        ReadOnlySpan<char> firstSegment = value.Slice(0, separatorIndex).Trim();
        return new string(firstSegment);
    }

    /// <summary>
    /// Extracts the first segment from a comma-separated value and trims it.
    /// </summary>
    /// <param name="value">The comma-separated value (e.g., "192.168.1.1, 10.0.0.1").</param>
    /// <returns>The first segment trimmed, or the original value if no comma exists.</returns>
    public static string ExtractFirstSegment(string value, char separator = ',')
    {
        return ExtractFirstSegment(value.AsSpan(), separator);
    }

    /// <summary>
    /// Creates a short correlation ID from a GUID (first 8 characters).
    /// </summary>
    /// <param name="guid">The GUID to truncate.</param>
    /// <returns>The first 8 characters of the GUID string representation.</returns>
    /// <remarks>
    /// Replaces: Guid.NewGuid().ToString().Substring(0, 8)
    /// </remarks>
    public static string CreateShortCorrelationId(Guid guid)
    {
        Span<char> buffer = stackalloc char[8];
        guid.TryFormat(buffer, out _, "N"); // Format without dashes
        return new string(buffer);
    }

    /// <summary>
    /// Checks if a path contains any of the specified substrings (case-insensitive).
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="segments">The segments to search for.</param>
    /// <returns>True if any segment is found in the path.</returns>
    /// <remarks>
    /// Optimized for checking multiple path segments without string allocations.
    /// Replaces: path.ToLowerInvariant().Contains(segment1) || path.ToLowerInvariant().Contains(segment2) ...
    /// </remarks>
    public static bool ContainsAny(ReadOnlySpan<char> path, params ReadOnlySpan<string> segments)
    {
        foreach (var segment in segments)
        {
            if (path.Contains(segment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a path matches any of the specified paths exactly (case-insensitive).
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="allowedPaths">The allowed paths to match against.</param>
    /// <returns>True if the path matches any of the allowed paths.</returns>
    /// <remarks>
    /// Optimized for checking multiple exact path matches without allocations.
    /// </remarks>
    public static bool EqualsAny(ReadOnlySpan<char> path, params ReadOnlySpan<string> allowedPaths)
    {
        foreach (var allowedPath in allowedPaths)
        {
            if (path.Equals(allowedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
