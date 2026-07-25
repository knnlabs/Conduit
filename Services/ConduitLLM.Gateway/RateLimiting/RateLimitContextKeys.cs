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

    /// <summary>
    /// The key's priority tier (0 low, 1 normal, 2 high; null normal). Low-priority keys are
    /// admitted against a reduced fraction of each group ceiling, so they shed first when the
    /// group is saturated.
    /// </summary>
    public const string Priority = "VirtualKey.RateLimitPriority";

    /// <summary>Group the key belongs to — the partition for every group-scope window.</summary>
    public const string GroupId = "VirtualKey.GroupId";

    // Group ceilings apply on top of the key's own; the tighter of the two governs.
    public const string GroupRpm = "VirtualKeyGroup.RateLimitRpm";
    public const string GroupRpd = "VirtualKeyGroup.RateLimitRpd";
    public const string GroupTpm = "VirtualKeyGroup.RateLimitTpm";
    public const string GroupMaxParallelRequests = "VirtualKeyGroup.MaxParallelRequests";

    /// <summary>Raw per-model override document from the key, parsed only when a route needs it.</summary>
    public const string ModelRateLimits = "VirtualKey.ModelRateLimits";

    /// <summary>The in-flight token reservation, if this request made one.</summary>
    public const string TokenReservation = "RateLimit.TokenReservation";
}
