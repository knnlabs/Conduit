namespace ConduitLLM.Functions.Security;

/// <summary>
/// Protects function credential secrets (API keys / MCP tokens) at rest.
/// </summary>
/// <remarks>
/// Encrypted values carry a version prefix (see the implementation). <see cref="Reveal"/> passes
/// through any value lacking that prefix unchanged, so encrypted config-scoped MCP tokens coexist
/// with legacy plaintext provider credentials (Exa/Tavily) without a data migration.
/// </remarks>
public interface IFunctionCredentialProtector
{
    /// <summary>Encrypts a plaintext secret for storage. Returns null for null/blank input.</summary>
    string? Protect(string? plaintext);

    /// <summary>
    /// Returns the plaintext secret, decrypting when the value is in protected form and returning
    /// it unchanged otherwise (legacy plaintext).
    /// </summary>
    string? Reveal(string? stored);

    /// <summary>True when the stored value is in protected (encrypted) form.</summary>
    bool IsProtected(string? stored);
}
