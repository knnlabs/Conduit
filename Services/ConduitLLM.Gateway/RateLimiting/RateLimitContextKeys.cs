namespace ConduitLLM.Gateway.RateLimiting;

/// <summary>
/// <see cref="HttpContext.Items"/> keys shared between the authentication handler (which reads
/// the key's configured ceilings), the middleware and endpoint filters that enforce them, and
/// the usage pipeline that reconciles token reservations.
/// </summary>
public static class RateLimitContextKeys
{
    /// <summary>Hash of the authenticated virtual key — the partition for every per-key window.</summary>
    public const string KeyHash = "VirtualKey.KeyHash";

    public const string Rpm = "VirtualKey.RateLimitRpm";
    public const string Rpd = "VirtualKey.RateLimitRpd";
    public const string Tpm = "VirtualKey.RateLimitTpm";
    public const string MaxParallelRequests = "VirtualKey.MaxParallelRequests";

    /// <summary>The in-flight token reservation, if this request made one.</summary>
    public const string TokenReservation = "RateLimit.TokenReservation";
}
