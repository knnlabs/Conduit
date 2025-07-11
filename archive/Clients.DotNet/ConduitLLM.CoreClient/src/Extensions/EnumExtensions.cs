using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.CoreClient.Extensions;

/// <summary>
/// Extension methods for converting enums to API-compatible string values.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Converts ImageQuality enum to API string representation.
    /// </summary>
    /// <param name="quality">The image quality enum value.</param>
    /// <returns>The API string representation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the enum value is not supported.</exception>
    public static string ToApiString(this ImageQuality quality) => quality switch
    {
        ImageQuality.Standard => "standard",
        ImageQuality.Hd => "hd",
        _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, "Unsupported image quality")
    };

    /// <summary>
    /// Converts ImageResponseFormat enum to API string representation.
    /// </summary>
    /// <param name="format">The image response format enum value.</param>
    /// <returns>The API string representation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the enum value is not supported.</exception>
    public static string ToApiString(this ImageResponseFormat format) => format switch
    {
        ImageResponseFormat.Url => "url",
        ImageResponseFormat.B64Json => "b64_json",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image response format")
    };

    /// <summary>
    /// Converts ImageStyle enum to API string representation.
    /// </summary>
    /// <param name="style">The image style enum value.</param>
    /// <returns>The API string representation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the enum value is not supported.</exception>
    public static string ToApiString(this ImageStyle style) => style switch
    {
        ImageStyle.Vivid => "vivid",
        ImageStyle.Natural => "natural",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unsupported image style")
    };

    /// <summary>
    /// Converts ImageSize enum to API string representation.
    /// </summary>
    /// <param name="size">The image size enum value.</param>
    /// <returns>The API string representation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the enum value is not supported.</exception>
    public static string ToApiString(this ImageSize size) => size switch
    {
        ImageSize.Size256x256 => "256x256",
        ImageSize.Size512x512 => "512x512",
        ImageSize.Size1024x1024 => "1024x1024",
        ImageSize.Size1792x1024 => "1792x1024",
        ImageSize.Size1024x1792 => "1024x1792",
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unsupported image size")
    };
}