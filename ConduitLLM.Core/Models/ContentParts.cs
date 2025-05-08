using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System;

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
    public bool IsBase64DataUrl => Url.StartsWith("data:image/");
    
    /// <summary>
    /// Gets the MIME type from the data URL, or null if this is not a data URL
    /// </summary>
    [JsonIgnore]
    public string? MimeType => IsBase64DataUrl 
        ? Url.Substring(5, Url.IndexOf(';') - 5) 
        : null;
        
    /// <summary>
    /// Gets the base64 data without the prefix, or null if this is not a data URL
    /// </summary>
    [JsonIgnore]
    public string? Base64Data
    {
        get
        {
            if (!IsBase64DataUrl) return null;
            
            int startIndex = Url.IndexOf("base64,");
            if (startIndex < 0) return null;
            
            return Url.Substring(startIndex + 7);
        }
    }
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
    /// Creates an ImageUrl by downloading an image from an external URL and converting it to a base64 data URL
    /// </summary>
    /// <param name="url">The HTTP URL of the image</param>
    /// <param name="detail">Optional detail level for vision models</param>
    /// <returns>An ImageUrl object with the image as a base64 data URL</returns>
    public static async Task<ImageUrl> FromExternalUrlAsync(string url, string? detail = null)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
            
        if (url.StartsWith("data:"))
            return new ImageUrl { Url = url, Detail = detail };
            
        using var httpClient = new HttpClient();
        byte[] imageBytes = await httpClient.GetByteArrayAsync(url);
        
        // Try to determine MIME type from content or fall back to a default
        string mimeType = "image/jpeg"; // Default fallback
        
        // Check magic numbers for common image formats
        if (imageBytes.Length >= 2)
        {
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8) // JPEG
                mimeType = "image/jpeg";
            else if (imageBytes.Length >= 8 && 
                     imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && 
                     imageBytes[2] == 0x4E && imageBytes[3] == 0x47) // PNG
                mimeType = "image/png";
            else if (imageBytes.Length >= 3 && 
                     imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && 
                     imageBytes[2] == 0x46) // GIF
                mimeType = "image/gif";
            else if (imageBytes.Length >= 4 && 
                     (imageBytes[0] == 0x42 && imageBytes[1] == 0x4D)) // BMP
                mimeType = "image/bmp";
        }
        
        string dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";
        
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
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp", 
            ".bmp" => "image/bmp",
            _ => "application/octet-stream" // Default fallback
        };
    }
}
