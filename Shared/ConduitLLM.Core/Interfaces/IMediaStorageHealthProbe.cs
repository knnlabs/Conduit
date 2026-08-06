namespace ConduitLLM.Core.Interfaces;

/// <summary>Non-mutating readiness probe for the configured media storage backend.</summary>
public interface IMediaStorageHealthProbe
{
    Task<MediaStorageHealthProbeResult> ProbeAsync(CancellationToken cancellationToken);
}

/// <summary>Result of a media storage readiness probe.</summary>
public sealed record MediaStorageHealthProbeResult(
    string Status,
    string Mode,
    string Description,
    bool Ephemeral,
    string? Bucket = null,
    string? Endpoint = null);
