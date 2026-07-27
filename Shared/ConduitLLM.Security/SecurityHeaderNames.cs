namespace ConduitLLM.Security;

/// <summary>
/// Canonical names for security-sensitive request headers.
/// </summary>
public static class SecurityHeaderNames
{
    public const string Authorization = "Authorization";
    public const string ApiKey = "X-API-Key";
    public const string MasterKey = "X-Master-Key";
    public const string VirtualKey = "X-Virtual-Key";
}
