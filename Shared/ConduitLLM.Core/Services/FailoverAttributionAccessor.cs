using ConduitLLM.Configuration;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Attribution snapshot for the candidate that produced the response (or the final failure).
/// </summary>
public sealed record FailoverAttribution(
    int ProviderId,
    ProviderType ProviderType,
    int KeyCredentialId,
    string ProviderModelId,
    int? MappingId,
    int? ModelCostId);

/// <summary>
/// Request-scoped bridge between the failover decorator and gateway middleware: the decorator
/// records the candidate before each attempt (last write wins = the serving candidate), and
/// usage/billing middleware plus controllers read it to attribute the request correctly and to
/// surface the <c>X-Conduit-Fallback</c> header.
/// </summary>
/// <remarks>
/// This is deliberately a scoped service and NOT an AsyncLocal: values set inside the
/// controller's async flow do not propagate upward to middleware, while a scoped instance is
/// shared by the (scoped) client factory and everything else in the request scope.
/// </remarks>
public interface IFailoverAttributionAccessor
{
    /// <summary>The most recently attempted candidate (the serving one once a response exists).</summary>
    FailoverAttribution? Current { get; }

    /// <summary>Number of candidates attempted so far in this request.</summary>
    int AttemptCount { get; }

    /// <summary>True when more than one candidate was attempted.</summary>
    bool FailoverOccurred { get; }

    /// <summary>Records the candidate about to be attempted.</summary>
    void RecordAttempt(FailoverAttribution attribution);
}

/// <inheritdoc />
public sealed class FailoverAttributionAccessor : IFailoverAttributionAccessor
{
    public FailoverAttribution? Current { get; private set; }

    public int AttemptCount { get; private set; }

    public bool FailoverOccurred => AttemptCount > 1;

    public void RecordAttempt(FailoverAttribution attribution)
    {
        Current = attribution;
        AttemptCount++;
    }
}
