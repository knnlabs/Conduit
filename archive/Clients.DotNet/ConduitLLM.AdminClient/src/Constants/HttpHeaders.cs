namespace ConduitLLM.AdminClient.Constants;

/// <summary>
/// HTTP header name constants for type-safe header management.
/// </summary>
public static class HttpHeaders
{
    /// <summary>
    /// Authorization header name.
    /// </summary>
    public const string Authorization = "Authorization";

    /// <summary>
    /// User-Agent header name.
    /// </summary>
    public const string UserAgent = "User-Agent";

    /// <summary>
    /// Content-Type header name.
    /// </summary>
    public const string ContentType = "Content-Type";

    /// <summary>
    /// Accept header name.
    /// </summary>
    public const string Accept = "Accept";
}

/// <summary>
/// User-Agent string constants for different client types.
/// </summary>
public static class UserAgents
{
    /// <summary>
    /// User-Agent string for the Core API client.
    /// </summary>
    public const string CoreClient = "ConduitLLM.CoreClient/1.0.0";

    /// <summary>
    /// User-Agent string for the Admin API client.
    /// </summary>
    public const string AdminClient = "ConduitLLM.AdminClient/1.0.0";
}