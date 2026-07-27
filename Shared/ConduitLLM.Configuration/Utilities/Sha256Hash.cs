using System.Security.Cryptography;
using System.Text;

namespace ConduitLLM.Configuration.Utilities;

/// <summary>
/// Computes SHA-256 values using the encodings already persisted by Conduit.
/// </summary>
public static class Sha256Hash
{
    /// <summary>
    /// Computes a lowercase hexadecimal SHA-256 value.
    /// </summary>
    public static string LowerHex(string value) =>
        LowerHex(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Computes a lowercase hexadecimal SHA-256 value.
    /// </summary>
    public static string LowerHex(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    /// <summary>
    /// Computes an uppercase hexadecimal SHA-256 value.
    /// </summary>
    public static string UpperHex(string value) =>
        UpperHex(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Computes an uppercase hexadecimal SHA-256 value.
    /// </summary>
    public static string UpperHex(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value));

    /// <summary>
    /// Computes a SHA-256 value using the RFC 4648 base64url alphabet.
    /// </summary>
    public static string Base64Url(string value) =>
        Base64Url(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Computes a SHA-256 value using the RFC 4648 base64url alphabet.
    /// </summary>
    public static string Base64Url(ReadOnlySpan<byte> value) =>
        EncodeBase64Url(SHA256.HashData(value));

    /// <summary>
    /// Computes a SHA-256 value using Conduit's legacy storage-key alphabet.
    /// This intentionally maps slash to dash and plus to underscore.
    /// </summary>
    public static string LegacyStorageBase64Url(string value) =>
        LegacyStorageBase64Url(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Computes a SHA-256 value using Conduit's legacy storage-key alphabet.
    /// </summary>
    public static string LegacyStorageBase64Url(ReadOnlySpan<byte> value) =>
        EncodeLegacyStorageBase64Url(SHA256.HashData(value));

    /// <summary>
    /// Computes a SHA-256 stream value using Conduit's legacy storage-key alphabet.
    /// </summary>
    public static async Task<string> LegacyStorageBase64UrlAsync(
        Stream value,
        CancellationToken cancellationToken = default)
    {
        var hash = await SHA256.HashDataAsync(value, cancellationToken);
        return EncodeLegacyStorageBase64Url(hash);
    }

    private static string EncodeBase64Url(ReadOnlySpan<byte> hash) =>
        Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static string EncodeLegacyStorageBase64Url(ReadOnlySpan<byte> hash) =>
        Convert.ToBase64String(hash)
            .Replace('/', '-')
            .Replace('+', '_')
            .TrimEnd('=');
}
