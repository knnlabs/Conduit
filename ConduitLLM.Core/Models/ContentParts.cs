using System.Text.Json.Serialization;
using System.Collections.Generic;

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
}
