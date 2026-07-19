using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Models;

/// <summary>
/// One (provider, key) pair the failover decorator can attempt, with a lazily built client
/// chain. Each candidate builds the FULL existing decorator chain (base provider client →
/// prompt caching → context-aware error tracking bound to this key → performance tracking),
/// so per-key attribution and auto-disable behave exactly as they do without failover.
/// </summary>
public sealed record FailoverCandidate
{
    /// <summary>Canonical provider ID (never ProviderType) of this candidate.</summary>
    public required int ProviderId { get; init; }

    /// <summary>Provider type, carried for attribution/metrics.</summary>
    public required ProviderType ProviderType { get; init; }

    /// <summary>Key credential this candidate's chain is bound to.</summary>
    public required int KeyCredentialId { get; init; }

    /// <summary>External account group of the key (quota is typically shared per group), used
    /// to order candidates so rate-limit/balance failures hop accounts. 0 = ungrouped.</summary>
    public short ProviderAccountGroup { get; init; }

    /// <summary>The provider-native model id this candidate's client was constructed with.</summary>
    public required string ProviderModelId { get; init; }

    /// <summary>Effective base URL of this candidate (key override or provider default) —
    /// used to decide whether infrastructure errors are worth retrying on a sibling key.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Model mapping this candidate came from (provider-level failover attribution).</summary>
    public int? MappingId { get; init; }

    /// <summary>Cost configuration of this candidate's mapping (billing attribution when a
    /// non-primary candidate serves the request).</summary>
    public int? ModelCostId { get; init; }

    /// <summary>Builds (and memoizes) the full client chain for this candidate.</summary>
    public required Func<ILLMClient> ClientFactory { get; init; }

    private ILLMClient? _client;

    /// <summary>Gets the memoized client chain, building it on first use.</summary>
    public ILLMClient GetClient() => _client ??= ClientFactory();
}
