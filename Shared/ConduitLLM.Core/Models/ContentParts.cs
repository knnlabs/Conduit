using System.Text.Json;
using System.Text.Json.Serialization;

using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a text content part in a multimodal message.
/// </summary>
public class TextContentPart
{
    /// <summary>
    /// The type of content part. Always "text" for text content.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => "text";

    /// <summary>
    /// The text content.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

/// <summary>
/// Represents an image content part in a multimodal message.
/// </summary>
public class ImageUrlContentPart
{
    /// <summary>
    /// The type of content part. Always "image_url" for image URL content.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => "image_url";

    /// <summary>
    /// The image URL information.
    /// </summary>
    [JsonPropertyName("image_url")]
    public required ImageUrl ImageUrl { get; set; }
}

/// <summary>
/// Represents an image URL and its associated metadata.
/// </summary>
public class ImageUrl
{
    /// <summary>
    /// The URL of the image. Can be a data URL ("data:image/...") or an HTTP URL.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// Optional detail level for the image. Can be "low", "high", or "auto".
    /// </summary>
    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    /// <summary>
    /// Returns true if the URL is a base64 data URL
    /// </summary>
    [JsonIgnore]
    public bool IsBase64DataUrl =>
        DataUrl.TryParse(Url, out var dataUrl) &&
        dataUrl.IsBase64 &&
        dataUrl.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the MIME type from the data URL, or null if this is not a data URL
    /// </summary>
    [JsonIgnore]
    public string? MimeType =>
        DataUrl.TryParse(Url, out var dataUrl) && dataUrl.IsBase64
            ? dataUrl.MediaType
            : null;

    /// <summary>
    /// Gets the base64 data without the prefix, or null if this is not a data URL
    /// </summary>
    [JsonIgnore]
    public string? Base64Data
    {
        get
        {
            return DataUrl.TryParse(Url, out var dataUrl) && dataUrl.IsBase64
                ? dataUrl.Data
                : null;
        }
    }
}

/// <summary>
/// Represents an audio input content part in a multimodal message.
/// </summary>
public class InputAudioContentPart
{
    [JsonPropertyName("type")]
    public string Type => "input_audio";

    [JsonPropertyName("input_audio")]
    public required InputAudio InputAudio { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Base64-encoded audio and its container/sample format.</summary>
public class InputAudio
{
    [JsonPropertyName("data")]
    public required string Data { get; set; }

    [JsonPropertyName("format")]
    public required string Format { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Represents a document/file content part in a multimodal message.</summary>
public class FileContentPart
{
    [JsonPropertyName("type")]
    public string Type => "file";

    [JsonPropertyName("file")]
    public required FileContent File { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// A file supplied by URL/data URL or by a provider-bound uploaded file identifier.
/// Exactly one of <see cref="FileData"/> and <see cref="FileId"/> must be set.
/// </summary>
public class FileContent
{
    [JsonPropertyName("filename")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filename { get; set; }

    [JsonPropertyName("file_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileData { get; set; }

    [JsonPropertyName("file_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Extensible provider content part for forward-compatible content types.
/// Known providers may pass these through without Conduit discarding their fields.
/// </summary>
public class ProviderContentPart
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>An OpenRouter-compatible parsed-file annotation.</summary>
public class FileAnnotation
{
    [JsonPropertyName("type")]
    public string Type => "file";

    [JsonPropertyName("file")]
    public required ParsedFileAnnotation File { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Parsed file content that can be sent back to avoid parsing the same file again.</summary>
public class ParsedFileAnnotation
{
    [JsonPropertyName("hash")]
    public required string Hash { get; set; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("content")]
    public required List<JsonElement> Content { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Extension methods for working with ImageUrl objects
/// </summary>
public static class ImageUrlExtensions
{
    /// <summary>
    /// Creates an ImageUrl from a file path by reading the file and converting it to a base64 data URL
    /// </summary>
    /// <param name="filePath">Path to the image file</param>
    /// <param name="detail">Optional detail level for vision models</param>
    /// <returns>An ImageUrl object with the file contents as a base64 data URL</returns>
    public static async Task<ImageUrl> FromFilePathAsync(string filePath, string? detail = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Image file not found: {filePath}");

        byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
        string mimeType = GetMimeTypeFromFileExtension(Path.GetExtension(filePath));

        string dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(fileBytes)}";

        return new ImageUrl
        {
            Url = dataUrl,
            Detail = detail
        };
    }

    /// <summary>
    /// Gets a MIME type based on the file extension
    /// </summary>
    private static string GetMimeTypeFromFileExtension(string extension)
    {
        return Utilities.MediaContentTypes.GetContentType(extension) ?? "application/octet-stream";
    }
}
