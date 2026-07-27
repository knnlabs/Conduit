namespace ConduitLLM.Core.Utilities;

/// <summary>Represents the parsed metadata and payload of an RFC 2397 data URL.</summary>
public readonly record struct DataUrl(string MediaType, string Data, bool IsBase64)
{
    private const string Scheme = "data:";

    /// <summary>Returns whether a value uses the data URL scheme.</summary>
    public static bool IsDataUrl(string? value) =>
        value?.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Parses a data URL without decoding its payload. The scheme and base64 marker are
    /// case-insensitive; callers remain responsible for validating the media type and payload.
    /// </summary>
    public static bool TryParse(string? value, out DataUrl dataUrl)
    {
        dataUrl = default;
        if (!IsDataUrl(value))
        {
            return false;
        }

        var separator = value!.IndexOf(',');
        if (separator < Scheme.Length)
        {
            return false;
        }

        var metadata = value.AsSpan(Scheme.Length, separator - Scheme.Length);
        var segments = metadata.ToString().Split(';');
        var mediaType = segments[0].Length == 0 ? "text/plain" : segments[0];
        var isBase64 = segments
            .Skip(1)
            .Any(segment => segment.Equals("base64", StringComparison.OrdinalIgnoreCase));

        dataUrl = new DataUrl(mediaType, value[(separator + 1)..], isBase64);
        return true;
    }
}
