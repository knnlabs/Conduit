namespace ConduitLLM.Gateway.Options;

/// <summary>
/// Controls half-open probes for keys disabled because their provider account lacked balance.
/// </summary>
public sealed class ProviderKeyReprobeOptions
{
    public const string SectionName = "ProviderKeyReprobe";

    /// <summary>Whether automatic balance recovery probes are enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the worker scans Redis for eligible keys.</summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Cooldown before the first probe and base for exponential backoff.</summary>
    public TimeSpan InitialCooldown { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Maximum delay between failed probes.</summary>
    public TimeSpan MaxCooldown { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Distributed per-key lock duration for a single probe.</summary>
    public TimeSpan ProbeLockTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Maximum keys probed during one scan.</summary>
    public int BatchSize { get; set; } = 50;
}
