namespace ConduitLLM.Configuration.Security;

/// <summary>
/// Protects a provider credential's API key and secret-valued structured settings (for example an
/// AWS secret access key or a Google service-account JSON document).
/// </summary>
/// <remarks>
/// Values are encrypted at rest and carry a version prefix. <see cref="Reveal"/> passes through any
/// value lacking that prefix unchanged, so a value written before protection existed still reads
/// back without a data migration.
/// </remarks>
public interface IProviderSecretProtector
{
    /// <summary>Encrypts a plaintext secret for storage. Returns null/blank input unchanged.</summary>
    string? Protect(string? plaintext);

    /// <summary>
    /// Returns the plaintext secret, decrypting when the value is in protected form and returning it
    /// unchanged otherwise.
    /// </summary>
    string? Reveal(string? stored);

    /// <summary>True when the stored value is in protected (encrypted) form.</summary>
    bool IsProtected(string? stored);

    /// <summary>Encrypts every value in a settings map, leaving keys and null/blank values as they are.</summary>
    Dictionary<string, string>? ProtectAll(IReadOnlyDictionary<string, string>? plaintext);

    /// <summary>Decrypts every value in a stored settings map.</summary>
    Dictionary<string, string>? RevealAll(IReadOnlyDictionary<string, string>? stored);
}
