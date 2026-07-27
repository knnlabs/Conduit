namespace ConduitLLM.Core.Models;

/// <summary>
/// Common expiry contract for ephemeral authentication responses.
/// </summary>
public abstract class EphemeralKeyResponseBase
{
    [System.Text.Json.Serialization.JsonIgnore]
    protected string Token { get; set; } = string.Empty;

    /// <summary>
    /// When the ephemeral credential expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Number of seconds until the credential expires.
    /// </summary>
    public int ExpiresInSeconds { get; set; }
}
