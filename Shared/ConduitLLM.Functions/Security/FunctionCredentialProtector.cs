using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Functions.Security;

/// <summary>
/// <see cref="IFunctionCredentialProtector"/> backed by ASP.NET Core Data Protection (the same
/// key ring configured for the app, Redis-persisted in production).
/// </summary>
public sealed class FunctionCredentialProtector : IFunctionCredentialProtector
{
    /// <summary>Prefix marking a value produced by this protector.</summary>
    public const string Prefix = "enc:v1:";

    private const string ProtectorPurpose = "ConduitLLM.Functions.Credentials.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<FunctionCredentialProtector> _logger;

    public FunctionCredentialProtector(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<FunctionCredentialProtector> logger)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsProtected(string? stored) =>
        stored is not null && stored.StartsWith(Prefix, StringComparison.Ordinal);

    /// <inheritdoc />
    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        // Never double-protect an already-protected value.
        if (IsProtected(plaintext))
        {
            return plaintext;
        }

        return Prefix + _protector.Protect(plaintext);
    }

    /// <inheritdoc />
    public string? Reveal(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || !IsProtected(stored))
        {
            // Legacy plaintext (or empty): return as-is.
            return stored;
        }

        var payload = stored[Prefix.Length..];
        try
        {
            return _protector.Unprotect(payload);
        }
        catch (Exception ex)
        {
            // A key-ring change or corrupted value: fail closed rather than leak the ciphertext.
            _logger.LogError(ex, "Failed to unprotect a function credential; treating it as unavailable.");
            throw new InvalidOperationException(
                "Stored function credential could not be decrypted. The Data Protection key ring may have changed.", ex);
        }
    }
}
