using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Utilities;

/// <summary>
/// Builds storage keys while preserving the flat in-memory and date-partitioned S3 layouts.
/// </summary>
public static class MediaStorageKeys
{
    public static string GenerateFlat(string identifier, MediaType mediaType, string extension)
        => Generate(identifier, mediaType, extension, null);

    public static string GenerateDatePartitioned(
        string identifier,
        MediaType mediaType,
        string extension,
        DateTime utcDate)
        => Generate(identifier, mediaType, extension, utcDate.ToString("yyyy/MM/dd"));

    private static string Generate(
        string identifier,
        MediaType mediaType,
        string extension,
        string? partition)
    {
        var typeFolder = mediaType.ToString().ToLowerInvariant();
        return partition is null
            ? $"{typeFolder}/{identifier}{extension}"
            : $"{typeFolder}/{partition}/{identifier}{extension}";
    }
}
