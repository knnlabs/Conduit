using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Security;

/// <summary>
/// <see cref="IProviderSecretProtector"/> backed by ASP.NET Core Data Protection (the same key ring
/// the application configures, Redis-persisted in production so every node can read what any node
/// wrote).
/// </summary>
public sealed class ProviderSecretProtector : IProviderSecretProtector
{
    /// <summary>Prefix marking a value produced by this protector.</summary>
    public const string Prefix = "penc:v1:";

    private const string ProtectorPurpose = "ConduitLLM.Providers.SecretSettings.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<ProviderSecretProtector> _logger;

    /// <summary>Initializes the protector from the application's data-protection provider.</summary>
    public ProviderSecretProtector(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ProviderSecretProtector> logger)
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

        // Protect is called on every write, including updates that re-send unchanged values, so it
        // must never wrap an already-protected value a second time.
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
            return stored;
        }

        try
        {
            return _protector.Unprotect(stored[Prefix.Length..]);
        }
        catch (Exception ex)
        {
            // A rotated or lost key ring: fail closed rather than hand the ciphertext to a provider
            // as though it were the credential.
            _logger.LogError(ex, "Failed to unprotect a provider secret setting; treating it as unavailable.");
            throw new InvalidOperationException(
                "A stored provider secret could not be decrypted. The Data Protection key ring may have changed.", ex);
        }
    }

    /// <inheritdoc />
    public Dictionary<string, string>? ProtectAll(IReadOnlyDictionary<string, string>? plaintext) =>
        Map(plaintext, Protect);

    /// <inheritdoc />
    public Dictionary<string, string>? RevealAll(IReadOnlyDictionary<string, string>? stored) =>
        Map(stored, Reveal);

    private static Dictionary<string, string>? Map(
        IReadOnlyDictionary<string, string>? source,
        Func<string?, string?> transform)
    {
        if (source == null)
        {
            return null;
        }

        var result = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);
        foreach (var pair in source)
        {
            var value = transform(pair.Value);
            if (value != null)
            {
                result[pair.Key] = value;
            }
        }

        return result;
    }
}
