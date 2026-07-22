using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ConduitLLM.Security.Cryptography;

/// <summary>
/// Compares sensitive strings without short-circuiting on the first differing character.
/// </summary>
public static class ConstantTimeComparer
{
    /// <summary>
    /// Compares the exact UTF-16 code units of two values in fixed time for equal-length inputs.
    /// </summary>
    public static bool Equals(string? provided, string? expected)
    {
        if (provided is null || expected is null)
        {
            return false;
        }

        var providedBytes = MemoryMarshal.AsBytes(provided.AsSpan());
        var expectedBytes = MemoryMarshal.AsBytes(expected.AsSpan());
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
