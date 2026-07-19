using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Core.Configuration;

/// <summary>
/// Configuration for in-request failover across provider keys (and, when
/// <see cref="ProviderFailoverEnabled"/> is set, across providers).
/// </summary>
/// <remarks>
/// Bound from the <c>Conduit:Failover</c> section. Both flags default to <b>off</b>: with
/// <see cref="Enabled"/> false the client factory returns exactly the single-client chain it
/// always has, byte for byte.
/// </remarks>
public class FailoverOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Conduit:Failover";

    /// <summary>
    /// Master switch for in-request key-level failover. Environment override:
    /// <c>CONDUIT_FAILOVER_ENABLED</c> (mapped in configuration). Default off.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Enables cross-provider failover (requires <see cref="Enabled"/> too). Shipped by the
    /// provider-level failover PR; has no effect until candidates from multiple providers
    /// exist. Default off.
    /// </summary>
    public bool ProviderFailoverEnabled { get; set; } = false;

    /// <summary>Maximum keys attempted per provider (primary + alternates).</summary>
    [Range(1, 10)]
    public int MaxKeyAttemptsPerProvider { get; set; } = 2;

    /// <summary>Hard cap on total attempts across all providers.</summary>
    [Range(1, 10)]
    public int MaxTotalAttempts { get; set; } = 3;

    /// <summary>
    /// Total wall-clock budget for the failover loop. A new attempt is not started once 60% of
    /// this budget has elapsed (an attempt that cannot plausibly finish only adds latency).
    /// For streaming, the budget governs only the pre-first-chunk phase — a healthy long
    /// stream is never cut off by it.
    /// </summary>
    [Range(5, 600)]
    public double TotalBudgetSeconds { get; set; } = 90;

    /// <summary>
    /// When false (default), image generation only fails over on auth-class errors
    /// (401/402/403) — never on timeouts or 5xx, where the provider may have accepted the
    /// generation job and a retry would double-generate and double-cost.
    /// </summary>
    public bool EnableFullMediaFailover { get; set; } = false;
}
