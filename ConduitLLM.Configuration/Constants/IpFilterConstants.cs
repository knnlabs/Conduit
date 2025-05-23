namespace ConduitLLM.Configuration.Constants;

/// <summary>
/// Constants related to IP filtering
/// </summary>
public static class IpFilterConstants
{
    /// <summary>
    /// Blacklist filter type that blocks specified IP addresses or subnets
    /// </summary>
    public const string BLACKLIST = "blacklist";
    
    /// <summary>
    /// Whitelist filter type that only allows specified IP addresses or subnets
    /// </summary>
    public const string WHITELIST = "whitelist";
    
    /// <summary>
    /// Environment variable that controls whether IP filtering is enabled
    /// </summary>
    public const string IP_FILTERING_ENABLED_ENV = "CONDUIT_IP_FILTERING_ENABLED";
    
    /// <summary>
    /// Error message for blocked IP addresses
    /// </summary>
    public const string ACCESS_DENIED_MESSAGE = "Access denied based on IP address restrictions.";
}